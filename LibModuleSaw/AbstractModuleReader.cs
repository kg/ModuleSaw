using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModuleSaw {
    public class AbstractModuleReader {
        public struct StreamHeader {
            public string Key;
            public long   Offset;
            public long   Length;
        }

        public readonly BinaryReader Reader;

        public Configuration Configuration;

        public StreamHeader[] StreamHeaders { get; private set; }
        public string SubFormat { get; private set; }

        public AbstractModuleReader (BinaryReader input, Configuration configuration) {
            Reader = input;
            Configuration = configuration;
        }

        public bool ReadPrologue () {
            var buffer = Reader.ReadBytes(AbstractModuleBuilder.Prologue.Length);
            return buffer.SequenceEqual(AbstractModuleBuilder.Prologue);
        }

        public bool ReadHeader () {
            if (!ReadPrologue())
                return false;

            SubFormat = Reader.ReadString();

            if (Reader.ReadUInt32() != AbstractModuleBuilder.BoundaryMarker1)
                return false;

            var streamCount = Reader.ReadInt32();
            var headers = new StreamHeader[streamCount];
            var keyBuffer = new byte[KeyedStream.MaxKeyLength];
            for (int i = 0; i < streamCount; i++) {
                Reader.Read(keyBuffer, 0, keyBuffer.Length);

                headers[i] = new StreamHeader {
                    Key = Encoding.UTF8.GetString(keyBuffer, 0, Array.IndexOf(keyBuffer, 0)),
                    Offset = Reader.ReadInt64(),
                    Length = Reader.ReadInt64()
                };
            }

            StreamHeaders = headers;

            if (Reader.ReadUInt32() != AbstractModuleBuilder.BoundaryMarker2)
                return false;

            return true;
        }
    }
}
