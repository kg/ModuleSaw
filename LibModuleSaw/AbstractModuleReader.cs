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
            public uint   StreamLength;

            public override string ToString () {
                return $"{Key} {StreamLength}bytes {SegmentCount} segment(s)";
            }
        }

        public struct SegmentHeader {
            public uint StreamIndex;
            public uint SegmentIndex;
            public uint SegmentLength;
        }

        public class StreamList : IEnumerable<StreamTableHeader> {
            internal StreamTableHeader[] Headers;
            internal Dictionary<string, StreamTableHeader> Table =
                new Dictionary<string, StreamTableHeader>(StringComparer.Ordinal);
            internal Dictionary<string, ArrayBinaryReader> Readers =
                new Dictionary<string, ArrayBinaryReader>(StringComparer.Ordinal);

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
                ArrayBinaryReader result;
                if (Readers.TryGetValue(key, out result))
                    return result;

                var header = Table[key];
                var buffer = new byte[header.StreamLength];
                Readers[key] = result = new ArrayBinaryReader(new ArraySegment<byte>(buffer), 0, header.StreamLength, 0);
                return result;
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
            var length = (uint)input.Length;
            Bytes = new byte[input.Length];
            input.Read(Bytes, 0, (int)input.Length);

            Reader = new ArrayBinaryReader(new ArraySegment<byte>(Bytes), 0, length, length);
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

        public bool ReadSegment () {
            uint streamIndex, segmentIndex, segmentLength, marker;

            var headerOk = Reader.Read(out streamIndex) &
                Reader.Read(out segmentIndex) &
                Reader.Read(out segmentLength);
            if (!headerOk)
                return false;

            var streamHeader = Streams.Headers[streamIndex];
            var streamReader = Streams.Open(streamHeader.Key);
            var buffer = streamReader.Data.Array;
            var readOffset = (uint)(streamReader.Data.Offset + streamReader.AvailableLength);
            if (!Reader.Read(buffer, readOffset, segmentLength))
                return false;

            if (!Reader.Read(out marker))
                return false;

            if (marker != AbstractModuleBuilder.BoundaryMarker3)
                return false;

            streamReader.SetAvailableLength(streamReader.AvailableLength + segmentLength);
            return true;
        }

        public bool ReadAllSegments () {
            while (Reader.Position < Reader.Length) {
                if (!ReadSegment())
                    return false;
            }

            return true;
        }

        private void PreopenStreams () {
            IntStream = Streams.Open("i32");
            UIntStream = Streams.Open("u32");
            LongStream = Streams.Open("i64");
            ByteStream = UIntStream;
            SingleStream = Streams.Open("f32");
            DoubleStream = Streams.Open("f64");
            BooleanStream = ByteStream;
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
