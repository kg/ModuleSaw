using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace ModuleSaw {
    public class AbstractModuleBuilder {
        internal struct SegmentEntry {
            public KeyedStreamWriter Stream;
            public int SegmentIndex;

            public override string ToString () {
                return $"{Stream.Key} #{SegmentIndex}";
            }
        }

        // HACK: Large padding regions at stream boundaries can increase brotli compression
        //  efficiency, presumably because the context model responds to the distance between
        //  different data types?
        public const int StreamAlignment = 1024 * 64;

        public const int StreamAlignmentThreshold = 1024;

        public const uint BoundaryMarker1 = 0xDBCA1234,
            BoundaryMarker2 = 0xABCD9876,
            BoundaryMarker3 = 0x13579FCA;

        public static readonly byte[] Prologue = (new[] {
            '\x89', 'M', 'S', 'a', 'w', '\r', '\n', '\x1a', '\n', '\0'
        }).Select(c => (byte)c).ToArray();

        public KeyedStreamWriter
            IntStream, UIntStream,
            LongStream, ByteStream,
            SingleStream, DoubleStream,
            BooleanStream, ArrayLengthStream,
            StringLengthStream;

        private readonly Dictionary<string, KeyedStreamWriter> Streams = 
            new Dictionary<string, KeyedStreamWriter>(StringComparer.Ordinal);
        private readonly List<KeyedStreamWriter> OrderedStreams = new List<KeyedStreamWriter>();
        private readonly List<SegmentEntry> OrderedSegments = new List<SegmentEntry>();

        public AbstractModuleBuilder () {
            LongStream = GetStream("i64");
            IntStream = GetStream("i32");
            UIntStream = GetStream("u32");
            ByteStream = UIntStream;
            SingleStream = GetStream("f32");
            DoubleStream = GetStream("f64");
            BooleanStream = ByteStream;
            StringLengthStream = GetStream("stringLength");
            ArrayLengthStream = GetStream("arrayLength");
        }

        public void MoveStreamToBack (string key) {
            if (!Streams.ContainsKey(key))
                return;

            var s = Streams[key];
            OrderedStreams.Remove(s);
            OrderedStreams.Add(s);
        }

        public long TotalSize => OrderedStreams.Sum(os => os.Length);

        public int TotalSegmentCount => OrderedSegments.Count;

        public KeyedStreamWriter GetStream<T> () {
            return GetStream(typeof(T).FullName);
        }

        public KeyedStreamWriter GetStream (string key) {
            KeyedStreamWriter result;
            if (Streams.TryGetValue(key, out result))
                return result;

            result = new KeyedStreamWriter(key);
            Streams[key] = result;
            OrderedStreams.Add(result);
            OrderedSegments.Add(new SegmentEntry { Stream = result, SegmentIndex = 0 });
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write (long value, KeyedStreamWriter stream = null) {
            (stream ?? LongStream).WriteLEB(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write (int value, KeyedStreamWriter stream = null) {
            (stream ?? IntStream).WriteLEB(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write (uint value, KeyedStreamWriter stream = null) {
            (stream ?? UIntStream).WriteLEB(value);
        }

        public void Write (bool value) {
            BooleanStream.Write(value ? 1 : 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write (byte b) {
            ByteStream.Write(b);
        }

        public void Write (string text, KeyedStreamWriter stream) {
            if (text == null) {
                StringLengthStream.WriteLEB((uint)0);

                return;
            }

            if (text != null) {
                var bytes = Encoding.UTF8.GetBytes(text);
                var length = (uint)(bytes.Length + 1);

                StringLengthStream.WriteLEB(length);

                stream.Write(bytes);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteArrayLength (Array array) {
            if (array == null)
                Write((uint)0, ArrayLengthStream);
            else
                Write((uint)array.Length, ArrayLengthStream);
        }

        public void SplitSegments (uint minimumSegmentSize) {
            foreach (var s in OrderedStreams) {
                if (s.CurrentSegmentLength < minimumSegmentSize)
                    continue;

                OrderedSegments.Add(new SegmentEntry {
                    Stream = s,
                    SegmentIndex = s.SegmentCount
                });
                s.CreateNewSegment();
            }
        }

        public void SaveTo (Stream output) {
            using (var writer = new BinaryWriter(output, Encoding.UTF8, true)) {
                writer.Write(Prologue);

                var savedStreamCount = OrderedStreams.Count;

                writer.Write(BoundaryMarker1);
                writer.Write(savedStreamCount);

                writer.Flush();

                var startOfHeaders = (uint)writer.BaseStream.Position;
                var headerSize = KeyedStreamWriter.HeaderSize;
                var endOfHeaders = startOfHeaders + (uint)(savedStreamCount * headerSize) + 4;

                var dataOffset = endOfHeaders;

                foreach (var s in OrderedStreams)
                    s.WriteStreamTableHeader(writer);

                var streamSummary = (from s in OrderedStreams
                                     select new {
                                         name = s.Key,
                                         length = s.Length
                                     }).ToArray();

                writer.Write(BoundaryMarker2);
                writer.Flush();

                foreach (var se in OrderedSegments) {
                    var s = se.Stream;
                    s.Flush();
                    var seg = s.Segments.ElementAt(se.SegmentIndex);
                    var streamIndex = OrderedStreams.IndexOf(s);
                    writer.Write((uint)streamIndex);
                    writer.Write((uint)seg.Index);
                    writer.Write(seg.Length);
                    // FIXME: Account for any offset and not just GetBuffer
                    using (var window = new MemoryStream(s.Stream.GetBuffer(), (int)seg.Offset, (int)seg.Length, false))
                        window.CopyTo(writer.BaseStream);

                    writer.Write(BoundaryMarker3);

                    // FIXME
                    /*
                    if (seg.Length > StreamAlignmentThreshold) {
                        var paddedLength = ((seg.Length + StreamAlignment - 1) / StreamAlignment) * StreamAlignment;
                        var paddingLength = paddedLength - seg.Length;
                        for (int i = 0; i < paddingLength; i++)
                            writer.Write((byte)0);
                    }
                    */
                }

                writer.Flush();
            }
        }
    }
}
