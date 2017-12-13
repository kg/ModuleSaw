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

        internal void WriteHeader (BinaryWriter output) {
            output.Write(Key);
            output.Write(Stream.Length);
        }
    }
}
