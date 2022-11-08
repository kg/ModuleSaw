using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ModuleSaw;
using Wasm.Model;

namespace WasmSaw {
    public static class WasmToMsaw {
        public const uint MinimumSegmentSize = 81920;
        // FIXME: Segment splitting increases post-compression size by a measurable amount,
        //  but optimizes for better streaming compile speeds
        public const int CodeSegmentSplitInterval = 0;
        public const int DataSegmentSplitInterval = 0;

        public static void Convert (Stream input, Stream output) {
            var amb = new AbstractModuleBuilder();

            // HACK: Much faster to read everything in at once, because
            //  we need to seek a lot to decode functions
            MemoryStream inputMs;
            inputMs = new MemoryStream((int)input.Length);
            input.Position = 0;
            input.CopyTo(inputMs);
            inputMs.Position = 0;

            using (var reader = new BinaryReader(inputMs, Encoding.UTF8, false)) {
                StreamingConvert(reader, amb);

                output.SetLength(0);
                amb.SaveTo(output);
            }
        }

        public static void MeasureLocalSizes (Stream input, Stream output) {
            // HACK: Much faster to read everything in at once, because
            //  we need to seek a lot to decode functions
            MemoryStream inputMs;
            inputMs = new MemoryStream((int)input.Length);
            input.Position = 0;
            input.CopyTo(inputMs);
            inputMs.Position = 0;

            var data = new List<(uint index, int paramCount, int localCount, int managedSize, int linearSize, uint bodySize)>();
            func_type[] types = null;
            uint[] typeIndices = null;
            (uint, string)[] nameTable = null;

            using (var sr = new BinaryReader(inputMs, Encoding.UTF8, false))
            using (var writer = new StreamWriter(output, Encoding.UTF8)) {
                var reader = new ModuleReader(sr);

                Program.Assert(reader.ReadHeader(), "ReadHeader");

                int importCount = 0, exportCount = 0;

                SectionHeader sh;
                bool running = true;
                while (reader.ReadSectionHeader(out sh) && running) {
                    switch (sh.id) {
                        case SectionTypes.Type:
                            TypeSection ts;
                            Program.Assert(reader.ReadTypeSection(out ts));
                            types = ts.entries;
                            break;

                        case SectionTypes.Import:
                            ImportSection ims;
                            Program.Assert(reader.ReadImportSection(out ims));
                            importCount = ims.entries.Count(e => e.kind == external_kind.Function);
                            break;

                        case SectionTypes.Export:
                            ExportSection exs;
                            Program.Assert(reader.ReadExportSection(out exs));
                            exportCount = exs.entries.Count(e => e.kind == external_kind.Function);
                            break;

                        case SectionTypes.Function:
                            FunctionSection fs;
                            Program.Assert(reader.ReadFunctionSection(out fs));
                            typeIndices = fs.types;
                            break;

                        case SectionTypes.Code:
                            CodeSection cs;
                            writer.WriteLine("func_index, body_size, param_count, local_count, managed_frame_size, linear_frame_size");
                            Program.Assert(reader.ReadCodeSection(out cs, (fb, br) => {
                                var localsSize = 0;
                                var numLocals = 0;
                                var typeIndex = typeIndices[fb.Index];
                                var type = types[typeIndex];
                                foreach (var param in type.param_types)
                                    localsSize += GetSizeForLanguageType(param);
                                foreach (var local in fb.locals) {
                                    localsSize += (int)(GetSizeForLanguageType(local.type) * local.count);
                                    numLocals += (int)local.count;
                                }
                                var linearStackSize = GetLinearStackSizeForFunction(fb, type, (MemoryStream)br.BaseStream, writer);

                                data.Add((fb.Index, type.param_types.Length, numLocals, localsSize, linearStackSize, fb.body_size));
                            }));
                            break;

                        case SectionTypes.Custom:
                            if (sh.name == "name") {
                                nameTable = ReadNameSection(reader, sr, sh);
                                sr.BaseStream.Seek(sh.StreamPayloadEnd, SeekOrigin.Begin);
                            } else {
                                Console.WriteLine(sh.name);
                                sr.BaseStream.Seek(sh.StreamPayloadEnd, SeekOrigin.Begin);
                            }
                            running = false;
                            break;

                        default:
                            sr.BaseStream.Seek(sh.StreamPayloadEnd, SeekOrigin.Begin);
                            break;
                    }
                }

                foreach (var rec in data) {
                    string name = null;
                    var biasedIndex = rec.index + importCount;
                    if (nameTable != null)
                        name = nameTable.FirstOrDefault(tup => tup.Item1 == biasedIndex).Item2;
                    if (name == null)
                        name = $"#{rec.index}";

                    if (rec.linearSize >= 250000)
                        Debugger.Break();
                    writer.WriteLine($"{name},{rec.bodySize},{rec.paramCount},{rec.localCount},{rec.managedSize},{rec.linearSize}");
                }
            }
        }

