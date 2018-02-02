using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModuleSaw {
    public class AbstractModuleReader {
        public struct StreamTableHeader {
            public string Key;
            public uint   SegmentCount;
            public ulong  StreamLength;
        }

        public class StreamList : IEnumerable<StreamTableHeader> {
            internal StreamTableHeader[] Headers;
            internal Dictionary<string, StreamTableHeader> Table =
                new Dictionary<string, StreamTableHeader>(StringComparer.Ordinal);

            internal StreamList () {
            }

            public StreamTableHeader this [int index] {
                get {
                    return Headers[index];
                }
            }

            public StreamTableHeader this [string key] {
                get {
                    return Table[key];
                }
            }

            public int Count {
                get {
                    return Headers.Length;
                }
            }

            public ArrayBinaryReader Open (string key) {
                throw new NotImplementedException();
            }

            public IEnumerator<StreamTableHeader> GetEnumerator () {
                return ((IEnumerable<StreamTableHeader>)Headers).GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator () {
                return ((IEnumerable<StreamTableHeader>)Headers).GetEnumerator();
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

        private bool ReadPrologue () {
            var buffer = new byte[AbstractModuleBuilder.Prologue.Length];
            if (!Reader.Read(buffer))
                return false;
            return buffer.SequenceEqual(AbstractModuleBuilder.Prologue);
        }
        
        public bool ReadHeader () {
            if (!ReadPrologue())
                return false;            

            if (!Reader.Read(out uint temp) || 
                (temp != AbstractModuleBuilder.BoundaryMarker1))
                return false;

            if (!Reader.Read(out int streamCount))
                return false;

            var headers = new StreamTableHeader[streamCount];
            var keyBuffer = new byte[KeyedStreamWriter.MaxKeyLength];
            Streams.Table.Clear();

            for (int i = 0; i < streamCount; i++) {
                if (!Reader.Read(keyBuffer))
                    return false;

                headers[i] = new StreamTableHeader {
                    Key = Encoding.UTF8.GetString(keyBuffer, 0, Array.IndexOf(keyBuffer, (byte)0))
                };

                if (!Reader.Read(out headers[i].SegmentCount))
                    return false;

                if (!Reader.Read(out headers[i].StreamLength))
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
            IntStream = Streams.Open("i32");
            UIntStream = Streams.Open("u32");
            LongStream = Streams.Open("i64");
            ByteStream = Streams.Open("u8");
            SingleStream = Streams.Open("f32");
            DoubleStream = Streams.Open("f64");
            BooleanStream = Streams.Open("u1");
            ArrayLengthStream = Streams.Open("arrayLength");
            StringLengthStream = Streams.Open("stringLength");
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
