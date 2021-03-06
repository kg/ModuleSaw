﻿using System;
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

        i32_extend_8_s = 0xc0,
        i32_extend_16_s,
        i64_extend_8_s,
        i64_extend_16_s,
        i64_extend_32_s,

        PREFIX_sat = 0xfc,
        PREFIX_atomic = 0xfe
    }

    public static class OpcodesInfo {
        public const Opcodes FirstLoad = Opcodes.i32_load,
            LastLoad = Opcodes.i64_load32_u,
            FirstStore = Opcodes.i32_store,
            LastStore = Opcodes.i64_store32;

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
            { Opcodes.i32_store, 4 },
            { Opcodes.i64_store, 8 },
            { Opcodes.f32_store, 4 },
            { Opcodes.f64_store, 8 },
            { Opcodes.i32_store8, 1 },
            { Opcodes.i32_store16, 2 },
            { Opcodes.i64_store8, 1 },
            { Opcodes.i64_store16, 2 },
            { Opcodes.i64_store32, 4 },
        };
    }

    public enum ExpressionState : byte {
        Uninitialized = 0,
        BodyNotRead = 1,
        Initialized = 2
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
        public uint reserved;
    }
}
