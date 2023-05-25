using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using ModuleSaw;

namespace Wasm.Model {
    public interface IExpressionVisitor {
        void Visit (ref Expression e, int depth, out bool wantToVisitChildren);
    }

    public enum Opcodes : uint {
        unreachable = 0x00,
        nop,
        block,
        loop,
        @if,
        @else,
        
        try_ = 0x06,
        catch_,
        catch_all = 0x19,
        throw_ = 0x08,
        rethrow_ = 0x09,

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

        i32_extend_8_s = 0xc0,
        i32_extend_16_s,
        i64_extend_8_s,
        i64_extend_16_s,
        i64_extend_32_s,

        ref_null = 0xd0,
        ref_is_null,
        ref_func,

        PREFIX_simd = 0xfd,
        PREFIX_sat_or_bulk = 0xfc,
        PREFIX_atomic = 0xfe,

        _MULTIBYTE = 0x100,

        _FIRST_SAT_OR_BULK = i32_trunc_sat_f32_s,
        i32_trunc_sat_f32_s = 0xFC00,
        i32_trunc_sat_f32_u = 0xFC01,
        i32_trunc_sat_f64_s = 0xFC02,
        i32_trunc_sat_f64_u = 0xFC03,
        i64_trunc_sat_f32_s = 0xFC04,
        i64_trunc_sat_f32_u = 0xFC05,
        i64_trunc_sat_f64_s = 0xFC06,
        i64_trunc_sat_f64_u = 0xFC07,
        memory_init = 0xFC08,
        data_drop = 0xFC09,
        memory_copy = 0xFC0A,
        memory_fill = 0xFC0B,
        table_init = 0xFC0C,
        elem_drop = 0xFC0D,
        table_copy = 0xFC0E,
            /*
| `memory.init` | `0xfc 0x08` | `segment:varuint32`, `memory:0x00` | copy from a passive data segment to linear memory |
| `data.drop` | `0xfc 0x09` | `segment:varuint32` | prevent further use of passive data segment |
| `memory.copy` | `0xfc 0x0a` | `memory_dst:0x00` `memory_src:0x00` | copy from one region of linear memory to another region |
| `memory.fill` | `0xfc 0x0b` | `memory:0x00` | fill a region of linear memory with a given byte value |
| `table.init` | `0xfc 0x0c` | `segment:varuint32`, `table:0x00` | copy from a passive element segment to a table |
| `elem.drop` | `0xfc 0x0d` | `segment:varuint32` | prevent further use of a passive element segment |
| `table.copy` | `0xfc 0x0e` | `table_dst:0x00` `table_src:0x00` | copy from one region of a table to another region |
            */
        _LAST_SAT_OR_BULK = table_copy,

