using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ModuleSaw;
using Wasm.Model;

namespace WasmSaw {
    public class ModuleEncoder {
        public readonly AbstractModuleBuilder Builder;
        public readonly ExpressionEncoder ExpressionEncoder;

        public readonly TypeEncoders Types;

        public ModuleEncoder (AbstractModuleBuilder builder) {
            Builder = builder;
            ExpressionEncoder = new ExpressionEncoder(builder);

            Types = new TypeEncoders(this);
        }
    }
}
