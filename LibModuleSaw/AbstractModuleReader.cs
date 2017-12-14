﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModuleSaw {
    public class AbstractModuleReader {
        public struct StreamHeader {
            public string Key;
            public long   Offset;
            public long   Length;
        }

        public class StreamList : IEnumerable<StreamHeader> {
            internal StreamHeader[] Headers;
            internal Dictionary<string, StreamHeader> Table =
                new Dictionary<string, StreamHeader>(StringComparer.Ordinal);

            internal StreamList () {
            }

            public StreamHeader this [int index] {
                get {
                    return Headers[index];
                }
            }

            public StreamHeader this [string key] {
                get {
                    return Table[key];
                }
            }

            public int Count {
                get {
                    return Headers.Length;
                }
            }

            public IEnumerator<StreamHeader> GetEnumerator () {
                return ((IEnumerable<StreamHeader>)Headers).GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator () {
                return ((IEnumerable<StreamHeader>)Headers).GetEnumerator();
            }
        }

        public readonly BinaryReader Reader;
        private readonly MemoryStream BaseStream;
        private readonly byte[] Bytes;

        public Configuration Configuration;

        public readonly StreamList Streams = new StreamList();

        public string SubFormat { get; private set; }

        private AbstractModuleStreamReader
            IntStream, UIntStream,
            LongStream, ULongStream,
            SByteStream, ByteStream,
            SingleStream, DoubleStream,
            BooleanStream, ArrayLengthStream,
            StringLengthStream, StringStream;

        public AbstractModuleReader (Stream input, Configuration configuration) {
            Configuration = configuration;

            // This sucks :(
            Bytes = new byte[input.Length];
            input.Read(Bytes, 0, (int)input.Length);

            BaseStream = new MemoryStream(Bytes, false);
            Reader = new AbstractModuleStreamReader(Configuration, BaseStream);
        }

        public AbstractModuleStreamReader Open (StreamHeader header) {
            var stream = new MemoryStream(Bytes, (int)header.Offset, (int)header.Length, false);
            return new AbstractModuleStreamReader(Configuration, stream);
        }

        private bool ReadPrologue () {
            var buffer = Reader.ReadBytes(AbstractModuleBuilder.Prologue.Length);
            return buffer.SequenceEqual(AbstractModuleBuilder.Prologue);
        }
        
        public bool ReadHeader () {
            // Access base methods
            var br = (BinaryReader)Reader;

            if (!ReadPrologue())
                return false;

            SubFormat = br.ReadString();

            if (br.ReadUInt32() != AbstractModuleBuilder.BoundaryMarker1)
                return false;

            var streamCount = br.ReadInt32();
            var headers = new StreamHeader[streamCount];
            var keyBuffer = new byte[KeyedStream.MaxKeyLength];
            Streams.Table.Clear();

            for (int i = 0; i < streamCount; i++) {
                br.Read(keyBuffer, 0, keyBuffer.Length);

                headers[i] = new StreamHeader {
                    Key = Encoding.UTF8.GetString(keyBuffer, 0, Array.IndexOf(keyBuffer, (byte)0)),
                    Offset = br.ReadInt64(),
                    Length = br.ReadInt64()
                };

                Streams.Table.Add(headers[i].Key, headers[i]);
            }

            Streams.Headers = headers;

            if (Reader.ReadUInt32() != AbstractModuleBuilder.BoundaryMarker2)
                return false;

            PreopenStreams();

            return true;
        }

        private void PreopenStreams () {
            IntStream = Open(Streams["i32"]);
            UIntStream = Open(Streams["u32"]);
            LongStream = Open(Streams["i64"]);
            ULongStream = Open(Streams["u64"]);
            SByteStream = Open(Streams["i8"]);
            ByteStream = Open(Streams["u8"]);
            SingleStream = Open(Streams["f32"]);
            DoubleStream = Open(Streams["f64"]);
            BooleanStream = Open(Streams["u1"]);
            ArrayLengthStream = Open(Streams["arrayLength"]);
            StringLengthStream = Open(Streams["stringLength"]);
            StringStream = Open(Streams["string"]);
        }

        public string ReadString (AbstractModuleStreamReader stream = null) {
            var lengthPlusOne = StringLengthStream.ReadInt32();
            if (lengthPlusOne == 0)
                return null;
            else if (lengthPlusOne == 1)
                return "";

            var chars = (stream ?? StringStream).ReadChars(lengthPlusOne - 1);
            return new string(chars);
        }
    }

    public class AbstractModuleStreamReader : BinaryReader {
        public readonly Configuration Configuration;

        public AbstractModuleStreamReader (Configuration configuration, Stream input) 
            : base(input, Encoding.UTF8, false) 
        {
            Configuration = configuration;
        }

        public long Length {
            get => BaseStream.Length;
        }

        new public int ReadInt32 () {
            if (Configuration.Varints)
                return (int)this.ReadLEBInt();
            else
                return base.ReadInt32();
        }

        new public uint ReadUInt32 () {
            if (Configuration.Varints)
                return (uint)this.ReadLEBUInt();
            else
                return base.ReadUInt32();
        }

        new public long ReadInt64 () {
            if (Configuration.Varints)
                return (long)this.ReadLEBInt();
            else
                return base.ReadInt64();
        }

        new public ulong ReadUInt64 () {
            if (Configuration.Varints)
                return (ulong)this.ReadLEBUInt();
            else
                return base.ReadUInt64();
        }
    }
}
