using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace ModuleSaw {
    public class AbstractModuleBuilder {
        public const uint BoundaryMarker1 = 0xDBCA1234,
            BoundaryMarker2 = 0xABCD9876,
            BoundaryMarker3 = 0x13579FCA;

        public static readonly byte[] Prologue = (new[] {
            '\x89', 'M', 'S', 'a', 'w', '\r', '\n', '\x1a', '\n', '\0'
        }).Select(c => (byte)c).ToArray();

        public KeyedStream
            IntStream, UIntStream,
            LongStream, ByteStream,
            SingleStream, DoubleStream,
            BooleanStream, ArrayLengthStream,
            StringLengthStream;

        public Configuration Configuration;

        private readonly Dictionary<string, KeyedStream> Streams = 
            new Dictionary<string, KeyedStream>(StringComparer.Ordinal);
        private readonly List<KeyedStream> OrderedStreams = new List<KeyedStream>();

        public AbstractModuleBuilder () {
            Configuration = new Configuration();

            LongStream = GetStream("i64");
            IntStream = GetStream("i32");
            UIntStream = GetStream("u32");
            ByteStream = GetStream("u8");
            SingleStream = GetStream("f32");
            DoubleStream = GetStream("f64");
            BooleanStream = GetStream("u1");
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

        public long TotalSize {
            get {
                return OrderedStreams.Sum(os => os.Length);
            }
        }

        public KeyedStream GetStream<T> () {
            return GetStream(typeof(T).FullName);
        }

        public KeyedStream GetStream (string key) {
            KeyedStream result;
            if (Streams.TryGetValue(key, out result))
                return result;

            result = new KeyedStream(key);
            Streams[key] = result;
            OrderedStreams.Add(result);
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write (long value, KeyedStream stream = null) {
            if (Configuration.Varints)
                (stream ?? LongStream).WriteLEB(value);
            else
                (stream ?? LongStream).Write(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write (int value, KeyedStream stream = null) {
            if (Configuration.Varints)
                (stream ?? IntStream).WriteLEB(value);
            else
                (stream ?? IntStream).Write(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write (uint value, KeyedStream stream = null) {
            if (Configuration.Varints)
                (stream ?? UIntStream).WriteLEB(value);
            else
                (stream ?? UIntStream).Write(value);
        }

        public void Write (bool value) {
            BooleanStream.Write(value ? 1 : 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write (byte b) {
            ByteStream.Write(b);
        }

        public void Write (string text, KeyedStream stream) {
            if (text == null) {
                if (Configuration.Varints)
                    StringLengthStream.WriteLEB((uint)0);
                else
                    StringLengthStream.Write((uint)0);

                return;
            }

            if (text != null) {
                var bytes = Encoding.UTF8.GetBytes(text);
                var length = (uint)(bytes.Length + 1);

                if (Configuration.Varints)
                    StringLengthStream.WriteLEB(length);
                else
                    StringLengthStream.Write(length);

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

        public void SaveTo (Stream output, string subFormat) {
            using (var writer = new BinaryWriter(output, Encoding.UTF8, true)) {
                writer.Write(Prologue);

                writer.Write(subFormat);

                writer.Write(BoundaryMarker1);

                writer.Write(OrderedStreams.Count);

                writer.Flush();

                long startOfHeaders = writer.BaseStream.Position;
                long headerSize = KeyedStream.HeaderSize;
                long endOfHeaders = startOfHeaders + (OrderedStreams.Count * headerSize) + 4;

                long dataOffset = endOfHeaders;

                foreach (var s in OrderedStreams) {
                    s.WriteHeader(writer, dataOffset);
                    dataOffset += s.Length + 4;
                }

                writer.Write(BoundaryMarker2);

                foreach (var s in OrderedStreams) {
                    s.Flush();
                    s.Stream.Position = 0;
                    s.Stream.CopyTo(writer.BaseStream);

                    writer.Write(BoundaryMarker3);
                }

                writer.Flush();
            }
        }
    }
}
