using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WasmSaw.Model {
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
    }

    public struct TypeSection {
        public uint count;
        public func_type[] entries;
    }

    public struct ImportSection {
        public uint count;
        public import_entry[] entries;
    }

    public struct import_entry {
        public string module;
        public string field;
        public external_kind kind;
        // HACK
        public uint type;
    }

    public struct func_type {
        public sbyte form;
        public LanguageTypes[] param_types;
        public LanguageTypes? return_type;
    }

    public struct global_type {
        public LanguageTypes content_type;
        public bool mutability;
    }

    public struct table_type {
        public LanguageTypes element_Type;
        public resizable_limits limits;
    }

    public struct memory_type {
        public resizable_limits limits;
    }

    public struct resizable_limits {
        public uint initial;
        public uint? maximum;
    }
}