        private static (uint idx, string name)[] ReadNameSection (ModuleReader reader, BinaryReader sr, SectionHeader nameSectionHeader) {
            while (sr.BaseStream.Position < nameSectionHeader.StreamPayloadEnd) {
                var id = reader.Reader.ReadByte();
                var size = (uint)reader.Reader.ReadLEBUInt();
                switch (id) {
                    // Function names
                    case 1:
                        return reader.ReadList((i) => {
                            var idx = (uint)reader.Reader.ReadLEBUInt();
                            var name = reader.Reader.ReadPString();
                            return (idx, name);
                        });

                    // Module name
                    case 0:
                    // Local names
                    case 2:
                    default:
                        sr.BaseStream.Seek(size, SeekOrigin.Current);
                        break;
                }
            }

            return null;
        }

        private static Expression MakeConst (int i) {
            return new Expression {
                Opcode = Opcodes.i32_const,
                Body = { U = { i32 = i }, Type = ExpressionBody.Types.i32 }
            };
        }
        private static Stream GetFunctionBodyStream (byte[] bytes, function_body function) {
            var size = (int)(function.StreamEnd - function.StreamOffset);
            var result = new MemoryStream(bytes, (int)function.StreamOffset, size, false);
            return result;
        }

        private static int GetLinearStackSizeForFunction (function_body fb, func_type type, MemoryStream source, StreamWriter writer) {
            try {
                using (var subStream = GetFunctionBodyStream(source.GetBuffer(), fb)) {
                    var reader = new ExpressionReader(new BinaryReader(subStream));

                    Expression expr;
                    Opcodes previous = Opcodes.end;

                    var localCount = fb.locals.Sum(l => l.count) + type.param_types.Length;
                    var locals = new Expression[localCount];
                    Expression global0 = MakeConst(0);
                    var stack = new Stack<Expression>();

                    int num_read = 0;
                    while (reader.TryReadExpression(out expr) && num_read < 20) {
                        if (!reader.TryReadExpressionBody(ref expr))
                            throw new Exception("Failed to read body of " + expr.Opcode);

                        num_read++;

                        switch (expr.Opcode) {
                            case Opcodes.i32_const:
                                stack.Push(expr);
                                break;
                            case Opcodes.get_global:
                                // HACK
                                stack.Push(MakeConst(0));
                                break;
                            case Opcodes.set_global:
                                if (expr.Body.U.u32 == 0) {
                                    global0 = stack.Pop();
                                    num_read = 9999;
                                } else
                                    stack.Pop();
                                break;
                            case Opcodes.get_local:
                                stack.Push(locals[expr.Body.U.u32]);
                                break;
                            case Opcodes.set_local:
                                locals[expr.Body.U.u32] = stack.Pop();
                                break;
                            case Opcodes.tee_local:
                                locals[expr.Body.U.u32] = stack.Peek();
                                break;
                            case Opcodes.i32_add:
                            case Opcodes.i32_sub: {
                                    var a = stack.Pop().Body.U.i32;
                                    var b = stack.Pop().Body.U.i32;
                                    stack.Push(MakeConst(
                                        a + (b * (expr.Opcode == Opcodes.i32_sub ? -1 : 1))
                                    ));
                                    break;
                            }
                            case Opcodes.end:
                                break;
                            default:
                                break;
                        }

                        previous = expr.Opcode;
                    }

                    return Math.Abs(global0.Body.U.i32);
                }
            } catch {
                return 0;
            }
        }

