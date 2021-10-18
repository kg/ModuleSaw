using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ModuleSaw {
    public static class StreamExtensions {
        static ThreadLocal<byte[]> Buf = new ThreadLocal<byte[]>(() => new byte[1]);

        public static byte ReadByteButFast (this Stream s) {
            if (s is StreamWindow)
                return (byte)s.ReadByte();

            var buf = Buf.Value;
            if (s.Read(buf) != 1)
                throw new EndOfStreamException($"Hit end of stream while reading byte from {s}");
            return buf[0];
        }

        public static byte ReadByteButFast (this BinaryReader br) {
            var buf = Buf.Value;
            if (br.Read(buf) != 1)
                throw new EndOfStreamException($"Hit end of stream while reading byte from {br}");
            return buf[0];
        }
    }

    public class StreamWindow : Stream {
        public readonly Stream BaseStream;
        public readonly long OriginalPosition;

        private readonly long Offset, _Length;
        private long _Position;
        private byte[] ByteBuf = new byte[1];

        public StreamWindow (Stream baseStream, long offset, long length) {
            BaseStream = baseStream;
            if (!baseStream.CanRead || !baseStream.CanSeek)
                throw new Exception();

            OriginalPosition = baseStream.Position;
            Offset = offset;
            _Length = length;
        }

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override long Length => _Length;

        public override long Position {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _Position;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => _Position = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Flush () {
            throw new InvalidOperationException();
        }

        public override int ReadByte () {
            if (Read(ByteBuf, 0, 1) != 1)
                return -1;
            else
                return ByteBuf[0];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int Read (byte[] buffer, int offset, int count) {
            if (BaseStream.Position != _Position)
                BaseStream.Position = _Position + Offset;

            var actualCount = (int)Math.Min(count, (_Length - Position));
            if (actualCount <= 0)
                return 0;

            var readCount = BaseStream.Read(buffer, offset, actualCount);
            Position += readCount;
            return readCount;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override long Seek (long offset, SeekOrigin origin) {
            // FIXME: EOF
            switch (origin) {
                case SeekOrigin.Begin:
                    return Position = offset;
                case SeekOrigin.Current:
                    return (Position += offset);
                case SeekOrigin.End:
                    return Position = (_Length - offset);
                default:
                    throw new ArgumentException();
            }
        }

        public override void SetLength (long value) {
            throw new InvalidOperationException();
        }

        public override void Write (byte[] buffer, int offset, int count) {
            throw new InvalidOperationException();
        }

        public override void Close () {
            BaseStream.Position = OriginalPosition;
            base.Close();
        }

        protected override void Dispose (bool disposing) {
            BaseStream.Position = OriginalPosition;
            base.Dispose(disposing);
        }
    }
}
