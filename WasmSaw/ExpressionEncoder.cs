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
            }

            return null;
        }

        public static class FakeOpcodes {
            public const byte FirstFakeOpcode = 0xF0;
            public const Opcodes dup = (Opcodes)(FirstFakeOpcode + 0);
            /*
            public const Opcodes i32_load_relative = (Opcodes)(FirstFakeOpcode + 1);
            public const Opcodes i32_store_relative = (Opcodes)(FirstFakeOpcode + 2);
            */
        }

        // Return true to suppress emitting this opcode (because we emitted other ones)
        private bool PeepholeOptimize (ref Expression e) {
            switch (PreviousExpression.Opcode) {
                case Opcodes.set_local: {
                    if (
                        (e.Opcode == Opcodes.get_local) &&
                        (e.Body.U.u32 == PreviousExpression.Body.U.u32)
                    ) {
                        Write(new Expression {
                            Opcode = FakeOpcodes.dup,
                            Body = {
                                Type = ExpressionBody.Types.none
                            }
                        });
                        return true;
                    }
                    break;
                }
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

            return false;
        }

        private Expression PreviousExpression;

        public void Write (
            ref Expression e
        ) {
            if (PeepholeOptimize(ref e))
                return;

            PreviousExpression = e;
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
                for (int i = 0, l = e.Body.children.Count; i < l; i++)
                    Write(e.Body.children[i]);
            }
        }

        public void Write (Expression e) {
            Write(ref e);
        }
    }
}
