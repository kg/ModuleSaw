using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModuleSaw {
    public class KeyedStreamWriter : BinaryWriter {
        public struct Segment {
            public int Index;
            public long Offset;
            public long Length;
        }

        public const uint MaxKeyLength = 48;
        public const uint HeaderSize = (sizeof(uint) * 2) + MaxKeyLength;

        private readonly List<long> SegmentStartOffsets = new List<long> { 0 };

        public readonly string Key;
        public MemoryStream Stream { get; private set; }

        private KeyedStreamWriter (string key, MemoryStream ms)
            : base (ms, Encoding.UTF8)
        {
            if (Encoding.UTF8.GetByteCount(key) > MaxKeyLength)
                throw new ArgumentException("Key too long");

            Key = key;
            Stream = ms;
        }

        public KeyedStreamWriter (string key)
            : this (key, new MemoryStream()) {
        }

        public long Length => Stream.Length;
        public int SegmentCount => SegmentStartOffsets.Count;
        public long CurrentSegmentLength => Stream.Length - SegmentStartOffsets.Last();

        private Segment GetSegment (int index) {
            var segmentOffset = SegmentStartOffsets[index];
            long segmentEndOffset;
            if (index < SegmentStartOffsets.Count - 1)
                segmentEndOffset = SegmentStartOffsets[index + 1];
            else
                segmentEndOffset = Stream.Length;

            return new Segment {
                Index = index,
                Offset = segmentOffset,
                Length = segmentEndOffset - segmentOffset
            };
        }

        public IEnumerable<Segment> Segments {
            get {
                for (int i = 0; i < SegmentCount; i++)
                    yield return GetSegment(i);
            }
        }

        internal unsafe void WriteStreamName (BinaryWriter output) {
            var keyBuffer = new byte[MaxKeyLength];
            int _;
            bool ok;
            
            fixed (char * pKey = Key)
            fixed (byte * pBytes = keyBuffer) {
                Encoding.UTF8.GetEncoder().Convert(pKey, Key.Length, pBytes, keyBuffer.Length, true, out _, out _, out ok);
            }

            output.Write(keyBuffer);
        }

        internal void WriteStreamTableHeader (BinaryWriter output) {
            WriteStreamName(output);
            output.Write(SegmentStartOffsets.Count);
            output.Write(Stream.Length);
        }

        internal void CreateNewSegment () {
            // Console.WriteLine($"Split {Key} at at length {CurrentSegmentLength}");
            SegmentStartOffsets.Add(Length);
        }
    }
}
