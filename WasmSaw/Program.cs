using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ModuleSaw;

namespace WasmSaw {
    class Program {
        static void Main (string[] args) {
            if (args.Length != 2)
                throw new Exception("Expected: input.wasm output.msaw");

            var config = CreateConfiguration();
            var amb = new AbstractModuleBuilder {
                Configuration = config
            };

            using (var input = new BinaryReader(File.OpenRead(args[0]), Encoding.UTF8, false)) {
            }

            using (var output = File.OpenWrite(args[1])) {
                output.SetLength(0);
                amb.SaveTo(output);
            }

            if (Debugger.IsAttached)
                Console.ReadLine();
        }

        private static Configuration CreateConfiguration () {
            var result = new Configuration {
            };

            // result.AddSchema();

            return result;
        }
    }
}
