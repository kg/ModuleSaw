using System;
using System.Collections;
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

        public class StreamList : IEnumerable<StreamHeader> {
            internal StreamHeader[] Headers;
            internal Dictionary<string, StreamHeader> Table =
                new Dictionary<string, StreamHeader>(StringComparer.Ordinal);

            internal StreamList () {
            }

            public StreamHeader this [int index] {
                get {
                    return Headers[index];
                }
            }

            public StreamHeader this [string key] {
                get {
                    return Table[key];
                }
            }

            public int Count {
                get {
                    return Headers.Length;
                }
            }

            public IEnumerator<StreamHeader> GetEnumerator () {
                return ((IEnumerable<StreamHeader>)Headers).GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator () {
                return ((IEnumerable<StreamHeader>)Headers).GetEnumerator();
            }
        }

        public readonly BinaryReader Reader;

        public Configuration Configuration;

        public readonly StreamList Streams = new StreamList();

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
            Streams.Table.Clear();

            for (int i = 0; i < streamCount; i++) {
                Reader.Read(keyBuffer, 0, keyBuffer.Length);

                headers[i] = new StreamHeader {
                    Key = Encoding.UTF8.GetString(keyBuffer, 0, Array.IndexOf(keyBuffer, (byte)0)),
                    Offset = Reader.ReadInt64(),
                    Length = Reader.ReadInt64()
                };

                Streams.Table.Add(headers[i].Key, headers[i]);
            }

            Streams.Headers = headers;

            if (Reader.ReadUInt32() != AbstractModuleBuilder.BoundaryMarker2)
                return false;

            return true;
        }
    }
}
