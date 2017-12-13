﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModuleSaw {
    public class StreamWindow : Stream {
        public readonly Stream BaseStream;

        private readonly long Offset, _Length;

        public StreamWindow (Stream baseStream, long offset, long length) {
            BaseStream = baseStream;
            if (!baseStream.CanRead || !baseStream.CanSeek)
                throw new Exception();

            Offset = offset;
            _Length = length;
        }

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override long Length => _Length;

        public override long Position { get; set; }

        public override void Flush () {
            BaseStream.Flush();
        }

        public override int Read (byte[] buffer, int offset, int count) {
            if (BaseStream.Position != Position)
                BaseStream.Position = Position;

            var readCount = BaseStream.Read(buffer, offset, count);
            Position += readCount;
            return readCount;
        }

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
    }
}
