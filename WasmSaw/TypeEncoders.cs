using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ModuleSaw;
using Wasm.Model;

namespace WasmSaw {
    public abstract class TypeEncoder<T> {
        public readonly ModuleEncoder ModuleEncoder;

        public TypeEncoder (ModuleEncoder moduleEncoder) {
            ModuleEncoder = moduleEncoder;
        }

        public AbstractModuleBuilder Builder {
            get => ModuleEncoder.Builder;
        }

        public void Encode (T[] array) {
            Builder.WriteArrayLength(array);
            for (int i = 0; i < array.Length; i++)
                Encode(ref array[i]);
        }

        public abstract void Encode (ref T value);

        public void Encode (T value) {
            Encode(ref value);
        }

        protected KeyedStream GetStream (string name) {
            return ModuleEncoder.Builder.GetStream(name);
        }

        protected void Write (ref Expression e) {
            ModuleEncoder.ExpressionEncoder.Write(ref e);
        }
    }

    public class TypeEncoders {
        public readonly SectionHeaderEncoder SectionHeader;
        public readonly func_typeEncoder func_type;
        public readonly import_entryEncoder import_entry;
        public readonly global_variableEncoder global_variable;
        public readonly export_entryEncoder export_entry;
        public readonly elem_segmentEncoder elem_segment;
        public readonly function_bodyEncoder function_body;
        public readonly data_segmentEncoder data_segment;

        public TypeEncoders (ModuleEncoder moduleEncoder) {
            SectionHeader = new SectionHeaderEncoder(moduleEncoder);
            func_type = new func_typeEncoder(moduleEncoder);
            import_entry = new import_entryEncoder(moduleEncoder);
            global_variable = new global_variableEncoder(moduleEncoder);
            export_entry = new export_entryEncoder(moduleEncoder);
            elem_segment = new elem_segmentEncoder(moduleEncoder);
            function_body = new function_bodyEncoder(moduleEncoder);
            data_segment = new data_segmentEncoder(moduleEncoder);
        }

        public class SectionHeaderEncoder : TypeEncoder<SectionHeader> {
            KeyedStream payload_lens, ids;

            public SectionHeaderEncoder (ModuleEncoder moduleEncoder) : base(moduleEncoder) {
                ids = GetStream("section.id");
                payload_lens = GetStream("section.payload_len");
            }

            public override void Encode (ref SectionHeader value) {
                ids.Write((byte)value.id);
                Builder.Write(value.name);
                Builder.Write(value.payload_len, payload_lens);
            }
        }

        public class func_typeEncoder : TypeEncoder<func_type> {
            KeyedStream forms, types;

            public func_typeEncoder (ModuleEncoder moduleEncoder) : base (moduleEncoder) {
                forms = GetStream("func_type.form");
                types = GetStream("types");
            }

            public override void Encode (ref func_type value) {
                forms.Write(value.form);
                Builder.WriteArrayLength(value.param_types);
                foreach (var pt in value.param_types)
                    types.Write((sbyte)pt);
                types.Write((sbyte)value.return_type);
            }
        }

        public class import_entryEncoder : TypeEncoder<import_entry> {
            KeyedStream modules, fields, kinds;

            public import_entryEncoder (ModuleEncoder moduleEncoder) : base(moduleEncoder) {
                modules = GetStream("module");
                fields = GetStream("field");
                kinds = GetStream("external_kind");
            }

            public override void Encode (ref import_entry value) {
                Builder.Write(value.module, modules);
                Builder.Write(value.field, fields);
                kinds.Write((byte)value.kind);
            }
        }

        public class global_variableEncoder : TypeEncoder<global_variable> {
            KeyedStream types;

            public global_variableEncoder (ModuleEncoder moduleEncoder) : base(moduleEncoder) {
                types = GetStream("global_variable.type");
            }

            public override void Encode (ref global_variable value) {
                types.Write((byte)value.type.content_type);
                types.Write(value.type.mutability);
                Write(ref value.init);
            }
        }

        public class export_entryEncoder : TypeEncoder<export_entry> {
            KeyedStream fields, kinds, indices;

            public export_entryEncoder (ModuleEncoder moduleEncoder) : base(moduleEncoder) {
                fields = GetStream("field");
                kinds = GetStream("external_kind");
                indices = GetStream("table_index");
            }

            public override void Encode (ref export_entry value) {
                Builder.Write(value.field, fields);
                kinds.Write((byte)value.kind);
                Builder.Write(value.index, indices);
            }
        }

        public class elem_segmentEncoder : TypeEncoder<elem_segment> {
            KeyedStream indices;

            public elem_segmentEncoder (ModuleEncoder moduleEncoder) : base(moduleEncoder) {
                indices = GetStream("table_index");
            }

            public override void Encode (ref elem_segment value) {
                Builder.Write(value.index, indices);
                Write(ref value.offset);
                Builder.WriteArrayLength(value.elems);
                foreach (var elem in value.elems)
                    Builder.Write(elem, indices);
            }
        }

        public class function_bodyEncoder : TypeEncoder<function_body> {
            private KeyedStream locals;

            public function_bodyEncoder (ModuleEncoder moduleEncoder) : base(moduleEncoder) {
                locals = GetStream("local_entry");
            }

            public override void Encode (ref function_body value) {
                Builder.WriteArrayLength(value.locals);
                foreach (var le in value.locals) {
                    Builder.Write(le.count, locals);
                    locals.Write((sbyte)le.type);
                }

                // FIXME
                Builder.Write(value.code_size);
            }
        }

        public class data_segmentEncoder : TypeEncoder<data_segment> {
            public data_segmentEncoder (ModuleEncoder moduleEncoder) : base(moduleEncoder) {
            }

            public override void Encode (ref data_segment value) {
                Builder.Write(value.index);
                Write(ref value.offset);

                // FIXME
                Builder.Write(value.size);
            }
        }
    }
}
