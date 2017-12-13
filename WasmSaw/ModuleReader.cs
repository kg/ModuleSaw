﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ModuleSaw;

namespace Wasm.Model {
    public class ModuleReader {
        public readonly BinaryReader Reader;

        public ModuleReader (BinaryReader reader) {
            Reader = reader;
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
            sh.payload_len = (uint)Reader.ReadLEBUInt();
            if (sh.id == 0)
                sh.name = Reader.ReadPString();
            else
                sh.name = null;

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

        public bool ReadCodeSection (out CodeSection cs) {
            cs.bodies = ReadList((i) => {
                var bodySize = (long)Reader.ReadLEBUInt();
                var bodyOffset = Reader.BaseStream.Position;
                var localEntries = ReadList(
                    (j) => new local_entry {
                        count = (uint)Reader.ReadLEBUInt(),
                        type = (LanguageTypes)Reader.ReadLEBInt()
                    }
                );
                var codeOffset = Reader.BaseStream.Position;
                Reader.BaseStream.Seek((long)(bodyOffset + bodySize), SeekOrigin.Begin);

                return new function_body {
                    body_size = (uint)bodySize,
                    locals = localEntries,
                    codeOffset = codeOffset
                };
            });

            return cs.bodies != null;
        }
    }
}
