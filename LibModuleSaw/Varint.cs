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
        public static void WriteLEB (this BinaryWriter writer, ulong value) {
            // Console.Write($"{value:X8}u: ");
            do {
                var b = (byte)(value & 0x7Ful);
                value >>= 7;

                if (value != 0)
                    b |= 0x80;

                writer.Write(b);
                // Console.Write(b.ToString("X2"));
            } while (value != 0);
            // Console.WriteLine();
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

        // HACK: Not thread-safe
        private static byte[] LEBBuffer = new byte[10];

        public unsafe static ulong? ReadLEBUInt (this BinaryReader reader) {
            var br = reader.BaseStream;
            var count = br.Read(LEBBuffer, 0, 10);
            if (count == 0)
                return null;

            fixed (byte* pBuffer = LEBBuffer) {
                var ok = ReadLEBUInt(pBuffer, (uint)count, out ulong result, out uint bytesRead);
                br.Position -= (count - bytesRead);
                if (ok)
                    return result;
                else
                    return null;
            }
        }

        public unsafe static bool ReadLEBUInt (
            byte* pBytes, uint count, 
            out ulong result, out uint bytesRead
        ) {
            result = 0;
            bytesRead = 0;
            int shift = 0;

            for (uint i = 0; i < count; i++) {
                var b = pBytes[i];
                var shifted = (ulong)(b & 0x7F) << shift;
                result |= shifted;

                if ((b & 0x80) == 0) {
                    bytesRead = i + 1;
                    return true;
                }

                shift += 7;
            }

            return false;
        }

        public unsafe static long? ReadLEBInt (this BinaryReader reader) {
            var br = reader.BaseStream;
            var count = br.Read(LEBBuffer, 0, 10);
            if (count == 0)
                return null;

            fixed (byte* pBuffer = LEBBuffer) {
                var ok = ReadLEBInt(pBuffer, (uint)count, out long result, out uint bytesRead);
                br.Position -= (count - bytesRead);
                if (ok)
                    return result;
                else
                    return null;
            }
        }
        
        public unsafe static bool ReadLEBInt (
            byte* pBytes, uint count, 
            out long result, out uint bytesRead
        ) {
            int shift = 0;
            byte b = 0;

            result = 0;
            bytesRead = 0;

            for (uint i = 0; i < count; i++) {
                b = pBytes[i];
                var shifted = (long)(b & 0x7F) << shift;
                result |= shifted;
                shift += 7;

                if ((b & 0x80) == 0) {
                    bytesRead = i + 1;
                    if ((b & 0x40) != 0)
                        result |= (((long)-1) << shift);
                    return true;
                }
            }

            return false;
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

        private static ulong SelfTestSingle (ulong l) {
            var ms = new MemoryStream();
            var bw = new BinaryWriter(ms, Encoding.UTF8, true);
            bw.WriteLEB(l);
            bw.Dispose();

            ms.Position = 0;
            var br = new BinaryReader(ms);
            var read = br.ReadLEBUInt();
            return read.Value;
        }

        private static long SelfTestSingle (long l) {
            var ms = new MemoryStream();
            var bw = new BinaryWriter(ms, Encoding.UTF8, true);
            bw.WriteLEB(l);
            bw.Dispose();

            ms.Position = 0;
            var br = new BinaryReader(ms);
            var read = br.ReadLEBInt();
            return read.Value;
        }

        public static void SelfTest () {
            var values = new long[] {
                9401, 12546, 113794, 51, 15658, 376331,
                23891164, 6249699, 8841692, 0, 1, -1,
                127, 128, -127, -128
            };
            bool failed = false;

            var ms = new MemoryStream();
            using (var bw = new BinaryWriter(ms, Encoding.UTF8, true))
            foreach (var l in values) {
                bw.WriteLEB(l);
                bw.WriteLEB((ulong)l);
            }

            ms.Position = 0;
            using (var br = new BinaryReader(ms))
            foreach (var l in values) {
                var a = br.ReadLEBInt();
                var b = br.ReadLEBUInt();

                if (a != l) {
                    Console.WriteLine("Expected {0} got {1}", l, a);
                    failed = true;
                }

                if (b != (ulong)l) {
                    Console.WriteLine("Expected {0} got {1}", (ulong)l, b);
                    failed = true;
                }
            }

            if (failed)
                throw new Exception();
        }
    }
}
