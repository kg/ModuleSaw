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

        public ArrayBinaryReader GetStream (string key) {
            return Reader.Streams.Open(key);
        }

        private void Check (bool b) {
            if (!b)
                throw new System.IO.EndOfStreamException();
        }

        public func_type func_type () {
            var types = GetStream("types");

            var result = new func_type {
                // form = GetStream("func_type.form").ReadByte(),
                param_types = Reader.ReadArray(() => {
                    types.Read(out LanguageTypes t);
                    return t;
                }),
            };
            Check(types.Read(out result.return_type));
            return result;
        }

        public import_entry import_entry () {
            var modules = GetStream("module");
            var fields = GetStream("field");
            var kinds = GetStream("external_kind");

            var result = new import_entry {
                module = Reader.ReadString(modules),
                field = Reader.ReadString(fields),
            };
            Check(kinds.Read(out result.kind));

            switch (result.kind) {
                case external_kind.Function:
                    Check(
                        GetStream("function_index").ReadU32LEB(out result.type.Function)
                    );
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
            var s = GetStream("resizable_limits");

            var result = new resizable_limits();
            Check(s.Read(out result.flags));
            Check(s.ReadU32LEB(out result.initial));
            Check(s.ReadU32LEB(out result.maximum));
            return result;
        }

        public table_type table_type () {
            var elementTypes = GetStream("element_type");
            var result = new table_type();
            Check(elementTypes.Read(out result.element_type));
            result.limits = resizable_limits();
            return result;
        }

        public global_type global_type () {
            var s = GetStream("global_types");
            var result = new global_type();
            Check(s.Read(out result.mutability));
            Check(s.Read(out result.content_type));
            return result;
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

            var result = new export_entry {
                field = Reader.ReadString(fields)
            };
            Check(kinds.Read(out result.kind));
            Check(indices.ReadU32LEB(out result.index));
            return result;
        }

        public elem_segment elem_segment () {
            var indices = GetStream("table_index");

            var result = new elem_segment();
            Check(indices.ReadU32LEB(out result.index));

            if (!Expression.Decode(out result.offset))
                throw new Exception("Decode failed for elem_segment " + result.offset.Opcode);

            var count = Reader.ReadArrayLength();
            result.elems = new uint[count];

            for (uint i = 0; i < count; i++)
                Check(indices.ReadU32LEB(out result.elems[i]));

            return result;
        }

        public function_body function_body () {
            var locals = GetStream("local_entry");

            var result = new function_body();
            Check(Reader.UIntStream.ReadU32LEB(out result.body_size));

            var count = Reader.ReadArrayLength();
            result.locals = new local_entry[count];
            for (uint i = 0; i < count; i++) {
                Check(locals.ReadU32LEB(out result.locals[i].count));
                Check(locals.Read(out result.locals[i].type));
            }
            return result;
        }

        public data_segment data_segment () {
            var result = new data_segment();
            Check(Reader.UIntStream.ReadU32LEB(out result.index));
            Check(Reader.UIntStream.ReadU32LEB(out result.size));
            
            if (!Expression.Decode(out result.offset)) {
                Console.WriteLine(
                    // throw new Exception(
                    "Decode failed for data_segment offset: opcode was " + result.offset.Opcode
                );
            }

            return result;
        }
    }
}
