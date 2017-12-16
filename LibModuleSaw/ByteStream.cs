using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ModuleSaw {
    public unsafe class ArrayBinaryReader : IDisposable {
        public readonly ArraySegment<byte> Data;
        public readonly uint Length;

        private GCHandle Pin;
        private byte* pData;
        private byte* pStart, pEnd;

        public bool IsDisposed { get; private set; }

        public ArrayBinaryReader (ArraySegment<byte> data, uint? initialPosition = null, uint? length = null) {
            Data = data;
            Pin = GCHandle.Alloc(Data.Array, GCHandleType.Pinned);
            Length = length.GetValueOrDefault((uint)data.Count);
            pStart = ((byte*)Pin.AddrOfPinnedObject()) + data.Offset;
            pEnd = pData + Length;
            pData = pStart + initialPosition.GetValueOrDefault(0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ArrayBinaryReader (byte[] data, uint offset, uint length)
            : this (new ArraySegment<byte>(data, (int)offset, (int)length)) {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ArrayBinaryReader (byte[] data)
            : this (new ArraySegment<byte>(data)) {
        }

        public uint Position {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get {
                return (uint)(pData - pStart);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool SetPosition (uint offsetFromBeginning) {
            var p = pStart + offsetFromBeginning;
            if (p > pEnd)
                return false;

            pData = p;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Seek (int offset) {
            var p = pData + offset;
            if ((p < pStart) || (p > pEnd))
                return false;

            pData = p;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Read (out byte result) {
            if (pData >= pEnd) {
                result = 0;
                return false;
            } else {
                result = *(pData++);
                return true;
            }
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct ReadUnion<T> {
            [FieldOffset(0)]
            public fixed byte buffer[128];
            [FieldOffset(0)]
            public T value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]        
        public bool Read<T> (out T result)
            where T : struct {
            var u = default(ReadUnion<T>);
            var size = Marshal.SizeOf<T>();

            if (
                (size > 128) || 
                !Read(u.buffer, (uint)size)
            ) {
                result = default(T);
                return false;
            }

            result = u.value;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Read (byte[] resultBuffer, uint? length = null) {
            fixed (byte* pResult = resultBuffer)
                return Read(pResult, length.GetValueOrDefault((uint)resultBuffer.Length));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Read (void* resultBuffer, uint length) {
            var p = pData;
            pData += length;

            if (pData > pEnd) {
                pData = p;
                return false;
            }

            var pResult = (byte*)resultBuffer;
            for (uint i = 0; i < length; i++)
                pResult[i] = p[i];

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Chomp (out byte* data, uint length) {
            data = null;
            var p = pData + length;
            if (p > pEnd)
                return false;

            data = pData;
            pData += length;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ReadI32LEB (out int result) {
            var remaining = pEnd - pData;
            if (remaining <= 0) {
                result = 0;
                return false;
            }

            var ok = VarintExtensions.ReadLEBInt(
                pData, (uint)remaining, out long temp, out uint bytesRead
            );
            if (ok)
                result = (int)temp;
            else
                result = 0;
            pData += bytesRead;
            return ok;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ReadU32LEB (out uint result) {
            var remaining = pEnd - pData;
            if (remaining <= 0) {
                result = 0;
                return false;
            }

            var ok = VarintExtensions.ReadLEBUInt(
                pData, (uint)remaining, out ulong temp, out uint bytesRead
            );
            if (ok)
                result = (uint)temp;
            else
                result = 0;
            pData += bytesRead;
            return ok;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ReadI64LEB (out long result) {
            var remaining = pEnd - pData;
            if (remaining <= 0) {
                result = 0;
                return false;
            }

            var ok = VarintExtensions.ReadLEBInt(
                pData, (uint)remaining, out result, out uint bytesRead
            );
            pData += bytesRead;
            return ok;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ReadU64LEB (out ulong result) {
            var remaining = pEnd - pData;
            if (remaining <= 0) {
                result = 0;
                return false;
            }

            var ok = VarintExtensions.ReadLEBUInt(
                pData, (uint)remaining, out result, out uint bytesRead
            );
            pData += bytesRead;
            return ok;
        }

        public bool CopyTo (Stream destination, uint length) {
            if (!Chomp(out byte* temp, length))
                return false;

            destination.Write(Data.Array, (int)(Data.Offset + (temp - pStart)), (int)length);
            return true;
        }

        public bool CopyTo (BinaryWriter destination, uint length) {
            if (!Chomp(out byte* temp, length))
                return false;

            destination.Write(Data.Array, (int)(Data.Offset + (temp - pStart)), (int)length);
            return true;
        }

        public void Dispose () {
            if (IsDisposed)
                return;

            IsDisposed = true;
            Pin.Free();
            // HACK so all reads abort
            pData = pEnd + 1;
        }
    }
}