        v128_load                     = 0xFD00,
        v128_load8x8_s                = 0xFD01,
        v128_load8x8_u                = 0xFD02,
        v128_load16x4_s               = 0xFD03,
        v128_load16x4_u               = 0xFD04,
        v128_load32x2_s               = 0xFD05,
        v128_load32x2_u               = 0xFD06,
        v128_load8_splat              = 0xFD07,
        v128_load16_splat             = 0xFD08,
        v128_load32_splat             = 0xFD09,
        v128_load64_splat             = 0xFD0a,
        v128_store                    = 0xFD0b,
        v128_const                    = 0xFD0c,
        i8x16_shuffle                 = 0xFD0d,
        i8x16_swizzle                 = 0xFD0e,
        i8x16_splat                   = 0xFD0f,
        i16x8_splat                   = 0xFD10,
        i32x4_splat                   = 0xFD11,
        i64x2_splat                   = 0xFD12,
        f32x4_splat                   = 0xFD13,
        f64x2_splat                   = 0xFD14,
        i8x16_extract_lane_s          = 0xFD15,
        i8x16_extract_lane_u          = 0xFD16,
        i8x16_replace_lane            = 0xFD17,
        i16x8_extract_lane_s          = 0xFD18,
        i16x8_extract_lane_u          = 0xFD19,
        i16x8_replace_lane            = 0xFD1a,
        i32x4_extract_lane            = 0xFD1b,
        i32x4_replace_lane            = 0xFD1c,
        i64x2_extract_lane            = 0xFD1d,
        i64x2_replace_lane            = 0xFD1e,
        f32x4_extract_lane            = 0xFD1f,
        f32x4_replace_lane            = 0xFD20,
        f64x2_extract_lane            = 0xFD21,
        f64x2_replace_lane            = 0xFD22,
        i8x16_eq                      = 0xFD23,
        i8x16_ne                      = 0xFD24,
        i8x16_lt_s                    = 0xFD25,
        i8x16_lt_u                    = 0xFD26,
        i8x16_gt_s                    = 0xFD27,
        i8x16_gt_u                    = 0xFD28,
        i8x16_le_s                    = 0xFD29,
        i8x16_le_u                    = 0xFD2a,
        i8x16_ge_s                    = 0xFD2b,
        i8x16_ge_u                    = 0xFD2c,
        i16x8_eq                      = 0xFD2d,
        i16x8_ne                      = 0xFD2e,
        i16x8_lt_s                    = 0xFD2f,
        i16x8_lt_u                    = 0xFD30,
        i16x8_gt_s                    = 0xFD31,
        i16x8_gt_u                    = 0xFD32,
        i16x8_le_s                    = 0xFD33,
        i16x8_le_u                    = 0xFD34,
        i16x8_ge_s                    = 0xFD35,
        i16x8_ge_u                    = 0xFD36,
        i32x4_eq                      = 0xFD37,
        i32x4_ne                      = 0xFD38,
        i32x4_lt_s                    = 0xFD39,
        i32x4_lt_u                    = 0xFD3a,
        i32x4_gt_s                    = 0xFD3b,
        i32x4_gt_u                    = 0xFD3c,
        i32x4_le_s                    = 0xFD3d,
        i32x4_le_u                    = 0xFD3e,
        i32x4_ge_s                    = 0xFD3f,
        i32x4_ge_u                    = 0xFD40,
        f32x4_eq                      = 0xFD41,
        f32x4_ne                      = 0xFD42,
        f32x4_lt                      = 0xFD43,
        f32x4_gt                      = 0xFD44,
        f32x4_le                      = 0xFD45,
        f32x4_ge                      = 0xFD46,
        f64x2_eq                      = 0xFD47,
        f64x2_ne                      = 0xFD48,
        f64x2_lt                      = 0xFD49,
        f64x2_gt                      = 0xFD4a,
        f64x2_le                      = 0xFD4b,
        f64x2_ge                      = 0xFD4c,
        v128_not                      = 0xFD4d,
        v128_and                      = 0xFD4e,
        v128_andnot                   = 0xFD4f,
        v128_or                       = 0xFD50,
        v128_xor                      = 0xFD51,
        v128_bitselect                = 0xFD52,
        i8x16_abs                     = 0xFD60,
        i8x16_neg                     = 0xFD61,
        i8x16_all_true                = 0xFD63,
        i8x16_bitmask                 = 0xFD64,
        i8x16_narrow_i16x8_s          = 0xFD65,
        i8x16_narrow_i16x8_u          = 0xFD66,
        i8x16_shl                     = 0xFD6b,
        i8x16_shr_s                   = 0xFD6c,
        i8x16_shr_u                   = 0xFD6d,
        i8x16_add                     = 0xFD6e,
        i8x16_add_sat_s               = 0xFD6f,
        i8x16_add_sat_u               = 0xFD70,
        i8x16_sub                     = 0xFD71,
        i8x16_sub_sat_s               = 0xFD72,
        i8x16_sub_sat_u               = 0xFD73,
        i8x16_min_s                   = 0xFD76,
        i8x16_min_u                   = 0xFD77,
        i8x16_max_s                   = 0xFD78,
        i8x16_max_u                   = 0xFD79,
        i8x16_avgr_u                  = 0xFD7b,
        i16x8_abs                     = 0xFD80,
        i16x8_neg                     = 0xFD81,
        i16x8_all_true                = 0xFD83,
        i16x8_bitmask                 = 0xFD84,
        i16x8_narrow_i32x4_s          = 0xFD85,
        i16x8_narrow_i32x4_u          = 0xFD86,
        i16x8_extend_low_i8x16_s      = 0xFD87,
        i16x8_extend_high_i8x16_s     = 0xFD88,
        i16x8_extend_low_i8x16_u      = 0xFD89,
        i16x8_extend_high_i8x16_u     = 0xFD8a,
        i16x8_shl                     = 0xFD8b,
        i16x8_shr_s                   = 0xFD8c,
        i16x8_shr_u                   = 0xFD8d,
        i16x8_add                     = 0xFD8e,
        i16x8_add_sat_s               = 0xFD8f,
        i16x8_add_sat_u               = 0xFD90,
        i16x8_sub                     = 0xFD91,
        i16x8_sub_sat_s               = 0xFD92,
        i16x8_sub_sat_u               = 0xFD93,
        i16x8_mul                     = 0xFD95,
        i16x8_min_s                   = 0xFD96,
        i16x8_min_u                   = 0xFD97,
        i16x8_max_s                   = 0xFD98,
        i16x8_max_u                   = 0xFD99,
        i16x8_avgr_u                  = 0xFD9b,
        i32x4_abs                     = 0xFDa0,
        i32x4_neg                     = 0xFDa1,
        i32x4_all_true                = 0xFDa3,
        i32x4_bitmask                 = 0xFDa4,
        i32x4_extend_low_i16x8_s      = 0xFDa7,
        i32x4_extend_high_i16x8_s     = 0xFDa8,
        i32x4_extend_low_i16x8_u      = 0xFDa9,
        i32x4_extend_high_i16x8_u     = 0xFDaa,
        i32x4_shl                     = 0xFDab,
        i32x4_shr_s                   = 0xFDac,
        i32x4_shr_u                   = 0xFDad,
        i32x4_add                     = 0xFDae,
        i32x4_sub                     = 0xFDb1,
        i32x4_mul                     = 0xFDb5,
        i32x4_min_s                   = 0xFDb6,
        i32x4_min_u                   = 0xFDb7,
        i32x4_max_s                   = 0xFDb8,
        i32x4_max_u                   = 0xFDb9,
        i32x4_dot_i16x8_s             = 0xFDba,
        i64x2_abs                     = 0xFDc0,
        i64x2_neg                     = 0xFDc1,
        i64x2_bitmask                 = 0xFDc4,
        i64x2_extend_low_i32x4_s      = 0xFDc7,
        i64x2_extend_high_i32x4_s     = 0xFDc8,
        i64x2_extend_low_i32x4_u      = 0xFDc9,
        i64x2_extend_high_i32x4_u     = 0xFDca,
        i64x2_shl                     = 0xFDcb,
        i64x2_shr_s                   = 0xFDcc,
        i64x2_shr_u                   = 0xFDcd,
        i64x2_add                     = 0xFDce,
        i64x2_sub                     = 0xFDd1,
        i64x2_mul                     = 0xFDd5,
        f32x4_ceil                    = 0xFD67,
        f32x4_floor                   = 0xFD68,
        f32x4_trunc                   = 0xFD69,
        f32x4_nearest                 = 0xFD6a,
        f64x2_ceil                    = 0xFD74,
        f64x2_floor                   = 0xFD75,
        f64x2_trunc                   = 0xFD7a,
        f64x2_nearest                 = 0xFD94,
        f32x4_abs                     = 0xFDe0,
        f32x4_neg                     = 0xFDe1,
        f32x4_sqrt                    = 0xFDe3,
        f32x4_add                     = 0xFDe4,
        f32x4_sub                     = 0xFDe5,
        f32x4_mul                     = 0xFDe6,
        f32x4_div                     = 0xFDe7,
        f32x4_min                     = 0xFDe8,
        f32x4_max                     = 0xFDe9,
        f32x4_pmin                    = 0xFDea,
        f32x4_pmax                    = 0xFDeb,
        f64x2_abs                     = 0xFDec,
        f64x2_neg                     = 0xFDed,
        f64x2_sqrt                    = 0xFDef,
        f64x2_add                     = 0xFDf0,
        f64x2_sub                     = 0xFDf1,
        f64x2_mul                     = 0xFDf2,
        f64x2_div                     = 0xFDf3,
        f64x2_min                     = 0xFDf4,
        f64x2_max                     = 0xFDf5,
        f64x2_pmin                    = 0xFDf6,
        f64x2_pmax                    = 0xFDf7,
        i32x4_trunc_sat_f32x4_s       = 0xFDf8,
        i32x4_trunc_sat_f32x4_u       = 0xFDf9,
        f32x4_convert_i32x4_s         = 0xFDfa,
        f32x4_convert_i32x4_u         = 0xFDfb,
        v128_load32_zero              = 0xFD5c,
        v128_load64_zero              = 0xFD5d,
        i16x8_extmul_low_i8x16_s      = 0xFD9c,
        i16x8_extmul_high_i8x16_s     = 0xFD9d,
        i16x8_extmul_low_i8x16_u      = 0xFD9e,
        i16x8_extmul_high_i8x16_u     = 0xFD9f,
        i32x4_extmul_low_i16x8_s      = 0xFDbc,
        i32x4_extmul_high_i16x8_s     = 0xFDbd,
        i32x4_extmul_low_i16x8_u      = 0xFDbe,
        i32x4_extmul_high_i16x8_u     = 0xFDbf,
        i64x2_extmul_low_i32x4_s      = 0xFDdc,
        i64x2_extmul_high_i32x4_s     = 0xFDdd,
        i64x2_extmul_low_i32x4_u      = 0xFDde,
        i64x2_extmul_high_i32x4_u     = 0xFDdf,
        i16x8_q15mulr_sat_s           = 0xFD82,
        v128_any_true                 = 0xFD53,
        v128_load8_lane               = 0xFD54,
        v128_load16_lane              = 0xFD55,
        v128_load32_lane              = 0xFD56,
        v128_load64_lane              = 0xFD57,
        v128_store8_lane              = 0xFD58,
        v128_store16_lane             = 0xFD59,
        v128_store32_lane             = 0xFD5a,
        v128_store64_lane             = 0xFD5b,
        i64x2_eq                      = 0xFDd6,
        i64x2_ne                      = 0xFDd7,
        i64x2_lt_s                    = 0xFDd8,
        i64x2_gt_s                    = 0xFDd9,
        i64x2_le_s                    = 0xFDda,
        i64x2_ge_s                    = 0xFDdb,
        i64x2_all_true                = 0xFDc3,
        f64x2_convert_low_i32x4_s     = 0xFDfe,
        f64x2_convert_low_i32x4_u     = 0xFDff,
        i32x4_trunc_sat_f64x2_s_zero  = 0xFDfc,
        i32x4_trunc_sat_f64x2_u_zero  = 0xFDfd,
        f32x4_demote_f64x2_zero       = 0xFD5e,
        f64x2_promote_low_f32x4       = 0xFD5f,
        i8x16_popcnt                  = 0xFD62,
        i16x8_extadd_pairwise_i8x16_s = 0xFD7c,
        i16x8_extadd_pairwise_i8x16_u = 0xFD7d,
        i32x4_extadd_pairwise_i16x8_s = 0xFD7e,
        i32x4_extadd_pairwise_i16x8_u = 0xFD7f,

