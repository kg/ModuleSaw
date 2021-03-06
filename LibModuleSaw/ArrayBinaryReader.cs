﻿using System;
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
        public uint AvailableLength { get; private set; }

        private GCHandle Pin;
        private byte* pData;
        private byte* pStart, pEnd;

        public bool IsDisposed { get; private set; }

        public ArrayBinaryReader (
            ArraySegment<byte> data, 
            uint initialPosition, uint length, uint availableLength
        ) {
            Data = data;
            Pin = GCHandle.Alloc(Data.Array, GCHandleType.Pinned);
            Length = length;
            AvailableLength = availableLength;
            pStart = ((byte*)Pin.AddrOfPinnedObject()) + data.Offset;
            pEnd = pStart + AvailableLength;
            pData = pStart + initialPosition;
        }

        public void SetAvailableLength (uint newAvailableLength) {
            AvailableLength = newAvailableLength;
            pEnd = pStart + newAvailableLength;
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Read (out sbyte result) {
            if (pData >= pEnd) {
                result = 0;
                return false;
            } else {
                result = *(sbyte*)(pData++);
                return true;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Read (out bool result) {
            if (pData >= pEnd) {
                result = false;
                return false;
            } else {
                result = *(pData++) != 0;
                return true;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]        
        public bool Read (out uint result) {
            fixed (uint* p = &result)
                return Read(p, sizeof(uint));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]        
        public bool Read (out ulong result) {
            fixed (ulong* p = &result)
                return Read(p, sizeof(ulong));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]        
        public bool Read (out int result) {
            fixed (int* p = &result)
                return Read(p, sizeof(int));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]        
        public bool Read (out long result) {
            fixed (long* p = &result)
                return Read(p, sizeof(long));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]        
        public bool Read (out float result) {
            fixed (float* p = &result)
                return Read(p, sizeof(float));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]        
        public bool Read (out double result) {
            fixed (double* p = &result)
                return Read(p, sizeof(double));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Read (byte[] resultBuffer, uint? length = null) {
            fixed (byte* pResult = resultBuffer)
                return Read(pResult, length.GetValueOrDefault((uint)resultBuffer.Length));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Read (byte[] resultBuffer, uint destinationOffset, uint length) {
            if (length == 0)
                return true;

            fixed (byte* pResult = &resultBuffer[destinationOffset])
                return Read(pResult, length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Read (void* resultBuffer, uint length) {
            if (length == 0)
                return true;

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
