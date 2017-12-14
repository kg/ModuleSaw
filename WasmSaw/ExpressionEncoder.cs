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

        private KeyedStream OpcodeStream, GlobalIndices, LocalIndices,
            MemoryImmediates, BrTables, BlockTypes,
            FunctionIndices, TypeIndices, BlockSizes;

        public ExpressionEncoder (AbstractModuleBuilder builder) {
            Builder = builder;

            OpcodeStream = builder.GetStream("opcode");
            GlobalIndices = builder.GetStream("global_index");
            LocalIndices = builder.GetStream("local_index");
            FunctionIndices = builder.GetStream("function_index");
            TypeIndices = builder.GetStream("type_index");
            MemoryImmediates = builder.GetStream("memory_immediate");
            BrTables = builder.GetStream("br_table");
            BlockTypes = builder.GetStream("block_type");
            BlockSizes = builder.GetStream("block_size");
        }

        private KeyedStream GetStreamForOpcode (Opcodes opcode) {
            switch (opcode) {
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
        
        public void Write (
            ref Expression e
        ) {
            OpcodeStream.Write((byte)e.Opcode);

            KeyedStream s = GetStreamForOpcode(e.Opcode);

            switch (e.Body.Type & ~ExpressionBody.Types.children) {
                case ExpressionBody.Types.none:
                    break;

                case ExpressionBody.Types.u64:
                    Builder.Write(e.Body.U.u64, s);
                    break;
                case ExpressionBody.Types.u32:
                    Builder.Write(e.Body.U.u32, s);
                    break;
                case ExpressionBody.Types.u1:
                    Builder.Write((byte)e.Body.U.u32, s);
                    break;
                case ExpressionBody.Types.i64:
                    Builder.Write(e.Body.U.i64, s);
                    break;
                case ExpressionBody.Types.i32:
                    Builder.Write(e.Body.U.i32, s);
                    break;
                case ExpressionBody.Types.f64:
                    Builder.DoubleStream.Write(e.Body.U.f64);
                    break;
                case ExpressionBody.Types.f32:
                    Builder.SingleStream.Write(e.Body.U.f32);
                    break;
                case ExpressionBody.Types.memory:
                    if (s == null)
                        s = MemoryImmediates;
                    Builder.Write(e.Body.U.memory.flags, s);
                    Builder.Write(e.Body.U.memory.offset, s);
                    break;
                case ExpressionBody.Types.type:
                    BlockTypes.Write((byte)e.Body.U.type);
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
                var exprs = e.Body.children;
                if (exprs != null) {
                    Builder.Write((uint)exprs.Count, BlockSizes);
                    foreach (var expr in exprs)
                        Write(expr);
                } else {
                    Builder.Write((uint)0, BlockSizes);
                }
            }
            // FIXME
        }

        public void Write (Expression e) {
            Write(ref e);
        }
    }
}
