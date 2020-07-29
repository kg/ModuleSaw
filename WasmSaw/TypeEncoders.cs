using System;
using System.Collections.Generic;
using System.IO;
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

        public TypeEncoders Types {
            get => ModuleEncoder.Types;
        }

        public AbstractModuleBuilder Builder {
            get => ModuleEncoder.Builder;
        }

        public void Encode (T[] array, int splitInterval = int.MaxValue) {
            Builder.WriteArrayLength(array);
            for (int i = 0, j = 0; i < array.Length; i++, j++) {
                Encode(ref array[i]);

                if (j == splitInterval) {
                    j = 0;
                    Builder.SplitSegments(WasmToMsaw.MinimumSegmentSize);
                }
            }
        }

        public abstract void Encode (ref T value);

        public void Encode (T value) {
            Encode(ref value);
        }

        protected KeyedStreamWriter GetStream (string name) {
            return ModuleEncoder.Builder.GetStream(name);
        }

        protected void Write (ref Expression e) {
            ModuleEncoder.ExpressionEncoder.Write(ref e);
        }
    }

    public class TypeEncoders {
        public readonly SectionHeaderEncoder SectionHeader;
        public readonly func_typeEncoder func_type;
        public readonly resizable_limitsEncoder resizable_limits;
        public readonly table_typeEncoder table_type;
        public readonly global_typeEncoder global_type;
        public readonly memory_typeEncoder memory_type;
        public readonly import_entryEncoder import_entry;
        public readonly global_variableEncoder global_variable;
        public readonly export_entryEncoder export_entry;
        public readonly elem_segmentEncoder elem_segment;
        public readonly function_bodyEncoder function_body;
        public readonly data_segmentEncoder data_segment;

        public TypeEncoders (ModuleEncoder moduleEncoder) {
            SectionHeader = new SectionHeaderEncoder(moduleEncoder);
            func_type = new func_typeEncoder(moduleEncoder);
            resizable_limits = new resizable_limitsEncoder(moduleEncoder);
            table_type = new table_typeEncoder(moduleEncoder);
            global_type = new global_typeEncoder(moduleEncoder);
            memory_type = new memory_typeEncoder(moduleEncoder);
            import_entry = new import_entryEncoder(moduleEncoder);
            global_variable = new global_variableEncoder(moduleEncoder);
            export_entry = new export_entryEncoder(moduleEncoder);
            elem_segment = new elem_segmentEncoder(moduleEncoder);
            function_body = new function_bodyEncoder(moduleEncoder);
            data_segment = new data_segmentEncoder(moduleEncoder);
        }

        public class SectionHeaderEncoder : TypeEncoder<SectionHeader> {
            KeyedStreamWriter payload_lens, ids, names;

            public SectionHeaderEncoder (ModuleEncoder moduleEncoder) : base(moduleEncoder) {
                ids = GetStream("section_id");
                names = GetStream("section_name");
                payload_lens = GetStream("section_payload_len");
            }

            public override void Encode (ref SectionHeader value) {
                ids.Write((sbyte)value.id);
                if (value.id == 0)
                    names.Write(value.name);
                Builder.Write(value.payload_len, payload_lens);
            }
        }

        public class func_typeEncoder : TypeEncoder<func_type> {
            KeyedStreamWriter types;

            public func_typeEncoder (ModuleEncoder moduleEncoder) : base (moduleEncoder) {
                types = GetStream("types");
            }

            public override void Encode (ref func_type value) {
                Builder.WriteArrayLength(value.param_types);
                foreach (var pt in value.param_types)
                    types.Write((byte)pt);
                types.Write((byte)value.return_type);
            }
        }

        public class resizable_limitsEncoder : TypeEncoder<resizable_limits> {
            KeyedStreamWriter flags, initials, maximums;

            public resizable_limitsEncoder (ModuleEncoder moduleEncoder) : base (moduleEncoder) {
                flags = GetStream("rl_flag");
                initials = GetStream("rl_initial");
                maximums = GetStream("rl_maximum");
            }

            public override void Encode (ref resizable_limits value) {
                flags.Write(value.flags);
                Builder.Write(value.initial, initials);
                // if (value.flags == 1)
                Builder.Write(value.maximum, maximums);
            }
        }

        public class table_typeEncoder : TypeEncoder<table_type> {
            KeyedStreamWriter types;

            public table_typeEncoder (ModuleEncoder moduleEncoder) : base (moduleEncoder) {
                types = GetStream("element_type");
            }

            public override void Encode (ref table_type value) {
                types.Write((byte)value.element_type);

                Types.resizable_limits.Encode(ref value.limits);
            }
        }

        public class global_typeEncoder : TypeEncoder<global_type> {
            KeyedStreamWriter contentTypes, mutabilities;

            public global_typeEncoder (ModuleEncoder moduleEncoder) : base (moduleEncoder) {
                contentTypes = GetStream("content_type");
                mutabilities = GetStream("mutability");
            }

            public override void Encode (ref global_type value) {
                mutabilities.Write(value.mutability);
                contentTypes.Write((byte)value.content_type);
            }
        }

        public class memory_typeEncoder : TypeEncoder<memory_type> {
            KeyedStreamWriter types;

            public memory_typeEncoder (ModuleEncoder moduleEncoder) : base (moduleEncoder) {
            }

            public override void Encode (ref memory_type value) {
                Types.resizable_limits.Encode(ref value.limits);
            }
        }

        public class import_entryEncoder : TypeEncoder<import_entry> {
            KeyedStreamWriter modules, fields, kinds, functionIndices;

            public import_entryEncoder (ModuleEncoder moduleEncoder) : base(moduleEncoder) {
                modules = GetStream("module");
                fields = GetStream("field");
                kinds = GetStream("external_kind");
                functionIndices = GetStream("function_index");
            }

            public override void Encode (ref import_entry value) {
                Builder.Write(value.module, modules);
                Builder.Write(value.field, fields);
                kinds.Write((byte)value.kind);

                switch (value.kind) {
                    case external_kind.Function:
                        Builder.Write(value.type.Function, functionIndices);
                        return;
                    case external_kind.Table:
                        Types.table_type.Encode(ref value.type.Table);
                        break;
                    case external_kind.Memory:
                        Types.memory_type.Encode(ref value.type.Memory);
                        break;
                    case external_kind.Global:
                        Types.global_type.Encode(ref value.type.Global);
                        break;
                    default:
                        throw new Exception("unknown import kind");
                }
            }
        }

        public class global_variableEncoder : TypeEncoder<global_variable> {
            KeyedStreamWriter types;

            public global_variableEncoder (ModuleEncoder moduleEncoder) : base(moduleEncoder) {
                types = GetStream("global_variable.type");
            }

            public override void Encode (ref global_variable value) {
                Types.global_type.Encode(ref value.type);
                Write(ref value.init);
            }
        }

        public class export_entryEncoder : TypeEncoder<export_entry> {
            KeyedStreamWriter fields, kinds, indices;

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
            KeyedStreamWriter indices;

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
            private KeyedStreamWriter locals, functionExpressionCounts;

            public function_bodyEncoder (ModuleEncoder moduleEncoder) : base(moduleEncoder) {
                locals = GetStream("local_entry");
                functionExpressionCounts = GetStream("function_expression_count");
            }

            private void PeepholeOptimize (ref Expression previous, ref Expression current, ref Expression next) {
                switch (previous.Opcode) {
                    case Opcodes.set_local: {
                        if (
                            (current.Opcode == Opcodes.get_local) &&
                            (current.Body.U.u32 == previous.Body.U.u32)
                        ) {
                            current = new Expression {
                                Opcode = ExpressionEncoder.FakeOpcodes.dup,
                                Body = {
                                    Type = ExpressionBody.Types.none
                                }
                            };
                            return;
                        }
                        break;
                    }
                    // We could generate dup for set/get global pairs but they don't seem to actually show up
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

                var naturalOps = new Dictionary<Opcodes, Opcodes> {
                    { Opcodes.i32_load, ExpressionEncoder.FakeOpcodes.i32_load_natural },
                    { Opcodes.i32_store, ExpressionEncoder.FakeOpcodes.i32_store_natural }
                };

                switch (current.Opcode) {
                    case Opcodes.i32_load:
                    case Opcodes.i32_store:
                        if (current.Body.U.memory.alignment_exponent == 2) {
                            current = new Expression {
                                Opcode = naturalOps[current.Opcode],
                                Body = {
                                    Type = ExpressionBody.Types.u32,
                                    U = {
                                        u32 = current.Body.U.memory.offset
                                    }
                                }
                            };
                            return;
                        }
                        break;
                    /*
                    case Opcodes.i32_const:
                        if (
                            (next.Opcode == Opcodes.set_local) &&
                            (current.Body.U.i32 == 0)
                        ) {
                            Write(new Expression {
                                Opcode = ExpressionEncoder.FakeOpcodes.zero_local,
                                Body = next.Body
                            });
                            return 2;
                        }
                        break;
                    */
                }
            }

            private void Write (Expression e) {
                Write(ref e);
            }

            private void FlushQueue (Expression[] queue, ref int queue_length, bool force) {
                if (queue_length == 0)
                    return;

                if (queue_length > 3)
                    throw new Exception();

                if ((queue_length == 3) || force) {
                    // HACK: queue[3] is always default(Expression) to give us an easy way
                    //  to indirectly reference it
                    PeepholeOptimize(ref queue[3], ref queue[0], ref queue[1]);
                    Write(ref queue[0]);

                    for (int i = 1; i < queue_length; i++) {
                        PeepholeOptimize(ref queue[i - 1], ref queue[i], ref queue[i + 1]);

                        // FIXME: Is this valid?
                        /*
                        if (queue[i].Opcode == Opcodes.nop)
                            continue;
                        */

                        Write(ref queue[i]);
                    }

                    queue_length = 0;

                    for (int i = 0; i < queue.Length; i++)
                        queue[i] = default(Expression);
                }
            }

            public override void Encode (ref function_body value) {
                Builder.Write(value.body_size);

                Builder.WriteArrayLength(value.locals);
                foreach (var le in value.locals) {
                    Builder.Write(le.count, locals);
                    locals.Write((byte)le.type);
                }

                // HACK: Decode the source function body and encode it into the modulebuilder on the fly
                using (var subStream = new StreamWindow(value.Stream, value.StreamOffset, value.StreamEnd - value.StreamOffset)) {
                    var reader = new ExpressionReader(new BinaryReader(subStream));

                    Opcodes previous = Opcodes.end;

                    var queue_length = 0;
                    // [prev, current, next, default(Expression)]
                    var queue = new Expression[4];

                    while (reader.TryReadExpression(out queue[queue_length])) {
                        if (!reader.TryReadExpressionBody(ref queue[queue_length]))
                            throw new Exception("Failed to read body of " + queue[queue_length].Opcode);

                        queue_length++;
                        FlushQueue(queue, ref queue_length, false);
                    }

                    FlushQueue(queue, ref queue_length, true);

                    if (subStream.Position != subStream.Length)
                        throw new Exception("Stopped reading opcodes before end of function body");

                    if (previous == Opcodes.end) {
                        Builder.Write(reader.NumRead, functionExpressionCounts);
                        return;
                    } else
                        throw new Exception("Found no end opcode in function body");
                }
            }
        }

        public class data_segmentEncoder : TypeEncoder<data_segment> {
            public data_segmentEncoder (ModuleEncoder moduleEncoder) : base(moduleEncoder) {
            }

            public override void Encode (ref data_segment value) {
                Builder.Write(value.index);
                Builder.Write(value.size);

                Write(ref value.offset);
            }
        }
    }
}
