using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModuleSaw {
    public static class VarintExtensions {
        public static void WriteLEB (this BinaryWriter writer, uint value) {
            do {
                var b = value & 0x7F;
                value >>= 7;
                if (value != 0)
                    b |= 0x80;

                writer.Write((byte)b);
            } while (value != 0);
        }

        public static void WriteLEB (this BinaryWriter writer, int value) {
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

        public static uint ReadLEBUInt (this BinaryReader reader) {
            uint result = 0;
            int shift = 0;
            while (true) {
                var b = reader.ReadByte();
                var shifted = (uint)(b & 0x7F) << shift;
                result |= shifted;

                if ((b & 0x80) == 0)
                    break;

                shift += 7;
            }

            return result;
        }

        public static int ReadLEBInt (this BinaryReader reader) {
            int result = 0, shift = 0;
            byte b;

            while (true) {
                b = reader.ReadByte();
                var shifted = (b & 0x7F) << shift;
                result |= shifted;
                shift += 7;

                if ((b & 0x80) == 0)
                    break;
            }

            if ((b & 0x40) != 0)
                result |= (-1 << shift);

            return result;
        }
    }
}
