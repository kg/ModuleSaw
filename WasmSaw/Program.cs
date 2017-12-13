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

            // result.AddSchema();

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
            var mr = new ModuleReader(wasm);

            Assert(mr.ReadHeader(), "ReadHeader");

            SectionHeader sh;
            while (mr.ReadSectionHeader(out sh)) {
                switch (sh.id) {
                    case SectionTypes.Type:
                        TypeSection ts;
                        Assert(mr.ReadTypeSection(out ts));
                        break;

                    case SectionTypes.Import:
                        ImportSection ims;
                        Assert(mr.ReadImportSection(out ims));
                        break;

                    case SectionTypes.Function:
                        FunctionSection fs;
                        Assert(mr.ReadFunctionSection(out fs));
                        break;

                    case SectionTypes.Global:
                        GlobalSection gs;
                        Assert(mr.ReadGlobalSection(out gs));
                        break;

                    case SectionTypes.Export:
                        ExportSection exs;
                        Assert(mr.ReadExportSection(out exs));
                        break;

                    case SectionTypes.Element:
                        ElementSection els;
                        Assert(mr.ReadElementSection(out els));
                        break;

                    case SectionTypes.Code:
                        CodeSection cs;
                        Assert(mr.ReadCodeSection(out cs));
                        break;

                    case SectionTypes.Data:
                        DataSection ds;
                        Assert(mr.ReadDataSection(out ds));
                        break;

                    default:
                        Console.WriteLine("{0} {1}b", sh.name ?? sh.id.ToString(), sh.payload_len);
                        wasm.BaseStream.Seek(sh.payload_len, SeekOrigin.Current);
                        break;
                }
            }
        }
    }
}
