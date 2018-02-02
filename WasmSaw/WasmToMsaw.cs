using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ModuleSaw;
using Wasm.Model;

namespace WasmSaw {
    public static class WasmToMsaw {
        public const int CodeSegmentSplitInterval = 511;
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

        private static void StreamingConvert (BinaryReader wasm, AbstractModuleBuilder amb) {
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
                        t.function_body.Encode(cs.bodies, CodeSegmentSplitInterval);
                        break;

                    case SectionTypes.Data:
                        DataSection ds;
                        Program.Assert(reader.ReadDataSection(out ds));
                        t.data_segment.Encode(ds.entries, DataSegmentSplitInterval);

                        var dataStream = amb.GetStream("data_segments");
                        dataStream.Flush();

                        foreach (var ent in ds.entries) {
                            using (var subStream = new StreamWindow(wasm.BaseStream, ent.data_offset, ent.size))
                                subStream.CopyTo(dataStream.BaseStream);
                        }
                        break;

                    default:
                        var s = amb.GetStream("unknown_section_length");
                        amb.Write(sh.payload_len, s);

                        s = amb.GetStream("unknown_section_data");
                        using (var src = new StreamWindow(wasm.BaseStream, sh.StreamHeaderStart, sh.payload_len))
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
