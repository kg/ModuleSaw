using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using ModuleSaw;

namespace Wasm.Model {
    public enum SectionTypes : byte {
        Custom = 0,

        Type = 1,
        Import,
        Function,
        Table,
        Memory,
        Global,
        Export,
        Start,
        Element,
        Code,
        Data,

        Unknown
    }

    public enum external_kind : byte {
        Function = 0,
        Table,
        Memory,
        Global
    }

    public enum LanguageTypes : byte {
        none = 0,

        i32 = 0x7f,
        i64 = 0x7e,
        f32 = 0x7d,
        f64 = 0x7c,
        anyfunc = 0x70,
        func = 0x60,
        empty_block = 0x40,

        INVALID = 0xFF
    }

    public record struct SectionHeader {
        public SectionTypes id;
        public uint payload_len;
        public string name;

        // Relative to start of file
        public long StreamHeaderStart, StreamPayloadStart, StreamPayloadEnd;
    }

    public struct TypeSection {
        public func_type[] entries;
    }

    public struct ImportSection {
        public import_entry[] entries;
    }

    public readonly record struct func_type_index (uint index);

    public struct FunctionSection {
        public func_type_index[] types;
    }

    public struct TableSection {
        public table_type[] entries;
    }

    public struct MemorySection {
        public memory_type[] entries;
    }

    public struct GlobalSection {
        public global_variable[] globals;
    }

    public struct ExportSection {
        public export_entry[] entries;
    }

    public struct StartSection {
        public uint index;
    }

    public struct ElementSection {
        public elem_segment[] entries;
    }

    public struct CodeSection {
        public function_body[] bodies;
    }

    public struct DataSection {
        public data_segment[] entries;
    }

    public record struct function_body {
        public uint Index;

        public uint body_size;
        public local_entry[] locals;

        public long StreamOffset, StreamEnd;
    }

    public record struct local_entry {
        public uint count;
        public LanguageTypes type;
    }

    public record struct data_segment {
        public uint mode;
        public uint index;
        public Expression[] offset;
        public uint size;

        // Position inside stream
        public long data_offset;
    }

    public record struct elem_segment {
        public uint index;
        public Expression[] offset;
        public uint[] elems;
    }

    public record struct export_entry {
        public string field;
        public external_kind kind;
        public uint index;
    }

    public record struct global_variable {
        public global_type type;
        public Expression[] init;
    }

    public record struct import_entry {
        [StructLayout(LayoutKind.Explicit)]
        public struct TypeUnion {
            [FieldOffset(0)]
            public uint Function;
            [FieldOffset(0)]
            public table_type Table;
            [FieldOffset(0)]
            public memory_type Memory;
            [FieldOffset(0)]
            public global_type Global;
        }

        public string module;
        public string field;
        public external_kind kind;
        public TypeUnion type;
    }

    public record struct func_type {
        // Always -0x20?
        // public sbyte form;
        public LanguageTypes[] param_types;
        public LanguageTypes return_type;
    }

    public record struct global_type {
        public LanguageTypes content_type;
        public bool mutability;
    }

    public record struct table_type {
        public LanguageTypes element_type;
        public resizable_limits limits;
    }

    public record struct memory_type {
        public resizable_limits limits;
    }

    public record struct resizable_limits {
        public byte flags;
        public uint initial;
        public uint maximum;
    }

    public static class ModelExtensions {
        public static bool Read (this ArrayBinaryReader reader, out LanguageTypes result) {
            var ok = reader.Read(out byte b);
            result = (LanguageTypes)b;
            return ok;
        }

        public static bool Read (this ArrayBinaryReader reader, out external_kind result) {
            var ok = reader.Read(out byte b);
            result = (external_kind)b;
            return ok;
        }
    }
}