        private static int GetSizeForLanguageType (LanguageTypes type) {
            switch (type) {
                case LanguageTypes.f32:
                    return 4;
                case LanguageTypes.f64:
                    return 8;
                case LanguageTypes.i32:
                    return 4;
                case LanguageTypes.i64:
                    return 8;
                case LanguageTypes.anyfunc:
                case LanguageTypes.func:
                    return 4;
                default:
                    return 0;
            }
        }

        private static void StreamingConvert (BinaryReader wasm, AbstractModuleBuilder amb) {
            var ms = (MemoryStream)wasm.BaseStream;
            var reader = new ModuleReader(wasm);
            var encoder = new ModuleEncoder(amb);

            Program.Assert(reader.ReadHeader(), "ReadHeader");

            var t = encoder.Types;

            SectionHeader sh;
            while (reader.ReadSectionHeader(out sh)) {
                var previousSize = amb.TotalSize;

                t.SectionHeader.Encode(ref sh);

                switch (sh.id) {
                    case SectionTypes.Type:
                        TypeSection ts;
                        Program.Assert(reader.ReadTypeSection(out ts));
                        t.func_type.Encode(ts.entries);
                        break;

                    case SectionTypes.Import:
                        ImportSection ims;
                        Program.Assert(reader.ReadImportSection(out ims));
                        t.import_entry.Encode(ims.entries);
                        break;

                    case SectionTypes.Function:
                        FunctionSection fs;
                        Program.Assert(reader.ReadFunctionSection(out fs));
                        var functionIndices = amb.GetStream("function_index");
                        amb.WriteArrayLength(fs.types);
                        foreach (var typeIndex in fs.types)
                            amb.Write(typeIndex, functionIndices);
                        break;

                    case SectionTypes.Table:
                        // FIXME: Not tested
                        TableSection tbs;
                        Program.Assert(reader.ReadTableSection(out tbs));
                        t.table_type.Encode(tbs.entries);
                        break;

                    case SectionTypes.Global:
                        GlobalSection gs;
                        Program.Assert(reader.ReadGlobalSection(out gs));
                        t.global_variable.Encode(gs.globals);
                        break;

                    case SectionTypes.Export:
                        ExportSection exs;
                        Program.Assert(reader.ReadExportSection(out exs));
                        t.export_entry.Encode(exs.entries);
                        break;

                    case SectionTypes.Element:
                        ElementSection els;
                        Program.Assert(reader.ReadElementSection(out els));
                        t.elem_segment.Encode(els.entries);
                        break;

                    case SectionTypes.Code:
                        CodeSection cs;
                        Program.Assert(reader.ReadCodeSection(out cs));
                        t.function_body.SourceStream = ms;
                        t.function_body.Encode(cs.bodies, CodeSegmentSplitInterval);
                        Console.WriteLine("Encoded {0} expressions", t.function_body.ModuleEncoder.ExpressionEncoder.NumWritten);
                        break;

                    case SectionTypes.Data:
                        DataSection ds;
                        Program.Assert(reader.ReadDataSection(out ds));
                        t.data_segment.Encode(ds.entries, DataSegmentSplitInterval);

                        var dataStream = amb.GetStream("data_segments");
                        dataStream.Flush();

                        foreach (var ent in ds.entries) {
                            using (var subStream = new MemoryStream(ms.GetBuffer(), (int)ent.data_offset, (int)ent.size, false))
                                subStream.CopyTo(dataStream.BaseStream);
                        }
                        break;

                    default:
                        var s = amb.GetStream("unknown_section_length");
                        amb.Write(sh.payload_len, s);

                        s = amb.GetStream("unknown_section_data");
                        using (var src = new MemoryStream(ms.GetBuffer(), (int)sh.StreamHeaderStart, (int)sh.payload_len, false))
                            src.CopyTo(s.Stream);

                        wasm.BaseStream.Seek(sh.StreamPayloadEnd, SeekOrigin.Begin);
                        break;
                }

                Console.WriteLine(
                    "{0}: Wrote {1} byte(s) (from {2}b of wasm)", 
                    (sh.id == SectionTypes.Custom) ? sh.name : sh.id.ToString(), amb.TotalSize - previousSize, sh.payload_len
                );
            }

            amb.MoveStreamToBack("data_segments");
            amb.MoveStreamToBack("unknown_section_data");
        }
    }
}
