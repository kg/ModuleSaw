using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ModuleSaw {
    public static class VarintExtensions {
        [StructLayout(LayoutKind.Explicit)]
        private struct U {
            [FieldOffset(0)]
            public UInt64 u64;
            [FieldOffset(0)]
            public Int64 i64;
            [FieldOffset(0)]
            public UInt32 u32;
            [FieldOffset(0)]
            public Int32 i32;
        }

        public static void WriteLEB (this BinaryWriter writer, ulong value) {
            do {
                var b = (byte)(value & 0x7Ful);
                value >>= 7;

                if (value != 0)
                    b |= 0x80;

                writer.Write(b);
            } while (value != 0);
        }

        public static void WriteLEB (this BinaryWriter writer, long value) {
            bool more = true;

            while (more) {
                var b = (byte)(value & 0x7FL);
                value >>= 7;

                bool signBit = (b & 0x40u) != 0;
                if (
                    ((value == 0) && !signBit) ||
                    ((value == -1) && signBit)
                )
                    more = false;
                else
                    b |= 0x80;

                writer.Write(b);
            }
        }

        public static void WriteLEB (this BinaryWriter writer, int value) {
            WriteLEB(writer, (long)value);
        }

        public static void WriteLEB (this BinaryWriter writer, uint value) {
            WriteLEB(writer, (ulong)value);
        }

        public static ulong? ReadLEBUInt (this BinaryReader reader) {
            var br = reader.BaseStream;
            var l = br.Length;

            ulong result = 0;
            int shift = 0;
            while (true) {
                if (br.Position == l)
                    return null;

                var b = reader.ReadByte();
                var shifted = (ulong)(b & 0x7F) << shift;
                result |= shifted;

                if ((b & 0x80) == 0)
                    break;

                shift += 7;
            }

            return result;
        }

        public static long? ReadLEBInt (this BinaryReader reader) {
            var br = reader.BaseStream;
            var l = br.Length;

            long result = 0;
            int shift = 0;
            byte b;

            while (true) {
                if (br.Position >= l)
                    return null;

                b = reader.ReadByte();
                var shifted = (long)(b & 0x7F) << shift;
                result |= shifted;
                shift += 7;

                if ((b & 0x80) == 0)
                    break;
            }

            if ((b & 0x40) != 0)
                result |= (((long)-1) << shift);

            return result;
        }

        public static string ReadPString (this BinaryReader reader) {
            var length = reader.ReadLEBUInt();
            var body = reader.ReadBytes((int)length.Value);
            return Encoding.UTF8.GetString(body);
        }

        public static void WritePString (this BinaryWriter writer, string text) {
            var bytes = Encoding.UTF8.GetBytes(text);
            writer.WriteLEB((uint)bytes.Length);
            writer.Write(bytes);
        }
    }
}
