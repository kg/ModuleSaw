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
                Varints = false,
                ExcludePrimitivesFromPartitioning = false
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
            var dataSections = new List<DataSection>();

            SectionHeader sh;
            while (reader.ReadSectionHeader(out sh)) {
                encoder.Write(ref sh);

                switch (sh.id) {
                    case SectionTypes.Type:
                        TypeSection ts;
                        Assert(reader.ReadTypeSection(out ts));
                        encoder.WriteArray(ts.entries);
                        break;

                    case SectionTypes.Import:
                        ImportSection ims;
                        Assert(reader.ReadImportSection(out ims));
                        encoder.WriteArray(ims.entries);
                        break;

                    case SectionTypes.Function:
                        FunctionSection fs;
                        Assert(reader.ReadFunctionSection(out fs));
                        encoder.WriteArray(fs.types);
                        break;

                    case SectionTypes.Global:
                        GlobalSection gs;
                        Assert(reader.ReadGlobalSection(out gs));
                        encoder.WriteArray(gs.globals);
                        break;

                    case SectionTypes.Export:
                        ExportSection exs;
                        Assert(reader.ReadExportSection(out exs));
                        encoder.WriteArray(exs.entries);
                        break;

                    case SectionTypes.Element:
                        ElementSection els;
                        Assert(reader.ReadElementSection(out els));
                        encoder.WriteArray(els.entries);
                        break;

                    case SectionTypes.Code:
                        CodeSection cs;
                        Assert(reader.ReadCodeSection(out cs));
                        encoder.WriteArray(cs.bodies);
                        break;

                    case SectionTypes.Data:
                        DataSection ds;
                        Assert(reader.ReadDataSection(out ds));
                        dataSections.Add(ds);
                        encoder.WriteArray(ds.entries);
                        break;

                    default:
                        Console.WriteLine("{0} {1}b", sh.name ?? sh.id.ToString(), sh.payload_len);
                        wasm.BaseStream.Seek(sh.payload_len, SeekOrigin.Current);
                        break;
                }
            }

            var dataStream = amb.GetStream("data_sections");
            dataStream.Flush();

            foreach (var ds in dataSections) {
                foreach (var ent in ds.entries) {
                    var subStream = new StreamWindow(wasm.BaseStream, ent.data_offset, ent.size);
                    subStream.CopyTo(dataStream.BaseStream);
                }
            }
        }
    }
}
