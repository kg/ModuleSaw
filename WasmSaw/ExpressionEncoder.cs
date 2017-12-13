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

        private KeyedStream Opcodes;

        public ExpressionEncoder (AbstractModuleBuilder builder) {
            Builder = builder;

            Opcodes = builder.GetStream("opcodes");
        }
        
        public void Write (
            ref Expression e
        ) {
            Opcodes.Write((byte)e.Opcode);

            KeyedStream s;

            switch (e.Body.Type & ~ExpressionBody.Types.children) {
                case ExpressionBody.Types.none:
                    break;

                case ExpressionBody.Types.u64:
                    Builder.Write(e.Body.U.u64);
                    break;
                case ExpressionBody.Types.u32:
                    Builder.Write(e.Body.U.u32);
                    break;
                case ExpressionBody.Types.i64:
                    Builder.Write(e.Body.U.i64);
                    break;
                case ExpressionBody.Types.i32:
                    Builder.Write(e.Body.U.i32);
                    break;
                case ExpressionBody.Types.f64:
                    Builder.DoubleStream.Write(e.Body.U.f64);
                    break;
                case ExpressionBody.Types.f32:
                    Builder.SingleStream.Write(e.Body.U.f32);
                    break;
                case ExpressionBody.Types.memory:
                    s = Builder.GetStream("memory_immediate");
                    Builder.Write(e.Body.U.memory.flags, s);
                    Builder.Write(e.Body.U.memory.offset, s);
                    break;
                case ExpressionBody.Types.type:
                    Builder.GetStream("expression_type").Write((sbyte)e.Body.U.type);
                    break;
                case ExpressionBody.Types.br_table:
                    s = Builder.GetStream("br_table");

                    var bt = e.Body.br_table;
                    Builder.Write((uint)bt.target_table.Length, s);
                    foreach (var t in bt.target_table)
                        Builder.Write(t, s);
                    Builder.Write(bt.default_target, s);

                    break;

                default:
                    throw new Exception("Not implemented");
            }

            if ((e.Body.Type & ExpressionBody.Types.children) != 0) {
                var exprs = e.Body.children;
                s = Builder.GetStream("block_size");
                if (exprs != null) {
                    Builder.Write((uint)exprs.Count, s);
                    foreach (var expr in exprs)
                        Write(expr);
                } else {
                    Builder.Write((uint)0, s);
                }
            }
            // FIXME
        }

        public void Write (Expression e) {
            Write(ref e);
        }
    }
}
