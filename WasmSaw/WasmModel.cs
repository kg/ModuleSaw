﻿using System;
using System.Collections.Generic;
using System.Linq;
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
        public uint local_count;
        public local_entry[] locals;
        public byte[] code;
        public byte end;
    }

    public struct local_entry {
        public LanguageTypes[] type;
    }

    public struct init_expr {
        public Expression expr;
        public Expression end;
    }

    public struct memory_immediate {
        public uint flags;
        public uint offset;
    }

    public struct data_segment {
        public uint index;
        public init_expr offset;
        public byte[] data;
    }

    public struct elem_segment {
        public uint index;
        public init_expr offset;
        public uint[] elems;
    }

    public struct export_entry {
        public string field;
        public external_kind kind;
        public uint index;
    }

    public struct global_variable {
        public global_type type;
        public init_expr init;
    }

    public struct import_entry {
        public string module;
        public string field;
        public external_kind kind;
        // FIXME
        public object type;
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