using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModuleSaw {
    public class KeyedStream : BinaryWriter {
        public const uint MaxKeyLength = 48;
        public const uint HeaderSize = (sizeof(long) * 2) + MaxKeyLength;

        public readonly string Key;
        public MemoryStream Stream { get; private set; }

        private KeyedStream (string key, MemoryStream ms)
            : base (ms, Encoding.UTF8)
        {
            if (Encoding.UTF8.GetByteCount(key) > MaxKeyLength)
                throw new ArgumentException("Key too long");

            Key = key;
            Stream = ms;
        }

        public KeyedStream (string key)
            : this (key, new MemoryStream()) {
        }

        public long Length => Stream.Length;

        internal unsafe void WriteHeader (BinaryWriter output, long offsetOfData) {
            var keyBuffer = new byte[MaxKeyLength];
            int _;
            bool ok;
            
            fixed (char * pKey = Key)
            fixed (byte * pBytes = keyBuffer) {
                Encoding.UTF8.GetEncoder().Convert(pKey, Key.Length, pBytes, keyBuffer.Length, true, out _, out _, out ok);
            }

            output.Write(keyBuffer);
            output.Write(offsetOfData);
            output.Write(Stream.Length);
        }
    }
}
