using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Wasm.Model {
    public enum OpcodePrefixes : byte {
        none = 0x00,
        sat = 0xfc,
        atomic = 0xfe
    }

    public enum Opcodes : byte {
        unreachable = 0x00,
        nop,
        block,
        loop,
        @if,
        @else,

        end = 0x0b,
        br,
        br_if,
        br_table,
        @return,
        call,
        call_indirect,

        drop = 0x1a,
        select,

        get_local = 0x20,
        set_local,
        tee_local,
        get_global,
        set_global,

        i32_load = 0x28,
        i64_load,
        f32_load,
        f64_load,
        i32_load8_s,
        i32_load8_u,
        i32_load16_s,
        i32_load16_u,
        i64_load8_s,
        i64_load8_u,
        i64_load16_s,
        i64_load16_u,
        i64_load32_s,
        i64_load32_u,
        i32_store,
        i64_store,
        f32_store,
        f64_store,
        i32_store8,
        i32_store16,
        i64_store8,
        i64_store16,
        i64_store32,
        current_memory,
        grow_memory,

        i32_const = 0x41,
        i64_const,
        f32_const,
        f64_const,

        i32_eqz = 0x45,
        i32_eq,		
        i32_ne,		
        i32_lt_s,		
        i32_lt_u,		
        i32_gt_s,		
        i32_gt_u,		
        i32_le_s,		
        i32_le_u,		
        i32_ge_s,		
        i32_ge_u,		
        i64_eqz, 		
        i64_eq, 		
        i64_ne, 		
        i64_lt_s,
        i64_lt_u,
        i64_gt_s,
        i64_gt_u,
        i64_le_s,
        i64_le_u,
        i64_ge_s,
        i64_ge_u,
        f32_eq, 		
        f32_ne, 		
        f32_lt, 		
        f32_gt, 		
        f32_le, 		
        f32_ge, 		
        f64_eq, 		
        f64_ne, 		
        f64_lt, 		
        f64_gt, 		
        f64_le, 		
        f64_ge,

        i32_clz = 0x67,
        i32_ctz, 	  
        i32_popcnt, 	
        i32_add, 	
        i32_sub, 	
        i32_mul, 	
        i32_div_s, 	
        i32_div_u, 	
        i32_rem_s, 	
        i32_rem_u, 	
        i32_and, 	
        i32_or, 
        i32_xor, 	
        i32_shl, 	
        i32_shr_s, 	
        i32_shr_u, 	
        i32_rotl, 	
        i32_rotr, 	
        i64_clz, 	
        i64_ctz, 	
        i64_popcnt, 	
        i64_add, 	
        i64_sub, 	
        i64_mul, 	
        i64_div_s, 	
        i64_div_u, 	
        i64_rem_s, 	
        i64_rem_u, 	
        i64_and, 	
        i64_or, 
        i64_xor, 	
        i64_shl, 	
        i64_shr_s, 	
        i64_shr_u, 	
        i64_rotl, 	
        i64_rotr, 	
        f32_abs, 	
        f32_neg, 	
        f32_ceil, 	
        f32_floor, 	
        f32_trunc, 	
        f32_nearest, 
        f32_sqrt, 	
        f32_add, 	
        f32_sub, 	
        f32_mul, 	
        f32_div, 	
        f32_min, 	
        f32_max, 	
        f32_copysign,
        f64_abs, 	
        f64_neg, 	
        f64_ceil, 	
        f64_floor, 	
        f64_trunc, 	
        f64_nearest, 
        f64_sqrt, 	
        f64_add, 	
        f64_sub, 	
        f64_mul, 	
        f64_div, 	
        f64_min, 	
        f64_max, 	
        f64_copysign,

        i32_wrap_i64 = 0xa7, 		
        i32_trunc_s_f32, 		
        i32_trunc_u_f32, 		
        i32_trunc_s_f64, 		
        i32_trunc_u_f64, 		
        i64_extend_s_i32, 		
        i64_extend_u_i32, 		
        i64_trunc_s_f32, 		
        i64_trunc_u_f32, 		
        i64_trunc_s_f64, 		
        i64_trunc_u_f64, 		
        f32_convert_s_i32, 		
        f32_convert_u_i32, 		
        f32_convert_s_i64, 		
        f32_convert_u_i64, 		
        f32_demote_f64, 		
        f64_convert_s_i32, 		
        f64_convert_u_i32, 		
        f64_convert_s_i64, 		
        f64_convert_u_i64, 		
        f64_promote_f32, 		

        i32_reinterpret_f32 = 0xbc, 		
        i64_reinterpret_f64, 		
        f32_reinterpret_i32, 		
        f64_reinterpret_i64,

        // not in the spec 😒
        i32_extend_8_s = 0xc0,
        i32_extend_16_s,
        i64_extend_8_s,
        i64_extend_16_s,
        i64_extend_32_s,
    }

    public static class OpcodesInfo {
        public const Opcodes FirstLoad = Opcodes.i32_load,
            LastLoad = Opcodes.i64_load32_u,
            FirstStore = Opcodes.i32_store,
            LastStore = Opcodes.i64_store32;
    }

    public enum ExpressionState : byte {
        Uninitialized = 0,
        BodyNotRead = 1,
        Initialized = 2
    }

    public struct Expression {
        public ExpressionState State;

        public Opcodes Opcode;
        public ExpressionBody Body;

        public override string ToString () {
            return string.Format("({0} {1})", Opcode, Body);
        }
    }

    public struct ExpressionBody {
        [Flags]
        public enum Types : byte {
            none,
            u32,
            u1,
            i64,
            i32,
            f64,
            f32,
            memory,
            type,
            br_table,

            // An expression can have both an immediate and children so this is a flag
            children = 0x80,
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct Union {
            [FieldOffset(0)]
            public ulong u64;
            [FieldOffset(0)]
            public uint u32;
            [FieldOffset(0)]
            public byte u8;
            [FieldOffset(0)]
            public long i64;
            [FieldOffset(0)]
            public int i32;
            [FieldOffset(0)]
            public double f64;
            [FieldOffset(0)]
            public float f32;
            [FieldOffset(0)]
            public memory_immediate memory;
            [FieldOffset(0)]
            public LanguageTypes type;
        }

        public Types Type;

        public Union U;
        public br_table_immediate br_table;
        public List<Expression> children;

        private object ExtractValue () {
            switch (Type) {
                case Types.u32:
                    return U.u32;
                case Types.i32:
                    return U.i32;
                case Types.i64:
                    return U.i64;
                case Types.f32:
                    return U.f32;
                case Types.f64:
                    return U.f64;
                default:
                    return "<unknown>";
            }
        }

        public override string ToString () {
            if (Type == Types.none)
                return "";

            return string.Format("({0} {1})", Type, ExtractValue());
        }
    }

    public struct br_table_immediate {
        public uint[] target_table;
        public uint default_target;
    }

    public struct memory_immediate {
        public uint alignment_exponent;
        public uint offset;
    }

    public struct call_indirect_immediate {
        public uint sig_index;
        public uint reserved;
    }
}
