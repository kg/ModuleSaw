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
    static class Program {
        public static void Main (string[] args) {
            if (args.Length != 2)
                throw new Exception("Expected: WasmSaw input.wasm output.msaw\r\nor:       WasmSaw input.msaw output.wasm");

            Console.Write("{0} ... ", args[0]);

            // SelfTest();

            var config = CreateConfiguration();

            using (var inputFile = File.OpenRead(args[0]))
            using (var outputFile = File.OpenWrite(args[1])) {
                outputFile.SetLength(0);

                if (IsThisWasm(inputFile))
                    WasmToMsaw.Convert(inputFile, outputFile, config);
                else if (IsThisMsaw(inputFile, config))
                    MsawToWasm.Convert(inputFile, outputFile, config);
                else
                    throw new Exception("Unrecognized input format");
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

        private static ulong SelfTestSingle (ulong l) {
            var ms = new MemoryStream();
            var bw = new BinaryWriter(ms, Encoding.UTF8, true);
            bw.WriteLEB(l);
            bw.Dispose();

            ms.Position = 0;
            var br = new BinaryReader(ms);
            var read = br.ReadLEBUInt();
            return read.Value;
        }

        private static long SelfTestSingle (long l) {
            var ms = new MemoryStream();
            var bw = new BinaryWriter(ms, Encoding.UTF8, true);
            bw.WriteLEB(l);
            bw.Dispose();

            ms.Position = 0;
            var br = new BinaryReader(ms);
            var read = br.ReadLEBInt();
            return read.Value;
        }

        public static void SelfTest () {
            var values = new long[] {
                9401, 12546, 113794, 51, 15658, 376331, 23891164, 6249699, 8841692, 0, 1, -1, 127, 128, -127, -128
            };
            bool failed = false;

            foreach (var l in values) {
                var a = SelfTestSingle(l);
                var b = SelfTestSingle((ulong)l);

                if (a != l) {
                    Console.WriteLine("Expected {0} got {1}", l, a);
                    failed = true;
                }
                
                if (b != (ulong)l) {
                    Console.WriteLine("Expected {0} got {1}", (ulong)l, b);
                    failed = true;
                }
            }

            if (failed)
                throw new Exception();
        }
        
        public static bool IsThisWasm (Stream input) {
            input.Position = 0;

            using (var reader = new BinaryReader(input, Encoding.UTF8, true)) {
                var mr = new ModuleReader(reader);
                return mr.ReadHeader();
            }
        }

        public static bool IsThisMsaw (Stream input, Configuration configuration) {
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
