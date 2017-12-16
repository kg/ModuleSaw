﻿using System;
using System.Collections.Generic;
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
            pStart = (byte*)Pin.AddrOfPinnedObject();
            pEnd = pData + Length;
            pData = pStart + initialPosition.GetValueOrDefault(0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ArrayBinaryReader (byte[] data, uint offset, uint length)
            : this (new ArraySegment<byte>(data, (int)offset, (int)length)) {
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
        public bool ReadI32 (out int result) {
            fixed (int* pResult = &result)
                return Read(pResult, sizeof(int));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ReadU32 (out uint result) {
            fixed (uint* pResult = &result)
                return Read(pResult, sizeof(uint));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ReadF32 (out float result) {
            fixed (float* pResult = &result)
                return Read(pResult, sizeof(float));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ReadI64 (out long result) {
            fixed (long* pResult = &result)
                return Read(pResult, sizeof(long));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ReadU64 (out ulong result) {
            fixed (ulong* pResult = &result)
                return Read(pResult, sizeof(ulong));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ReadF64 (out double result) {
            fixed (double* pResult = &result)
                return Read(pResult, sizeof(double));
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
