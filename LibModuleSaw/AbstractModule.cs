using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModuleSaw {
    public class AbstractModuleBuilder {
        internal KeyedStream
            IntStream, UIntStream,
            LongStream, ULongStream,
            SByteStream, ByteStream,
            BooleanStream, TypeIdStream,
            StringLengthStream, StringStream,
            ArrayLengthStream;

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
            BooleanStream = GetStream("u1");
            TypeIdStream = GetStream("typeId");
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

        public void WriteValue (long value, KeyedStream stream = null) {
            if (Configuration.Varints)
                (stream ?? LongStream).WriteLEB(value);
            else
                (stream ?? LongStream).Write(value);
        }

        public void WriteValue (ulong value, KeyedStream stream = null) {
            if (Configuration.Varints)
                (stream ?? ULongStream).WriteLEB(value);
            else
                (stream ?? ULongStream).Write(value);
        }

        public void WriteValue (int value, KeyedStream stream = null) {
            if (Configuration.Varints)
                (stream ?? IntStream).WriteLEB(value);
            else
                (stream ?? IntStream).Write(value);
        }

        public void WriteValue (uint value, KeyedStream stream = null, bool disableUints = false) {
            // FIXME: Separate stream for never-LEB uints?
            if (Configuration.Varints && !disableUints)
                (stream ?? UIntStream).WriteLEB(value);
            else
                (stream ?? UIntStream).Write(value);
        }

        public void WriteValue (bool value) {
            BooleanStream.Write(value ? 1 : 0);
        }

        public void WriteValue (sbyte b) {
            SByteStream.Write(b);
        }

        public void WriteValue (byte b) {
            ByteStream.Write(b);
        }

        public void WriteString (string text) {
            var length = 0;
            if (text != null)
                length = text.Length + 1;

            // Write 0 if null, 1+len if non-null
            if (Configuration.Varints)
                StringLengthStream.WriteLEB(length);
            else
                StringLengthStream.Write(length);

            if ((text != null) && text.Length > 0)
                StringStream.Write(text.ToCharArray());
        }

        public void WriteArrayLength (Array array) {
            var length = 0;
            if (array != null)
                length = array.Length + 1;

            if (Configuration.Varints)
                ArrayLengthStream.WriteLEB(length);
            else
                ArrayLengthStream.Write(length);
        }

        public void SaveTo (Stream output) {
            var zero = new byte[1];
            // TODO: Header

            foreach (var s in OrderedStreams) {
                var bytes = Encoding.UTF8.GetBytes(s.Key);
                output.Write(bytes, 0, bytes.Length);
                output.Write(zero, 0, 1);

                var length = (int)s.Stream.Length;

                output.Write(BitConverter.GetBytes(length), 0, sizeof(int));

                bytes = s.Stream.GetBuffer();
                output.Write(bytes, 0, length);
            }
        }
    }
}
