using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ModuleSaw;
using Wasm.Model;

namespace WasmSaw {
    public static class MsawToWasm {

        public static void Convert (Stream input, Stream output, Configuration config) {
            input.Position = 0;

            var amr = new AbstractModuleReader(input, config);
            var writer = new BinaryWriter(output, Encoding.UTF8, true);

            Program.Assert(amr.ReadHeader());

            writer.Write((uint)0x6d736100);
            writer.Write((uint)1);

            var sectionIds = amr.Open(amr.Streams["section_id"]);
            var sectionNames = amr.Open(amr.Streams["section_name"]);
            var sectionPayloadLengths = amr.Open(amr.Streams["section_payload_len"]);
            var unknownSectionData = amr.Open(amr.Streams["unknown_section_data"]);

            var sectionCount = (uint)sectionIds.Length;

            for (int i = 0; i < sectionCount; i++) {
                var id = (SectionTypes)sectionIds.ReadSByte();
                string name = null;
                if (id == 0)
                    name = sectionNames.ReadString();
                var payload_len = sectionPayloadLengths.ReadUInt32();

                Console.WriteLine("{0} {1}", id, payload_len);

                writer.Write((sbyte)id);
                writer.WriteLEB(payload_len);
                return;

                var actualPayloadSize = payload_len;

                if (id == 0) {
                    writer.Flush();
                    var initOffset = writer.BaseStream.Position;

                    writer.WritePString(name);

                    writer.Flush();
                    var lastOffset = writer.BaseStream.Position;

                    actualPayloadSize -= (uint)(lastOffset - initOffset);
                }

                if (((sbyte)id <= 0) || ((sbyte)id >= (sbyte)SectionTypes.Unknown)) {
                    // FIXME: Use CopyTo?
                    writer.Write(unknownSectionData.ReadBytes((int)actualPayloadSize));
                } else {
                    DecodeSectionBody(amr, writer, (SectionTypes)id, actualPayloadSize);
                }
            }

            writer.Dispose();
        }

        private static void DecodeSectionBody (
            AbstractModuleReader amr, BinaryWriter writer, 
            SectionTypes id, uint payload_len
        ) {
            var td = new TypeDecoders(amr);
            switch (id) {
                case SectionTypes.Type:
                    DecodeTypeSection(amr, writer, td);
                    break;

                case SectionTypes.Import:
                    DecodeImportSection(amr, writer, td);
                    break;

                default:
                    Console.WriteLine("Not implemented: {0}", id);

                    for (int i = 0; i < payload_len; i++)
                        writer.Write((byte)id);
                    break;
            }


            // FIXME
        }

        private static void DecodeTypeSection (
            AbstractModuleReader amr, BinaryWriter writer, TypeDecoders td
        ) {
            var count = amr.ReadArrayLength();
            writer.WriteLEB(count);

            for (uint i = 0; i < count; i++) {
                var item = td.func_type();
                writer.Write((byte)0x60);

                writer.WriteLEB((uint)item.param_types.Length);
                foreach (var pt in item.param_types)
                    writer.Write((byte)pt);

                writer.Write(item.return_type != 0);
                if (item.return_type != 0)
                    writer.Write((byte)item.return_type);
            }
        }

        private static void DecodeResizableLimits (
            BinaryWriter writer, resizable_limits rl
        ) {
            writer.Write(rl.flags);
            writer.WriteLEB(rl.initial);
            if (rl.flags != 0)
                writer.WriteLEB(rl.maximum);
        }

        private static void DecodeTableType (
            BinaryWriter writer, table_type tt
        ) {
            writer.Write((byte)tt.element_type);
            DecodeResizableLimits(writer, tt.limits);
        }

        private static void DecodeGlobalType (
            BinaryWriter writer, global_type gt
        ) {
            writer.Write((byte)gt.content_type);
            writer.Write(gt.mutability);
        }

        private static void DecodeImportSection (
            AbstractModuleReader amr, BinaryWriter writer, TypeDecoders td
        ) {
            var count = amr.ReadArrayLength();
            writer.WriteLEB(count);

            for (uint i = 0; i < count; i++) {
                var item = td.import_entry();
                writer.WritePString(item.module);
                writer.WritePString(item.field);
                writer.Write((byte)item.kind);

                switch (item.kind) {
                    case external_kind.Function:
                        writer.WriteLEB(item.type.Function);
                        break;
                    case external_kind.Table:
                        DecodeTableType(writer, item.type.Table);
                        break;
                    case external_kind.Memory:
                        DecodeResizableLimits(writer, item.type.Memory.limits);
                        break;
                    case external_kind.Global:
                        DecodeGlobalType(writer, item.type.Global);
                        break;
                }
            }
        }
    }
}