        _FIRST_ATOMIC = AtomicNotify,
        AtomicNotify = 0xFE00,
        I32AtomicWait = 0xFE01,
        I64AtomicWait = 0xFE02,
        AtomicFence = 0xFE03,
        I32AtomicLoad = 0xFE10,
        I32AtomicLoad8U = 0xFE12,
        I32AtomicLoad16U = 0xFE13,
        I32AtomicStore = 0xFE17,
        I32AtomicStore8U = 0xFE19,
        I32AtomicStore16U = 0xFE1a,
        I32AtomicAdd = 0xFE1e,
        I32AtomicAdd8U = 0xFE20,
        I32AtomicAdd16U = 0xFE21,
        I32AtomicSub = 0xFE25,
        I32AtomicSub8U = 0xFE27,
        I32AtomicSub16U = 0xFE28,
        I32AtomicAnd = 0xFE2c,
        I32AtomicAnd8U = 0xFE2e,
        I32AtomicAnd16U = 0xFE2f,
        I32AtomicOr = 0xFE33,
        I32AtomicOr8U = 0xFE35,
        I32AtomicOr16U = 0xFE36,
        I32AtomicXor = 0xFE3a,
        I32AtomicXor8U = 0xFE3c,
        I32AtomicXor16U = 0xFE3d,
        I32AtomicExchange = 0xFE41,
        I32AtomicExchange8U = 0xFE43,
        I32AtomicExchange16U = 0xFE44,
        I32AtomicCompareExchange = 0xFE48,
        I32AtomicCompareExchange8U = 0xFE4a,
        I32AtomicCompareExchange16U = 0xFE4b,

