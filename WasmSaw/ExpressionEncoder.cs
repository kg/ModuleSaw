using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ModuleSaw;
using Wasm.Model;

namespace WasmSaw {
    public class ExpressionEncoder {
        public readonly AbstractModuleBuilder Builder;

        private KeyedStreamWriter OpcodeStream, GlobalIndices, LocalIndices,
            MemoryAlignments, MemoryOffsets, BrTables, BlockTypes,
            FunctionIndices, TypeIndices, BreakDepths;

        // [prev, current, next, default(Expression)]
        private Expression[] WriteQueue = new Expression[4];
        private int WriteQueueLength = 0;

        public ExpressionEncoder (AbstractModuleBuilder builder) {
            Builder = builder;

            OpcodeStream = builder.GetStream("opcode");
            GlobalIndices = builder.GetStream("global_index");
            LocalIndices = builder.GetStream("local_index");
            FunctionIndices = builder.GetStream("function_index");
            TypeIndices = builder.GetStream("type_index");
            MemoryAlignments = builder.GetStream("memory_alignment");
            MemoryOffsets = builder.GetStream("memory_offset");
            BrTables = builder.GetStream("br_table");
            BlockTypes = builder.GetStream("block_type");
            BreakDepths = builder.GetStream("break_depth");
        }

        private KeyedStreamWriter GetStreamForOpcode (Opcodes opcode) {
            switch (opcode) {
                case Opcodes.br:
                case Opcodes.br_if:
                    return BreakDepths;
                case Opcodes.get_global:
                case Opcodes.set_global:
                    return GlobalIndices;
                case Opcodes.get_local:
                case Opcodes.set_local:
                case Opcodes.tee_local:
                    return LocalIndices;
                case Opcodes.call:
                    return FunctionIndices;
                case Opcodes.call_indirect:
                    return TypeIndices;
                case FakeOpcodes.i32_load_natural:
                case FakeOpcodes.i32_store_natural:
                    return MemoryOffsets;
                /*
                case FakeOpcodes.i32_load_relative:
                case FakeOpcodes.i32_store_relative:
                    return MemoryOffsets;
                */
            }

            return null;
        }

        public static class FakeOpcodes {
            public const byte FirstFakeOpcode = 0xF0;
            public const Opcodes read_prior_local = (Opcodes)(FirstFakeOpcode + 0);
            public const Opcodes i32_load_natural = (Opcodes)(FirstFakeOpcode + 1);
            public const Opcodes i32_store_natural = (Opcodes)(FirstFakeOpcode + 2);
            public const Opcodes ldc_i32_zero = (Opcodes)(FirstFakeOpcode + 3);
            public const Opcodes ldc_i32_one = (Opcodes)(FirstFakeOpcode + 4);
            public const Opcodes ldc_i32_minus_one = (Opcodes)(FirstFakeOpcode + 5);
            /*
            public const Opcodes i32_load_relative = (Opcodes)(FirstFakeOpcode + 1);
            public const Opcodes i32_store_relative = (Opcodes)(FirstFakeOpcode + 2);
            */
        }

        private void WriteInternal (
            ref Expression e
        ) {
            OpcodeStream.Write((byte)e.Opcode);

            KeyedStreamWriter s = GetStreamForOpcode(e.Opcode);

            switch (e.Body.Type & ~ExpressionBody.Types.children) {
                case ExpressionBody.Types.none:
                    break;

                case ExpressionBody.Types.u32:
                    Builder.Write(e.Body.U.u32, s);
                    break;
                case ExpressionBody.Types.u1:
                    (s ?? Builder.ByteStream).Write((byte)e.Body.U.u32);
                    break;
                case ExpressionBody.Types.i64:
                    Builder.Write(e.Body.U.i64, s);
                    break;
                case ExpressionBody.Types.i32:
                    Builder.Write(e.Body.U.i32, s);
                    break;
                case ExpressionBody.Types.f64:
                    (s ?? Builder.DoubleStream).Write(e.Body.U.f64);
                    break;
                case ExpressionBody.Types.f32:
                    (s ?? Builder.SingleStream).Write(e.Body.U.f32);
                    break;
                case ExpressionBody.Types.memory:
                    Builder.Write(e.Body.U.memory.alignment_exponent, MemoryAlignments);
                    Builder.Write(e.Body.U.memory.offset, MemoryOffsets);
                    break;
                case ExpressionBody.Types.type:
                    (s ?? BlockTypes).Write((byte)e.Body.U.type);
                    break;
                case ExpressionBody.Types.br_table:
                    var bt = e.Body.br_table;
                    Builder.Write((uint)bt.target_table.Length, BrTables);
                    foreach (var t in bt.target_table)
                        Builder.Write(t, BrTables);
                    Builder.Write(bt.default_target, BrTables);

                    break;

                default:
                    throw new Exception("Not implemented");
            }

            if ((e.Body.Type & ExpressionBody.Types.children) != 0) {
                // HACK: The children were already pushed into the queue, so ignore them
                /*
                for (int i = 0, l = e.Body.children.Count; i < l; i++)
                    Write(e.Body.children[i]);
                */
            }
        }

