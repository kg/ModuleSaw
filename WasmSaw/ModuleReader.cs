using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ModuleSaw;

namespace Wasm.Model {
    public class ModuleReader {
        public readonly BinaryReader Reader;
        public readonly ExpressionReader ExpressionReader;

        public ModuleReader (BinaryReader reader) {
            Reader = reader;
            ExpressionReader = new ExpressionReader(reader);
        }

        public bool ReadHeader () {
            var bytes = Reader.ReadChars(4);
            if (!bytes.SequenceEqual(new char[] { '\0', 'a', 's', 'm' }))
                return false;

            var version = Reader.ReadUInt32();
            if (version != 1)
                return false;

            return true;
        }

        public bool ReadSectionHeader (out SectionHeader sh) {
            sh = default(SectionHeader);

            var id = Reader.ReadLEBInt();
            if (!id.HasValue)
                return false;

            sh.id = (SectionTypes)id;

            var position = Reader.BaseStream.Position;

            sh.payload_len = (uint)Reader.ReadLEBUInt();

            if (sh.id == 0)
                sh.name = Reader.ReadPString();
            else
                sh.name = null;

            sh.payload_start = Reader.BaseStream.Position;
            sh.payload_end = position + sh.payload_len;

            // FIXME
            return true;
        }

        private TItem[] ReadList<TItem> (Func<uint, TItem> readItem) {
            var count = Reader.ReadLEBUInt();
            if (!count.HasValue)
                return null;

            var result = new TItem[count.Value];
            for (uint i = 0; i < result.Length; i++)
                result[i] = readItem(i);

            return result;
        }

        private LanguageTypes ReadLanguageType () {
            return (LanguageTypes)Reader.ReadLEBInt();
        }

        public bool ReadFuncType (out func_type ft) {
            ft = default(func_type);
            ft.form = (sbyte)Reader.ReadLEBInt();
            ft.param_types = ReadList((i) => ReadLanguageType());
            var return_count = Reader.ReadLEBUInt();
            if (return_count == 1)
                ft.return_type = ReadLanguageType();
            else if (return_count > 1)
                throw new Exception("Multiple return types not implemented");
            // FIXME
            return true;
        }
    
        public bool ReadTypeSection (out TypeSection ts) {
            ts.entries = ReadList((i) => {
                func_type ft;
                // FIXME
                ReadFuncType(out ft);
                return ft;
            });

            return ts.entries != null;
        }

        private bool ReadResizableLimits (out resizable_limits rl) {
            rl.flags = Reader.ReadByte();
            rl.initial = (uint)Reader.ReadLEBUInt();
            if ((rl.flags & 1) == 1)
                rl.maximum = (uint)Reader.ReadLEBUInt();
            else
                rl.maximum = 0;
            // FIXME
            return true;
        }

        private bool ReadTableType (out table_type tt) {
            tt.element_type = ReadLanguageType();
            return ReadResizableLimits(out tt.limits);
        }

        private bool ReadMemoryType (out memory_type mt) {
            return ReadResizableLimits(out mt.limits);
        }

        private bool ReadGlobalType (out global_type gt) {
            gt.content_type = ReadLanguageType();
            gt.mutability = Reader.ReadByte() != 0;
            // FIXME
            return true;
        }
    
        public bool ReadImportSection (out ImportSection ims) {
            ims.entries = ReadList((i) => {
                var result = new import_entry {
                    module = Reader.ReadPString(),
                    field = Reader.ReadPString(),
                    kind = (external_kind)Reader.ReadByte()
                };

                switch (result.kind) {
                    case external_kind.Function:
                        result.type.Function = (uint)Reader.ReadLEBUInt();
                        break;
                    case external_kind.Table:
                        ReadTableType(out result.type.Table);
                        break;
                    case external_kind.Memory:
                        ReadMemoryType(out result.type.Memory);
                        break;
                    case external_kind.Global:
                        ReadGlobalType(out result.type.Global);
                        break;
                }

                return result;
            });

            return ims.entries != null;
        }

        public bool ReadFunctionSection (out FunctionSection fs) {
            fs.types = ReadList((i) => (uint)Reader.ReadLEBUInt());

            return fs.types != null;
        }

        public bool ReadGlobalSection (out GlobalSection gs) {
            gs.globals = ReadList((i) => {
                var result = default(global_variable);
                // FIXME
                ReadGlobalType(out result.type);
                // FIXME
                ExpressionReader.TryReadInitExpr(out result.init);
                return result;
            });

            return gs.globals != null;
        }

        public bool ReadExportSection (out ExportSection exs) {
            exs.entries = ReadList((i) =>
                new export_entry {
                    field = Reader.ReadPString(),
                    kind = (external_kind)Reader.ReadByte(),
                    index = (uint)Reader.ReadLEBUInt()
                }
            );

            return exs.entries != null;
        }
        
        public bool ReadElementSection (out ElementSection els) {
            els.entries = ReadList((i) => {
                var result = default(elem_segment);
                result.index = (uint)Reader.ReadLEBUInt();
                // FIXME
                ExpressionReader.TryReadInitExpr(out result.offset);
                result.elems = ReadList((j) => (uint)Reader.ReadLEBUInt());
                return result;
            });

            return els.entries != null;
        }

        public bool ReadCodeSection (out CodeSection cs) {
            cs.bodies = ReadList((i) => {
                var initialOffset = Reader.BaseStream.Position;

                var bodySize = (long)Reader.ReadLEBUInt();
                var bodyOffset = Reader.BaseStream.Position;
                var localEntries = ReadList(
                    (j) => new local_entry {
                        count = (uint)Reader.ReadLEBUInt(),
                        type = ReadLanguageType()
                    }
                );

                var codeOffset = Reader.BaseStream.Position;
                var codeEnd = bodyOffset + bodySize;
                Reader.BaseStream.Seek(codeEnd, SeekOrigin.Begin);

                return new function_body {
                    body_size = (uint)bodySize,
                    locals = localEntries,
                    body_offset = codeOffset,
                    body_end = codeEnd
                };
            });

            return cs.bodies != null;
        }

        public bool ReadDataSection (out DataSection ds) {
            ds.entries = ReadList((i) => {
                var index = Reader.ReadLEBUInt();
                // FIXME: Error handling
                Expression offset;
                ExpressionReader.TryReadInitExpr(out offset);
                var size = Reader.ReadLEBUInt();
                var dataOffset = Reader.BaseStream.Position;

                Reader.BaseStream.Seek((long)size, SeekOrigin.Current);

                return new data_segment {
                    index = (uint)index,
                    offset = offset,
                    size = (uint)size,
                    data_offset = dataOffset
                };
            });

            return ds.entries != null;
        }
    }
}