        I64AtomicLoad = 0xFE11,
        I64AtomicLoad8U = 0xFE14,
        I64AtomicLoad16U = 0xFE15,
        I64AtomicLoad32U = 0xFE16,
        I64AtomicStore = 0xFE18,
        I64AtomicStore8U = 0xFE1b,
        I64AtomicStore16U = 0xFE1c,
        I64AtomicStore32U = 0xFE1d,
        I64AtomicAdd = 0xFE1f,
        I64AtomicAdd8U = 0xFE22,
        I64AtomicAdd16U = 0xFE23,
        I64AtomicAdd32U = 0xFE24,
        I64AtomicSub = 0xFE26,
        I64AtomicSub8U = 0xFE29,
        I64AtomicSub16U = 0xFE2a,
        I64AtomicSub32U = 0xFE2b,
        I64AtomicAnd = 0xFE2d,
        I64AtomicAnd8U = 0xFE30,
        I64AtomicAnd16U = 0xFE31,
        I64AtomicAnd32U = 0xFE32,
        I64AtomicOr = 0xFE34,
        I64AtomicOr8U = 0xFE37,
        I64AtomicOr16U = 0xFE38,
        I64AtomicOr32U = 0xFE39,
        I64AtomicXor = 0xFE3b,
        I64AtomicXor8U = 0xFE3e,
        I64AtomicXor16U = 0xFE3f,
        I64AtomicXor32U = 0xFE40,
        I64AtomicExchange = 0xFE42,
        I64AtomicExchange8U = 0xFE45,
        I64AtomicExchange16U = 0xFE46,
        I64AtomicExchange32U = 0xFE47,
        I64AtomicCompareExchange = 0xFE49,
        I64AtomicCompareExchange8U = 0xFE4c,
        I64AtomicCompareExchange16U = 0xFE4d,
        I64AtomicCompareExchange32U = 0xFE4e,
        _LAST_ATOMIC = I64AtomicCompareExchange32U,
    }

