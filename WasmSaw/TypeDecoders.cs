using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ModuleSaw;
using Wasm.Model;

namespace WasmSaw {
    public class TypeDecoders {
        public readonly AbstractModuleReader Reader;
        private readonly Dictionary<string, AbstractModuleStreamReader> StreamCache =
            new Dictionary<string, AbstractModuleStreamReader>(StringComparer.Ordinal);

        public TypeDecoders (AbstractModuleReader reader) {
            Reader = reader;
        }

        private AbstractModuleStreamReader GetStream (string key) {
            AbstractModuleStreamReader result;
            if (!StreamCache.TryGetValue(key, out result))
                StreamCache[key] = result = Reader.Open(Reader.Streams[key]);

            return result;
        }

        public func_type func_type () {
            var types = GetStream("types");

            return new func_type {
                // form = GetStream("func_type.form").ReadByte(),
                param_types = Reader.ReadArray(() => (LanguageTypes)types.ReadByte()),
                return_type = (LanguageTypes)types.ReadByte()
            };
        }
    }
}
