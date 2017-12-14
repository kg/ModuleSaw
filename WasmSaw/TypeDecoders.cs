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
            return new func_type {
                form = GetStream("func_type.form").ReadSByte(),
            };
            var types = GetStream("types");
        }
    }
}