    public static class OpcodesInfo {
        public const Opcodes FirstLoad = Opcodes.i32_load,
            LastLoad = Opcodes.i64_load32_u,
            FirstStore = Opcodes.i32_store,
            LastStore = Opcodes.i64_store32;

        public static readonly bool[] KnownOpcodes = new bool[0xFFFF];

        public static readonly HashSet<int> Prefixes = new HashSet<int> {
            (int)Opcodes.PREFIX_simd,
            (int)Opcodes.PREFIX_atomic,
            (int)Opcodes.PREFIX_sat_or_bulk,
        };

        public static readonly Dictionary<Opcodes, uint> MemorySizeForOpcode = new Dictionary<Opcodes, uint> {
            { Opcodes.i32_load, 4 },
            { Opcodes.i64_load, 8 },
            { Opcodes.f32_load, 4 },
            { Opcodes.f64_load, 8 },
            { Opcodes.i32_load8_s, 1 },
            { Opcodes.i32_load8_u, 1 },
            { Opcodes.i32_load16_s, 2 },
            { Opcodes.i32_load16_u, 2 },
            { Opcodes.i64_load8_s, 1 },
            { Opcodes.i64_load8_u, 1 },
            { Opcodes.i64_load16_s, 2 },
            { Opcodes.i64_load16_u, 2 },
            { Opcodes.i64_load32_s, 4 },
            { Opcodes.i64_load32_u, 4 },
            { Opcodes.v128_load, 16 },
            { Opcodes.i32_store, 4 },
            { Opcodes.i64_store, 8 },
            { Opcodes.f32_store, 4 },
            { Opcodes.f64_store, 8 },
            { Opcodes.i32_store8, 1 },
            { Opcodes.i32_store16, 2 },
            { Opcodes.i64_store8, 1 },
            { Opcodes.i64_store16, 2 },
            { Opcodes.i64_store32, 4 },
            { Opcodes.v128_store, 16 },
        };

