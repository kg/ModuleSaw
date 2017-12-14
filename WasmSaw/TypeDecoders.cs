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
        public readonly ExpressionDecoder Expression;

        public TypeDecoders (AbstractModuleReader reader) {
            Reader = reader;
            Expression = new ExpressionDecoder(this);
        }

        public AbstractModuleStreamReader GetStream (string key) {
            return Reader.Open(Reader.Streams[key]);
        }

        public func_type func_type () {
            var types = GetStream("types");

            return new func_type {
                // form = GetStream("func_type.form").ReadByte(),
                param_types = Reader.ReadArray(() => (LanguageTypes)types.ReadByte()),
                return_type = (LanguageTypes)types.ReadByte()
            };
        }

        public import_entry import_entry () {
            var modules = GetStream("module");
            var fields = GetStream("field");
            var kinds = GetStream("external_kind");

            var result = new import_entry {
                module = Reader.ReadString(modules),
                field = Reader.ReadString(fields),
                kind = (external_kind)kinds.ReadByte()
            };

            switch (result.kind) {
                case external_kind.Function:
                    result.type.Function = GetStream("function_index").ReadUInt32();
                    return result;

                case external_kind.Table:
                    result.type.Table = table_type();
                    return result;

                case external_kind.Memory:
                    result.type.Memory = memory_type();
                    return result;

                case external_kind.Global:
                    result.type.Global = global_type();
                    return result;

                default:
                    throw new Exception("unknown import kind");
            }
        }

        public resizable_limits resizable_limits () {
            var flags = GetStream("rl_flag");
            var initials = GetStream("rl_initial");
            var maximums = GetStream("rl_maximum");

            return new resizable_limits {
                flags = flags.ReadByte(),
                initial = initials.ReadUInt32(),
                maximum = maximums.ReadUInt32()
            };
        }

        public table_type table_type () {
            var elementTypes = GetStream("element_type");
            return new table_type {
                element_type = (LanguageTypes)elementTypes.ReadByte(),
                limits = resizable_limits()
            };
        }

        public global_type global_type () {
            var contentTypes = GetStream("content_type");
            var mutabilities = GetStream("mutability");
            return new global_type {
                mutability = mutabilities.ReadBoolean(),
                content_type = (LanguageTypes)contentTypes.ReadByte()
            };
        }

        public memory_type memory_type () {
            return new memory_type {
                limits = resizable_limits()
            };
        }

        public global_variable global_variable () {
            var result = new global_variable {
                type = global_type()
            };
            if (Expression.Decode(out result.init))
                return result;
            else
                throw new Exception("Decode failed for global variable init " + result.init.Opcode);
        }

        public export_entry export_entry () {
            var fields = GetStream("field");
            var kinds = GetStream("external_kind");
            var indices = GetStream("table_index");

            return new export_entry {
                field = Reader.ReadString(fields),
                kind = (external_kind)kinds.ReadByte(),
                index = indices.ReadUInt32()
            };            
        }

        public elem_segment elem_segment () {
            var indices = GetStream("table_index");

            var result = new elem_segment {
                index = indices.ReadUInt32(),
            };

            if (!Expression.Decode(out result.offset))
                throw new Exception("Decode failed for elem_segment " + result.offset.Opcode);

            var count = Reader.ReadArrayLength();
            result.elems = new uint[count];

            for (uint i = 0; i < count; i++)
                result.elems[i] = indices.ReadUInt32();

            return result;
        }

        public function_body function_body () {
            var locals = GetStream("local_entry");

            var result = new function_body {
                body_size = Reader.UIntStream.ReadUInt32()
            };

            var count = Reader.ReadArrayLength();
            result.locals = new local_entry[count];
            for (uint i = 0; i < count; i++) {
                result.locals[i] = new local_entry {
                    count = locals.ReadUInt32(),
                    type = (LanguageTypes)locals.ReadByte()
                };
            }
            return result;
        }

        public data_segment data_segment () {
            var result = new data_segment {
                index = Reader.UIntStream.ReadUInt32(),
                size = Reader.UIntStream.ReadUInt32()
            };
            
            if (!Expression.Decode(out result.offset))
                throw new Exception("Decode failed for data_segment " + result.offset.Opcode);

            return result;
        }
    }
}
