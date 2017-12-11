using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModuleSaw {
    public class AbstractModuleBuilder {
        private KeyedStream
            IntStream, UIntStream, 
            LongStream, ULongStream,
            BooleanStream, TypeIdStream;

        public Configuration Configuration;

        private readonly Dictionary<string, KeyedStream> Streams = 
            new Dictionary<string, KeyedStream>(StringComparer.Ordinal);

        public AbstractModuleBuilder () {
            Configuration = new Configuration();

            LongStream = GetStream<long>();
            ULongStream = GetStream<ulong>();
            IntStream = GetStream<int>();
            UIntStream = GetStream<uint>();
            BooleanStream = GetStream<bool>();
            TypeIdStream = GetStream("typeId");
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
            return result;
        }

        public void Write (long value) {
            if (Configuration.Varints)
                LongStream.WriteLEB(value);
            else
                LongStream.Write(value);
        }

        public void Write (ulong value) {
            if (Configuration.Varints)
                ULongStream.WriteLEB(value);
            else
                ULongStream.Write(value);
        }

        public void Write (int value) {
            if (Configuration.Varints)
                IntStream.WriteLEB(value);
            else
                IntStream.Write(value);
        }

        public void Write (uint value, bool disableUints = false) {
            // FIXME: Separate stream for never-LEB uints?
            if (Configuration.Varints && !disableUints)
                UIntStream.WriteLEB(value);
            else
                UIntStream.Write(value);
        }

        public void Write (bool value) {
            BooleanStream.Write(value ? 1 : 0);
        }

        public void Write<T> (ref T value) {
            var schema = Configuration.GetSchema<T>();
            schema.Write(this, ref value);
        }

        public void Write<T> (T value) {
            Write(ref value);
        }

        public void SaveTo (Stream output) {
            // TODO: Header

            foreach (var kvp in Streams) {
                var bytes = Encoding.UTF8.GetBytes(kvp.Key);
                output.Write(bytes, 0, bytes.Length);

                var s = kvp.Value.Stream;
                var length = (int)s.Length;

                output.Write(BitConverter.GetBytes(length), 0, sizeof(int));

                bytes = s.GetBuffer();
                output.Write(bytes, 0, length);
            }
        }
    }
}
