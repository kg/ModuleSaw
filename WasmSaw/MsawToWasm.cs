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
        public static void Convert (Stream input, Stream output) {
            input.Position = 0;

            var amr = new AbstractModuleReader(input);
            var writer = new BinaryWriter(output, Encoding.UTF8, true);

            Program.Assert(amr.ReadHeader());

            writer.Write((uint)0x6d736100);
            writer.Write((uint)1);

            var sectionIds = amr.Open(amr.Streams["section_id"]);
            var sectionNames = amr.Open(amr.Streams["section_name"]);

            var sectionCount = sectionIds.Length;

            using (var scratchBuffer = new MemoryStream(256000))
            using (var scratchWriter = new BinaryWriter(scratchBuffer, Encoding.UTF8))
            for (int i = 0; i < sectionCount; i++) {
                scratchBuffer.SetLength(0);
                
                Check(sectionIds.Read(out sbyte b));
                var id = (SectionTypes)b;
                
                string name = null;
                if (id == 0) {
                    Check(sectionNames.ReadU32LEB(out uint nameLength));
                    var nameBytes = new byte[nameLength];
                    Check(sectionNames.Read(nameBytes));
                    name = Encoding.UTF8.GetString(nameBytes);
                }

                writer.Write((sbyte)id);

                if (((sbyte)id <= 0) || ((sbyte)id >= (sbyte)SectionTypes.Unknown)) {
                    var unknownSectionLengths = amr.Open(amr.Streams["unknown_section_length"]);
                    var unknownSectionData = amr.Open(amr.Streams["unknown_section_data"]);

                    Check(unknownSectionLengths.ReadU32LEB(out uint length));
                    
                    scratchWriter.Flush();
                    unknownSectionData.CopyTo(scratchWriter.BaseStream, length);
                } else {
                    EmitSectionBody(amr, scratchWriter, id);
                    scratchWriter.Flush();
                    writer.WriteLEB((uint)scratchBuffer.Position);
                    writer.Flush();
                }

                Console.WriteLine("{0}: Wrote {1} bytes", (id == 0) ? name : id.ToString(), scratchBuffer.Position);
                scratchBuffer.Position = 0;
                scratchBuffer.CopyTo(writer.BaseStream);
            }

            writer.Dispose();
        }

        private static void Check (bool b) {
            if (!b)
                throw new EndOfStreamException();
        }

        private static void EmitSectionBody (
            AbstractModuleReader amr, BinaryWriter writer, 
            SectionTypes id
        ) {
            var td = new TypeDecoders(amr);
            switch (id) {
                case SectionTypes.Type:
                    EmitTypeSection(amr, writer, td);
                    break;

                case SectionTypes.Import:
                    EmitImportSection(amr, writer, td);
                    break;

                case SectionTypes.Function:
                    EmitFunctionSection(amr, writer, td);
                    break;

                case SectionTypes.Global:
                    EmitGlobalSection(amr, writer, td);
                    break;

                case SectionTypes.Export:
                    EmitExportSection(amr, writer, td);
                    break;

                case SectionTypes.Element:
                    EmitElementSection(amr, writer, td);
                    break;

                case SectionTypes.Code:
                    EmitCodeSection(amr, writer, td);
                    break;

                case SectionTypes.Data:
                    EmitDataSection(amr, writer, td);
                    break;

                default:
                    Console.WriteLine("Not implemented: {0}", id);
                    break;
            }
        }

        private static void EmitTypeSection (
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

        private static void EmitResizableLimits (
            BinaryWriter writer, resizable_limits rl
        ) {
            writer.Write(rl.flags);
            writer.WriteLEB(rl.initial);
            if (rl.flags != 0)
                writer.WriteLEB(rl.maximum);
        }

        private static void EmitTableType (
            BinaryWriter writer, table_type tt
        ) {
            writer.Write((byte)tt.element_type);
            EmitResizableLimits(writer, tt.limits);
        }

        private static void EmitGlobalType (
            BinaryWriter writer, global_type gt
        ) {
            writer.Write((byte)gt.content_type);
            writer.Write(gt.mutability);
        }

        private static void EmitImportSection (
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
                        EmitTableType(writer, item.type.Table);
                        break;
                    case external_kind.Memory:
                        EmitResizableLimits(writer, item.type.Memory.limits);
                        break;
                    case external_kind.Global:
                        EmitGlobalType(writer, item.type.Global);
                        break;
                }
            }
        }

        private static void EmitFunctionSection (
            AbstractModuleReader amr, BinaryWriter writer, TypeDecoders td
        ) {
            var count = amr.ReadArrayLength();
            writer.WriteLEB(count);

            var indices = amr.Open(amr.Streams["function_index"]);

            for (uint i = 0; i < count; i++) {
                Check(indices.ReadU32LEB(out uint index));
                writer.WriteLEB(index);
            }
        }

        private static void EmitGlobalSection (
            AbstractModuleReader amr, BinaryWriter writer, TypeDecoders td
        ) {
            var count = amr.ReadArrayLength();
            writer.WriteLEB(count);

            for (uint i = 0; i < count; i++) {
                var item = td.global_variable();
                EmitGlobalType(writer, item.type);
                EmitInitExpression(writer, ref item.init);
            }
        }

        private static uint EmitInitExpression (
            BinaryWriter writer, ref Expression e
        ) {
            uint _2 = 0;
            EmitExpression(writer, ref e, ref _2);
            writer.Write((byte)Opcodes.end);
            return _2;
        }
        
        private static void EmitExpression (
            BinaryWriter writer, 
            ref Expression e, 
            ref uint numEmitted
        ) {
            writer.Write((byte)e.Opcode);
            numEmitted++;

            switch (e.Body.Type & ~ExpressionBody.Types.children) {
                case ExpressionBody.Types.none:
                    break;

                case ExpressionBody.Types.u32:
                    writer.WriteLEB(e.Body.U.u32);
                    break;
                case ExpressionBody.Types.u1:
                    writer.Write((byte)e.Body.U.u32);
                    break;
                case ExpressionBody.Types.i64:
                    writer.WriteLEB(e.Body.U.i64);
                    break;
                case ExpressionBody.Types.i32:
                    writer.WriteLEB(e.Body.U.i32);
                    break;
                case ExpressionBody.Types.f64:
                    writer.Write(e.Body.U.f64);
                    break;
                case ExpressionBody.Types.f32:
                    writer.Write(e.Body.U.f32);
                    break;
                case ExpressionBody.Types.memory:
                    writer.WriteLEB(e.Body.U.memory.flags);
                    writer.WriteLEB(e.Body.U.memory.offset);
                    break;
                case ExpressionBody.Types.type:
                    writer.Write((byte)e.Body.U.type);
                    break;
                case ExpressionBody.Types.br_table:
                    writer.WriteLEB((uint)e.Body.br_table.target_table.Length);
                    foreach (var t in e.Body.br_table.target_table)
                        writer.WriteLEB(t);
                    writer.WriteLEB(e.Body.br_table.default_target);

                    break;

                default:
                    throw new Exception("Not implemented");
            }

            // HACK
            if (e.Opcode == Opcodes.call_indirect)
                writer.Write((byte)0);

            if (e.Body.children != null) {
                Expression c;
                for (int i = 0; i < e.Body.children.Count; i++) {
                    c = e.Body.children[i];
                    EmitExpression(writer, ref c, ref numEmitted);
                }
            }
        }

        private static void EmitExportSection (
            AbstractModuleReader amr, BinaryWriter writer, TypeDecoders td
        ) {
            var count = amr.ReadArrayLength();
            writer.WriteLEB(count);

            for (uint i = 0; i < count; i++) {
                var item = td.export_entry();
                writer.WritePString(item.field);
                writer.Write((byte)item.kind);
                writer.WriteLEB(item.index);
            }
        }

        private static void EmitElementSection (
            AbstractModuleReader amr, BinaryWriter writer, TypeDecoders td
        ) {
            var count = amr.ReadArrayLength();
            writer.WriteLEB(count);

            for (uint i = 0; i < count; i++) {
                var item = td.elem_segment();
                writer.WriteLEB(item.index);
                EmitInitExpression(writer, ref item.offset);
                writer.WriteLEB((uint)item.elems.Length);
                foreach (var elem in item.elems)
                    writer.WriteLEB(elem);
            }
        }

        private static void EmitCodeSection (
            AbstractModuleReader amr, BinaryWriter writer, TypeDecoders td
        ) {
            var countStream = amr.Open(amr.Streams["function_expression_count"]);

            var count = amr.ReadArrayLength();
            writer.WriteLEB(count);

            using (var buffer = new MemoryStream(65536))
            using (var functionWriter = new BinaryWriter(buffer))
            for (uint i = 0; i < count; i++) {
                var item = td.function_body();
 
                functionWriter.WriteLEB((uint)item.locals.Length);
                foreach (var l in item.locals) {
                    functionWriter.WriteLEB(l.count);
                    functionWriter.Write((byte)l.type);
                }

                Check(countStream.ReadU32LEB(out uint expressionCount));
                uint numEmitted = 0;

                while (numEmitted < expressionCount) {
                    Expression e;
                    if (!td.Expression.Decode(out e))
                        throw new Exception("Failed decoding function body " + e.Opcode);

                    EmitExpression(functionWriter, ref e, ref numEmitted);
                }

                if (numEmitted > expressionCount)
                    throw new Exception("Decoded too many expressions");
                
                functionWriter.Flush();

                writer.WriteLEB((uint)buffer.Position);
                writer.Write(buffer.GetBuffer(), 0, (int)buffer.Position);

                buffer.SetLength(0);
            }
        }

        private static void EmitDataSection (
            AbstractModuleReader amr, BinaryWriter writer, TypeDecoders td
        ) {
            var dataStream = amr.Open(amr.Streams["data_segments"]);

            var count = amr.ReadArrayLength();
            writer.WriteLEB(count);

            for (uint i = 0; i < count; i++) {
                var item = td.data_segment();
                writer.WriteLEB(item.index);
                EmitInitExpression(writer, ref item.offset);
                writer.WriteLEB(item.size);

                dataStream.CopyTo(writer, item.size);
                /*
                writer.Flush();
                var bs = dataStream.BaseStream;
                using (var subStream = new StreamWindow(bs, bs.Position, item.size))
                    subStream.CopyTo(writer.BaseStream);

                dataStream.BaseStream.Seek(item.size, SeekOrigin.Current);
                */
            }
        }
    }
}