        static OpcodesInfo () {
            foreach (var value in typeof(Opcodes).GetEnumValues())
                KnownOpcodes[(uint)value] = true;
        }
    }

    public enum ExpressionState : byte {
        Uninitialized = 0,
        BodyNotRead = 1,
        Initialized = 2,
        InitializedWithError = 3
    }

    public class ExpressionEmitVisitor : IExpressionVisitor {
        public BinaryWriter Output { get; private set; }
        public int Count { get; private set; }

        public ExpressionEmitVisitor (BinaryWriter output) {
            Output = output;
        }

        public void Visit (ref Expression e, int depth, out bool wantToVisitChildren) {
            Count++;
            Output.Write((byte)e.Opcode);
            Expression.EmitBody(Output, ref e, out bool temp);
            wantToVisitChildren = true;
        }
    }

    public struct Expression {
        public ExpressionState State;

        public Opcodes Opcode;
        public ExpressionBody Body;

        public bool ValidateBody () {
            switch (Opcode) {
                case Opcodes.i32_const:
                    return (Body.Type == ExpressionBody.Types.i32) ||
                        (Body.Type == ExpressionBody.Types.u32);
                case Opcodes.i64_const:
                    return (Body.Type == ExpressionBody.Types.i64);
                case Opcodes.f32_const:
                    return (Body.Type == ExpressionBody.Types.f32);
                case Opcodes.f64_const:
                    return (Body.Type == ExpressionBody.Types.f64);
                case Opcodes.get_local:
                case Opcodes.set_local:
                case Opcodes.tee_local:
                case Opcodes.get_global:
                case Opcodes.set_global:
                    return (Body.Type == ExpressionBody.Types.u32);
                case var o when (o >= Opcodes.i32_load) && (o <= Opcodes.i64_store32):
                    return (Body.Type == ExpressionBody.Types.memory);
                case var o when (o >= Opcodes.i32_eqz):
                    return (Body.Type == ExpressionBody.Types.none);
                // v128_load
                case Opcodes.v128_load:
                    return (Body.Type == ExpressionBody.Types.memory);
                default:
                    // FIXME
                    return true;
            }
        }

        public static int Emit (BinaryWriter output, ref Expression e) {
            var visitor = new ExpressionEmitVisitor(output);
            Visit(ref e, visitor);
            return visitor.Count;
        }

