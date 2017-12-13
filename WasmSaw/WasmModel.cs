using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Wasm.Model {
    public enum SectionTypes : sbyte {
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
        Data
    }

    public enum external_kind : sbyte {
        Function = 0,
        Table,
        Memory,
        Global
    }

    public enum LanguageTypes : sbyte {
        none = 0,

        i32 = -0x01,
        i64 = -0x02,
        f32 = -0x03,
        f64 = -0x04,
        anyfunc = -0x10,
        func = -0x20,
        empty_block = -0x40
    }

    public struct SectionHeader {
        public SectionTypes id;
        public uint payload_len;
        public string name;

        // Relative to start of file
        public long payload_start, payload_end;
    }

    public struct TypeSection {
        public func_type[] entries;
    }

    public struct ImportSection {
        public import_entry[] entries;
    }

    public struct FunctionSection {
        public uint[] types;
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

    public struct function_body {
        public uint body_size;
        public local_entry[] locals;

        // Position inside stream
        public long body_offset, body_end;
    }

    public struct local_entry {
        public uint count;
        public LanguageTypes type;
    }

    public struct data_segment {
        public uint index;
        public Expression offset;
        public uint size;

        // Position inside stream
        public long data_offset;
    }

    public struct elem_segment {
        public uint index;
        public Expression offset;
        public uint[] elems;
    }

    public struct export_entry {
        public string field;
        public external_kind kind;
        public uint index;
    }

    public struct global_variable {
        public global_type type;
        public Expression init;
    }

    public struct import_entry {
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

    public struct func_type {
        public sbyte form;
        public LanguageTypes[] param_types;
        public LanguageTypes return_type;
    }

    public struct global_type {
        public LanguageTypes content_type;
        public bool mutability;
    }

    public struct table_type {
        public LanguageTypes element_type;
        public resizable_limits limits;
    }

    public struct memory_type {
        public resizable_limits limits;
    }

    public struct resizable_limits {
        public byte flags;
        public uint initial;
        public uint maximum;
    }
}