        public void Write (ref Expression e) {
            FlushQueue(WriteQueue, ref WriteQueueLength, false);
            WriteQueue[WriteQueueLength++] = e;

            if ((e.Body.Type & ExpressionBody.Types.children) != 0) {
                // Insert the children directly into the queue so they can be processed by the
                //  peephole optimizer.
                for (int i = 0, l = e.Body.children.Count; i < l; i++)
                    Write(e.Body.children[i]);
            }
        }

        public void Write (Expression e) {
            Write(ref e);
        }

        private void PeepholeOptimize (ref Expression previous, ref Expression current, ref Expression next) {
            if (current.Opcode == Opcodes.end)
                return;

            switch (previous.Opcode) {
                case Opcodes.set_local: {
                    if (
                        (current.Opcode == Opcodes.get_local) &&
                        (current.Body.U.u32 == previous.Body.U.u32)
                    ) {
                        current = new Expression {
                            Opcode = ExpressionEncoder.FakeOpcodes.read_prior_local,
                            Body = {
                                Type = ExpressionBody.Types.none
                            }
                        };
                        return;
                    }
                    break;
                }
                // We could generate dup for set/get global pairs but they don't seem to actually show up
                /* you'd think this would help, but brotli is smarter
                case Opcodes.get_local: {
                    if (
                        (
                            (e.Opcode == Opcodes.i32_load) ||
                            (e.Opcode == Opcodes.i32_store)
                        ) &&
                        (e.Body.U.memory.alignment_exponent == 2)
                    ) {
                        Write(new Expression {
                            Opcode = (e.Opcode == Opcodes.i32_load) 
                                ? FakeOpcodes.i32_load_relative
                                : FakeOpcodes.i32_store_relative,
                            Body = {
                                Type = ExpressionBody.Types.u32,
                                U = {
                                    u32 = e.Body.U.memory.offset
                                }
                            }
                        });
                        return true;
                    }
                    break;
                }
                */
            }

            var naturalOps = new Dictionary<Opcodes, Opcodes> {
                { Opcodes.i32_load,  FakeOpcodes.i32_load_natural },
                { Opcodes.i32_store, FakeOpcodes.i32_store_natural }
            };

            var constants = new Dictionary<int, Opcodes> {
                { 0, FakeOpcodes.ldc_i32_zero },
                { 1, FakeOpcodes.ldc_i32_one },
                { -1, FakeOpcodes.ldc_i32_minus_one }
            };

            switch (current.Opcode) {
                case Opcodes.i32_load:
                case Opcodes.i32_store:
                    if (current.Body.U.memory.alignment_exponent == 2) {
                        current = new Expression {
                            Opcode = naturalOps[current.Opcode],
                            Body = {
                                Type = ExpressionBody.Types.u32,
                                U = {
                                    u32 = current.Body.U.memory.offset
                                }
                            }
                        };
                        return;
                    }
                    break;
                // /* surprisingly, this is worse! (not by much, though)
                case Opcodes.i32_const:
                    Opcodes newOpcode;
                    if (
                        constants.TryGetValue(current.Body.U.i32, out newOpcode)
                    ) {
                        current = new Expression {
                            Opcode = newOpcode,
                            Body = default(ExpressionBody)
                        };
                        return;
                    }
                    break;
                // */
            }
        }

        private void FlushQueue (Expression[] queue, ref int queue_length, bool force) {
            if (queue_length == 0)
                return;

            if (queue_length > 3)
                throw new Exception();

            if ((queue_length == 3) || force) {
                // HACK: queue[3] is always default(Expression) to give us an easy way
                //  to indirectly reference it
                PeepholeOptimize(ref queue[3], ref queue[0], ref queue[1]);
                WriteInternal(ref queue[0]);

                for (int i = 1; i < queue_length; i++) {
                    PeepholeOptimize(ref queue[i - 1], ref queue[i], ref queue[i + 1]);

                    // FIXME: Is this valid?
                    if (queue[i].Opcode == Opcodes.nop)
                        continue;

                    WriteInternal(ref queue[i]);
                }

                queue_length = 0;

                for (int i = 0; i < queue.Length; i++)
                    queue[i] = default(Expression);
            }
        }

        internal void Flush () {
            FlushQueue(WriteQueue, ref WriteQueueLength, true);
        }
    }
}