        public static void EmitBody (BinaryWriter output, ref Expression e, out bool needToEmitChildren) {
#if DEBUG
            if (!e.ValidateBody())
                throw new Exception("Invalid body for expression " + e);
#endif

            switch (e.Body.Type & ~ExpressionBody.Types.children) {
                case ExpressionBody.Types.none:
                    break;

                case ExpressionBody.Types.u32:
                    output.WriteLEB(e.Body.U.u32);
                    break;
                case ExpressionBody.Types.u1:
                    output.Write((byte)e.Body.U.u32);
                    break;
                case ExpressionBody.Types.i64:
                    output.WriteLEB(e.Body.U.i64);
                    break;
                case ExpressionBody.Types.i32:
                    output.WriteLEB(e.Body.U.i32);
                    break;
                case ExpressionBody.Types.f64:
                    output.Write(e.Body.U.f64);
                    break;
                case ExpressionBody.Types.f32:
                    output.Write(e.Body.U.f32);
                    break;
                case ExpressionBody.Types.memory:
                    output.WriteLEB(e.Body.U.memory.alignment_exponent);
                    output.WriteLEB(e.Body.U.memory.offset);
                    break;
                case ExpressionBody.Types.type:
                    // Console.WriteLine(((uint)e.Body.U.type).ToString("X2") + " " + e.Body.U.type);
                    output.Write((byte)e.Body.U.type);
                    break;
                case ExpressionBody.Types.br_table:
                    output.WriteLEB((uint)e.Body.br_table.target_table.Length);
                    foreach (var t in e.Body.br_table.target_table)
                        output.WriteLEB(t);
                    output.WriteLEB(e.Body.br_table.default_target);

                    break;

                default:
                    throw new Exception("Not implemented");
            }

            // HACK
            if (e.Opcode == Opcodes.call_indirect)
                output.Write((byte)0);

            needToEmitChildren = (e.Body.children != null) && (e.Body.children.Count > 0);
        }

        public static void Visit (ref Expression e, IExpressionVisitor visitor) {
            visitor.Visit(ref e, 0, out bool wantToVisitChildren);
            if (wantToVisitChildren)
                VisitChildren(ref e, visitor);
        }

        public static void VisitChildren (ref Expression e, IExpressionVisitor visitor) {
            Stack<(List<Expression>, int)> stack = null;
            var current = e.Body.children;
            if (current == null)
                return;

            int i = 0;
            while (true) {
                if (i >= current.Count) {
                    if ((stack == null) || (stack.Count <= 0))
                        return;
                    else {
                        var tup = stack.Pop();
                        current = tup.Item1;
                        i = tup.Item2;
                        continue;
                    }
                }

                var c = current[i++];
                var hasChildren = (c.Body.children != null) && (c.Body.children.Count > 0);
                visitor.Visit(
                    ref c, 
                    (stack != null) ? stack.Count + 1 : 1, 
                    out bool wantToVisitChildren
                );
                if (!hasChildren || !wantToVisitChildren)
                    continue;

                if (stack == null)
                    stack = new Stack<(List<Expression>, int)>();
                stack.Push((current, i));
                current = c.Body.children;
                i = 0;
            }
        }

        public override string ToString () {
            return string.Format("({0} {1})", Opcode, Body);
        }
    }

    public struct ExpressionBody {
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
            call_indirect,

            // An expression can have both an immediate and children so this is a flag
            // TODO: Remove this
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
            [FieldOffset(0)]
            public call_indirect_immediate call_indirect;

        }

        public Types Type;

        public Union U;
        public br_table_immediate br_table;
        public List<Expression> children;

        private object ExtractValue () {
            switch (Type) {
                case Types.none:
                    return "<none>";
                case Types.u1:
                    return U.u8;
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
                case Types.memory:
                    return "<memory>";
                case Types.type:
                    return "<type " + U.type + ">";
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

        public uint EXT_natural_alignment, EXT_natural_exponent;
        public int EXT_relative_alignment_exponent;
    }

    public struct call_indirect_immediate {
        public uint sig_index;
        public uint table_index;
    }
}
