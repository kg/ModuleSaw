using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModuleSaw {
    public static class VarintExtensions {
        public static void WriteLEB (this BinaryWriter writer, ulong value) {
            do {
                var b = value & 0x7F;
                value >>= 7;
                if (value != 0)
                    b |= 0x80;

                writer.Write((byte)b);
            } while (value != 0);
        }

        public static void WriteLEB (this BinaryWriter writer, long value) {
            do {
                var b = value & 0x7F;
                value >>= 7;

                var signBit = (b & 0x40) != 0;

                if (
                    ((value == 0) && !signBit) ||
                    ((value == -1) && signBit)
                ) {
                    writer.Write((byte)b);
                    break;
                } else {
                    b |= 0x80;
                    writer.Write((byte)b);
                }
            } while (true);
        }

        public static void WriteLEB (this BinaryWriter writer, int value) {
            WriteLEB(writer, (long)value);
        }

        public static void WriteLEB (this BinaryWriter writer, uint value) {
            WriteLEB(writer, (ulong)value);
        }

        public static ulong ReadLEBUInt (this BinaryReader reader) {
            ulong result = 0;
            int shift = 0;
            while (true) {
                var b = reader.ReadByte();
                var shifted = (ulong)(b & 0x7F) << shift;
                result |= shifted;

                if ((b & 0x80) == 0)
                    break;

                shift += 7;
            }

            return result;
        }

        public static long ReadLEBInt (this BinaryReader reader) {
            long result = 0;
            int shift = 0;
            byte b;

            while (true) {
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
    }
}
