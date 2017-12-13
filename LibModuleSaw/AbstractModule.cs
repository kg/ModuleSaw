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

        public KeyedStream
            IntStream, UIntStream,
            LongStream, ULongStream,
            SByteStream, ByteStream,
            SingleStream, DoubleStream,
            BooleanStream, ArrayLengthStream,
            StringLengthStream, StringStream;

        public Configuration Configuration;

        private readonly Dictionary<string, KeyedStream> Streams = 
            new Dictionary<string, KeyedStream>(StringComparer.Ordinal);
        private readonly List<KeyedStream> OrderedStreams = new List<KeyedStream>();

        public AbstractModuleBuilder () {
            Configuration = new Configuration();

            LongStream = GetStream("u32");
            ULongStream = GetStream("u64");
            IntStream = GetStream("i32");
            UIntStream = GetStream("u32");
            SByteStream = GetStream("i8");
            ByteStream = GetStream("u8");
            SingleStream = GetStream("f32");
            DoubleStream = GetStream("f64");
            BooleanStream = GetStream("u1");
            StringLengthStream = GetStream("stringLength");
            StringStream = GetStream("string");
            ArrayLengthStream = GetStream("arrayLength");
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
        public void Write (ulong value, KeyedStream stream = null) {
            if (Configuration.Varints)
                (stream ?? ULongStream).WriteLEB(value);
            else
                (stream ?? ULongStream).Write(value);
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
        public void Write (sbyte b) {
            SByteStream.Write(b);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write (byte b) {
            ByteStream.Write(b);
        }

        public void Write (string text, KeyedStream stream = null) {
            var length = 0;
            if (text != null)
                length = text.Length + 1;

            // Write 0 if null, 1+len if non-null
            if (Configuration.Varints)
                StringLengthStream.WriteLEB(length);
            else
                StringLengthStream.Write(length);

            if ((text != null) && text.Length > 0)
                (stream ?? StringStream).Write(text.ToCharArray());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteArrayLength (Array array) {
            if (array == null)
                Write((uint)0, ArrayLengthStream);
            else
                Write((uint)array.Length + 1, ArrayLengthStream);
        }

        public void SaveTo (Stream output, string subFormat) {
            using (var writer = new BinaryWriter(output, Encoding.UTF8, true)) {
                var prologue = new[] {
                    '\x89', 'M', 'S', 'a', 'w', '\r', '\n', '\x1a', '\n', '\0'
                };
                writer.Write(prologue.Select(c => (byte)c).ToArray());

                writer.Write(subFormat);

                writer.Write(BoundaryMarker1);

                writer.Write(OrderedStreams.Count);
                foreach (var s in OrderedStreams)
                    s.WriteHeader(writer);

                writer.Write(BoundaryMarker2);

                foreach (var s in OrderedStreams) {
                    s.WriteHeader(writer);
                    writer.Flush();

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
