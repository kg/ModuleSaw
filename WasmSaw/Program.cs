using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using ModuleSaw;
using Wasm.Model;

namespace WasmSaw {
    class Program {
        static void Main (string[] args) {
            if (args.Length != 2)
                throw new Exception("Expected: input.wasm output.msaw");

            Console.Write("{0} ... ", args[0]);

            var config = CreateConfiguration();
            var amb = new AbstractModuleBuilder {
                Configuration = config
            };

            using (var input = new BinaryReader(File.OpenRead(args[0]), Encoding.UTF8, false)) {
                StreamingConvert(input, amb);
            }

            using (var output = File.OpenWrite(args[1])) {
                output.SetLength(0);
                amb.SaveTo(output);
            }

            Console.WriteLine(args[1]);

            if (Debugger.IsAttached)
                Console.ReadLine();
        }

        private static Configuration CreateConfiguration () {
            var result = new Configuration {
                Varints = true
            };

            return result;
        }

        private static void Assert (
            bool b,
            string description = null,
            [CallerMemberName] string memberName = "",  
            [CallerFilePath]   string sourceFilePath = "",  
            [CallerLineNumber] int sourceLineNumber = 0
        ) {
            if (!b)
                throw new Exception(string.Format(
                    "{0} failed in {1} @ {2}:{3}",
                    description ?? "Assert",
                    memberName, Path.GetFileName(sourceFilePath), sourceLineNumber
                ));
        }

        private static void StreamingConvert (BinaryReader wasm, AbstractModuleBuilder amb) {
            var reader = new ModuleReader(wasm);
            var encoder = new ModuleEncoder(amb);

            Assert(reader.ReadHeader(), "ReadHeader");

            // Record these and then write them at the end of the encoded module
            var codeSections = new List<CodeSection>();
            var dataSections = new List<DataSection>();
            var t = encoder.Types;

            SectionHeader sh;
            while (reader.ReadSectionHeader(out sh)) {
                t.SectionHeader.Encode(ref sh);

                switch (sh.id) {
                    case SectionTypes.Type:
                        TypeSection ts;
                        Assert(reader.ReadTypeSection(out ts));
                        t.func_type.Encode(ts.entries);
                        break;

                    case SectionTypes.Import:
                        ImportSection ims;
                        Assert(reader.ReadImportSection(out ims));
                        t.import_entry.Encode(ims.entries);
                        break;

                    case SectionTypes.Function:
                        FunctionSection fs;
                        Assert(reader.ReadFunctionSection(out fs));
                        amb.WriteArrayLength(fs.types);
                        foreach (var typeIndex in fs.types)
                            amb.Write(typeIndex);
                        break;

                    case SectionTypes.Global:
                        GlobalSection gs;
                        Assert(reader.ReadGlobalSection(out gs));
                        t.global_variable.Encode(gs.globals);
                        break;

                    case SectionTypes.Export:
                        ExportSection exs;
                        Assert(reader.ReadExportSection(out exs));
                        t.export_entry.Encode(exs.entries);
                        break;

                    case SectionTypes.Element:
                        ElementSection els;
                        Assert(reader.ReadElementSection(out els));
                        t.elem_segment.Encode(els.entries);
                        break;

                    case SectionTypes.Code:
                        CodeSection cs;
                        Assert(reader.ReadCodeSection(out cs));
                        codeSections.Add(cs);
                        t.function_body.Encode(cs.bodies);
                        break;

                    case SectionTypes.Data:
                        DataSection ds;
                        Assert(reader.ReadDataSection(out ds));
                        dataSections.Add(ds);
                        t.data_segment.Encode(ds.entries);
                        break;

                    default:
                        Console.WriteLine("{0} {1}b", sh.name ?? sh.id.ToString(), sh.payload_len);
                        wasm.BaseStream.Seek(sh.payload_len, SeekOrigin.Current);
                        break;
                }
            }

            var functionStream = amb.GetStream("function_bodies");
            functionStream.Flush();

            foreach (var cs in codeSections) {
                foreach (var body in cs.bodies) {
                    using (var subStream = new StreamWindow(wasm.BaseStream, body.code_offset, body.code_size))
                        subStream.CopyTo(functionStream.BaseStream);
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
    }
}
