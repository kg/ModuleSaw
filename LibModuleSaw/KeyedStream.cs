using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModuleSaw {
    public class KeyedStream : BinaryWriter {
        public readonly string Key;
        public MemoryStream Stream { get; private set; }

        private KeyedStream (string key, MemoryStream ms)
            : base (ms, Encoding.UTF8)
        {
            Key = key;
            Stream = ms;
        }

        public KeyedStream (string key)
            : this (key, new MemoryStream()) {
        }

        internal void WriteHeader (Stream output) {
            var bytes = Encoding.UTF8.GetBytes(Key);
            output.Write(bytes, 0, bytes.Length);
            output.Write(new byte[] { 0 }, 0, 1);

            var length = (int)Stream.Length;
            output.Write(BitConverter.GetBytes(length), 0, sizeof(int));
        }
    }
}
