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
            if (e.Body.children != null) {
                var exprs = e.Body.children;
                Builder.WriteArrayLength(exprs);
                for (var i = 0; i < exprs.Length; i++)
                    Write(ref exprs[i]);
            } else
                ;
            // FIXME
        }
    }
}
