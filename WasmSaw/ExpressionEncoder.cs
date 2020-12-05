using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ModuleSaw;
using Wasm.Model;

namespace WasmSaw {
    public class ExpressionEncoder {
        public const bool EnableOptimizer = true;

        public readonly AbstractModuleBuilder Builder;

        private KeyedStreamWriter OpcodeStream, GlobalIndices, LocalIndices,
            MemoryAlignments, MemoryOffsets, BrTables, BlockTypes,
            FunctionIndices, TypeIndices, BreakDepths;

        public int NumWritten = 0;

        // [prev, current, next]
        private Expression[] WriteQueue = new Expression[3];
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
            }

            return null;
        }

        public static class FakeOpcodes {
            // NOTE: The spec reserves '0xFC ...' for the trunc_sat prefix
            public const byte FirstFakeOpcode = 0xD0;

            public const Opcodes 
                                 // set_local x, get_local x pairs are common (~4400 in dotnet.wasm)
                                 //  so replacing the 2nd get_local with a special 'dup' opcode
                                 //  allows us to skip encoding the local index again, and save some
                                 //  space. converting the pair into a single tee_local instruction
                                 //  is *not* better than this, for reasons that are not obvious to me
                                 //  the semantics of set+get are different from tee in a threading context
                                 //  anyway.
                                 read_prior_local = (Opcodes)(FirstFakeOpcode + 0),
                                 // 'natural' i32 load/store save around 500b,
                                 //  and without the relative exponent encoding for memory
                                 //  alignment they save considerably more
                                 i32_load_natural = (Opcodes)(FirstFakeOpcode + 1),
                                 i32_store_natural = (Opcodes)(FirstFakeOpcode + 2),
                                 // 0 and 1 save a considerable amount of space,
                                 // -1 and 2 help a bit less but still help
                                 ldc_i32_zero = (Opcodes)(FirstFakeOpcode + 3),
                                 ldc_i32_one = (Opcodes)(FirstFakeOpcode + 4),
                                 ldc_i32_minus_one = (Opcodes)(FirstFakeOpcode + 5),
                                 ldc_i32_two = (Opcodes)(FirstFakeOpcode + 6);

            public static readonly Dictionary<int, Opcodes> Constants = 
                new Dictionary<int, Opcodes> {
                    { 0, ldc_i32_zero },
                    { 1, ldc_i32_one },
                    { 2, ldc_i32_two },
                    { -1, ldc_i32_minus_one }
                };

            public static readonly Dictionary<Opcodes, int> ReverseConstants = 
                new Dictionary<Opcodes, int> {
                    { ldc_i32_zero, 0 },
                    { ldc_i32_one, 1 },
                    { ldc_i32_two, 2 },
                    { ldc_i32_minus_one, -1 }
                };

            public static readonly Dictionary<Opcodes, Opcodes> NaturalOps =
                new Dictionary<Opcodes, Opcodes> {
                    { Opcodes.i32_load,  FakeOpcodes.i32_load_natural },
                    { Opcodes.i32_store, FakeOpcodes.i32_store_natural }
                };
            /*
             * Graveyard of abandoned and failed experiments:
             * 'load/store relative' opcodes for the pattern | a[b+c] |
             * ldc.i32.pot for constants that are powers of two
             * 'unreachable call' opcode for the pattern | call x; unreachable; end |
             * 'multi-get'/'multi-set' opcodes i.e. | (a, b, c) = (mem[0], mem[4], mem[8]) |
             * 'get-and-store' opcode for the pattern | mem[x] = local |
             */
        }

        private void WriteInternal (
            ref Expression e
        ) {
            Console.WriteLine("> write {0}", e);
            OpcodeStream.Write((byte)e.Opcode);
            NumWritten++;

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
                    Builder.Write(e.Body.U.memory.EXT_relative_alignment_exponent, MemoryAlignments);
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
        }

        public void WriteBuffered (ref Expression e) {
            Enqueue(WriteQueue, ref WriteQueueLength, ref e);

            if ((e.Body.Type & ExpressionBody.Types.children) != 0)
                EnqueueChildren(e.Body.children);
        }

        private struct WriteState {
            public List<Expression> list;
            public int offset, count;
        }

        private void EnqueueChildren (List<Expression> children) {
            if (children.Count <= 0)
                return;

            Stack<WriteState> stack = null;
            var state = new WriteState { list = children, offset = 0, count = children.Count };

            while ((state.offset < state.count) || ((stack != null) && (stack.Count > 0))) {
                if (state.offset >= state.count) {
                    if ((stack == null) || (stack.Count == 0))
                        return;

                    state = stack.Pop();
                    continue;
                }

                var child = state.list[state.offset++];
                Enqueue(WriteQueue, ref WriteQueueLength, ref child);

                var subChildren = child.Body.children;
                if ((subChildren != null) && (subChildren.Count > 0)) {
                    if (stack == null)
                        stack = new Stack<WriteState>();
                    stack.Push(state);
                    state = new WriteState { list = subChildren, count = subChildren.Count, offset = 0 };
                }
            }

            return;
        }

        private void Enqueue (Expression[] queue, ref int queueLength, ref Expression e) {
            FlushQueue(queue, ref queueLength, false);

            if (queueLength >= 3)
                throw new ArgumentOutOfRangeException("queueLength");

            queue[queueLength++] = e;
            FlushQueue(queue, ref queueLength, false);
        }

        public void Write (Expression e) {
            WriteUnbuffered(ref e);
        }

        public void WriteUnbuffered (ref Expression e) {
            WriteBuffered(ref e);
            Flush();
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

            switch (current.Opcode) {
                case Opcodes.i32_load:
                case Opcodes.i32_store:
                    if (current.Body.U.memory.alignment_exponent == 2) {
                        current = new Expression {
                            Opcode = FakeOpcodes.NaturalOps[current.Opcode],
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
                case Opcodes.i32_const:
                    Opcodes newOpcode;
                    if (
                        FakeOpcodes.Constants.TryGetValue(current.Body.U.i32, out newOpcode)
                    ) {
                        current = new Expression {
                            Opcode = newOpcode,
                            Body = default(ExpressionBody)
                        };
                        return;
                    }
                    break;
            }
        }

        private void FlushQueue (Expression[] queue, ref int queueLength, bool force) {
            if (queueLength > 3)
                throw new Exception();

            var minimum = force ? 1 : 3;

            while (queueLength >= minimum) {
                if (EnableOptimizer)
                    PeepholeOptimize(ref queue[0], ref queue[1], ref queue[2]);

                WriteInternal(ref queue[0]);
                queue[0] = queue[1];
                queue[1] = queue[2];
                queue[2] = default(Expression);

                queueLength--;
            }
        }

        internal void Flush () {
            FlushQueue(WriteQueue, ref WriteQueueLength, true);
            for (int i = 0; i < WriteQueue.Length; i++)
                WriteQueue[i] = default(Expression);
        }
    }
}
