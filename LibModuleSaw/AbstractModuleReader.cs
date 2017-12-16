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
            public uint   Offset;
            public uint   Length;
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

        public readonly ArrayBinaryReader Reader;
        private readonly byte[] Bytes;

        public readonly StreamList Streams = new StreamList();

        private readonly Dictionary<string, ArrayBinaryReader> StreamCache =
            new Dictionary<string, ArrayBinaryReader>(StringComparer.Ordinal);

        public string SubFormat { get; private set; }

        public ArrayBinaryReader
            IntStream, UIntStream,
            LongStream, ByteStream,
            SingleStream, DoubleStream,
            BooleanStream, ArrayLengthStream,
            StringLengthStream;

        public AbstractModuleReader (Stream input) {
            // This sucks :(
            Bytes = new byte[input.Length];
            input.Read(Bytes, 0, (int)input.Length);

            Reader = new ArrayBinaryReader(Bytes);
        }

        public ArrayBinaryReader Open (StreamHeader header) {
            ArrayBinaryReader result;
            if (!StreamCache.TryGetValue(header.Key, out result))
                StreamCache[header.Key] = result = OpenNew(header);

            return result;
        }

        public ArrayBinaryReader OpenNew (StreamHeader header) {
            return new ArrayBinaryReader(Bytes, (uint)header.Offset, (uint)header.Length);
        }

        private bool ReadPrologue () {
            var buffer = new byte[AbstractModuleBuilder.Prologue.Length];
            if (!Reader.Read(buffer))
                return false;
            return buffer.SequenceEqual(AbstractModuleBuilder.Prologue);
        }
        
        public bool ReadHeader () {
            if (!ReadPrologue())
                return false;

            if (!Reader.Read(out int subFormatLength))
                return false;

            if (!Reader.Seek(subFormatLength))
                return false;

            if (!Reader.Read(out uint temp) || 
                (temp != AbstractModuleBuilder.BoundaryMarker1))
                return false;

            if (!Reader.Read(out int streamCount))
                return false;

            var headers = new StreamHeader[streamCount];
            var keyBuffer = new byte[KeyedStream.MaxKeyLength];
            Streams.Table.Clear();

            for (int i = 0; i < streamCount; i++) {
                if (!Reader.Read(keyBuffer))
                    return false;

                headers[i] = new StreamHeader {
                    Key = Encoding.UTF8.GetString(keyBuffer, 0, Array.IndexOf(keyBuffer, (byte)0))
                };

                if (!Reader.Read(out headers[i].Offset))
                    return false;

                if (!Reader.Read(out headers[i].Length))
                    return false;

                Streams.Table.Add(headers[i].Key, headers[i]);
            }

            Streams.Headers = headers;

            if (!Reader.Read(out temp) ||
                (temp != AbstractModuleBuilder.BoundaryMarker2))
                return false;

            PreopenStreams();

            return true;
        }

        private void PreopenStreams () {
            IntStream = Open(Streams["i32"]);
            UIntStream = Open(Streams["u32"]);
            LongStream = Open(Streams["i64"]);
            ByteStream = Open(Streams["u8"]);
            SingleStream = Open(Streams["f32"]);
            DoubleStream = Open(Streams["f64"]);
            BooleanStream = Open(Streams["u1"]);
            ArrayLengthStream = Open(Streams["arrayLength"]);
            StringLengthStream = Open(Streams["stringLength"]);
        }

        public string ReadString (ArrayBinaryReader stream) {
            if (!StringLengthStream.ReadU32LEB(out uint lengthPlusOne))
                return null;

            if (lengthPlusOne == 0)
                return null;
            else if (lengthPlusOne == 1)
                return "";

            var bytes = new byte[lengthPlusOne - 1];
            if (!stream.Read(bytes))
                return null;

            return Encoding.UTF8.GetString(bytes);
        }

        public uint ReadArrayLength () {
            if (!ArrayLengthStream.ReadU32LEB(out uint result))
                throw new EndOfStreamException();
            return result;
        }

        public T[] ReadArray<T> (Func<T> readElement) {
            var length = ReadArrayLength();
            var result = new T[length];
            for (uint i = 0; i < length; i++)
                result[i] = readElement();
            return result;
        }
    }
}
