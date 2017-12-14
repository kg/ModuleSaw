﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ModuleSaw;
using Wasm.Model;

namespace WasmSaw {
    public static class WasmToMsaw {
        public static void Convert (Stream input, Stream output, Configuration config) {
            var amb = new AbstractModuleBuilder {
                Configuration = config
            };

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
                amb.SaveTo(output, "webassembly-v1");
            }
        }

        private static void StreamingConvert (BinaryReader wasm, AbstractModuleBuilder amb) {
            var reader = new ModuleReader(wasm);
            var encoder = new ModuleEncoder(amb);

            Program.Assert(reader.ReadHeader(), "ReadHeader");

            // Record these and then write them at the end of the encoded module
            var dataSections = new List<DataSection>();
            var t = encoder.Types;

            SectionHeader sh;
            while (reader.ReadSectionHeader(out sh)) {
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

                        var functionStream = amb.GetStream("function_bodies");
                        functionStream.Flush();

                        t.function_body.Encode(cs.bodies);

                        foreach (var body in cs.bodies)
                            EncodeFunctionBody(encoder, functionStream, wasm.BaseStream, body);

                        break;

                    case SectionTypes.Data:
                        DataSection ds;
                        Program.Assert(reader.ReadDataSection(out ds));
                        dataSections.Add(ds);
                        t.data_segment.Encode(ds.entries);
                        break;

                    default:
                        var s = amb.GetStream("unknown_sections");
                        amb.Write((sbyte)sh.id, s);
                        amb.Write(sh.name, s);
                        amb.Write(sh.payload_len, s);

                        s = amb.GetStream("unknown_section_data");
                        using (var src = new StreamWindow(wasm.BaseStream, sh.payload_start, sh.payload_end - sh.payload_start))
                            src.CopyTo(s.Stream);

                        wasm.BaseStream.Seek(sh.payload_end, SeekOrigin.Begin);
                        break;
                }
            }

            var dataStream = amb.GetStream("data_segments");
            dataStream.Flush();

            foreach (var ds in dataSections) {
                foreach (var ent in ds.entries) {
                    using (var subStream = new StreamWindow(wasm.BaseStream, ent.data_offset, ent.size))
                        subStream.CopyTo(dataStream.BaseStream);
                }
            }
        }

        private static void EncodeFunctionBody (ModuleEncoder encoder, KeyedStream functionStream, Stream baseStream, function_body body) {
            using (var subStream = new StreamWindow(baseStream, body.body_offset, body.body_size)) {
                var reader = new ExpressionReader(new BinaryReader(subStream));
                var ee = encoder.ExpressionEncoder;
                Expression e;

                while (reader.TryReadExpression(out e)) {
                    if (!reader.TryReadExpressionBody(ref e))
                        throw new Exception("Failed to read body of " + e.Opcode);

                    if (e.Opcode == Opcodes.end)
                        return;

                    ee.Write(ref e);
                }

                throw new Exception("Found no end opcode in function body");
            }
        }
    }
}
