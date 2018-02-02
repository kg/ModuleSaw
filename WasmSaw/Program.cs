using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using ModuleSaw;
using Wasm.Model;

namespace WasmSaw {
    static class Program {
        public static void Main (string[] args) {
            VarintExtensions.SelfTest();

            GCSettings.LatencyMode = GCLatencyMode.Batch;

            if (args.Length != 2)
                throw new Exception("Expected: WasmSaw input.wasm output.msaw\r\nor:       WasmSaw input.msaw output.wasm");

            Console.WriteLine($"{args[0]} ...");

            using (var inputFile = File.OpenRead(args[0]))
            using (var outputFile = File.OpenWrite(args[1])) {
                outputFile.SetLength(0);

                if (IsThisWasm(inputFile))
                    WasmToMsaw.Convert(inputFile, outputFile);
                else if (IsThisMsaw(inputFile))
                    MsawToWasm.Convert(inputFile, outputFile);
                else
                    throw new Exception("Unrecognized input format");
            }

            Console.WriteLine(args[1]);

            if (Debugger.IsAttached)
                Console.ReadLine();
        }
        
        public static bool IsThisWasm (Stream input) {
            input.Position = 0;

            using (var reader = new BinaryReader(input, Encoding.UTF8, true)) {
                var mr = new ModuleReader(reader);
                return mr.ReadHeader();
            }
        }

        public static bool IsThisMsaw (Stream input) {
            input.Position = 0;

            using (var reader = new BinaryReader(input, Encoding.UTF8, true)) {
                var prologue = reader.ReadBytes(AbstractModuleBuilder.Prologue.Length);
                return prologue.SequenceEqual(AbstractModuleBuilder.Prologue);
            }
        }

        public static void Assert (
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
    }
}
