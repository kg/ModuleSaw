using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ModuleSaw;

namespace Wasm.Model {
    public class ExpressionReader {
        public BinaryReader Reader;
        public uint NumRead { get; private set; }

        public ExpressionReader (BinaryReader reader) {
            Reader = reader;
        }

        public Opcodes? ReadOpcode () {
            // FIXME: Sanity checking to ensure we read a signed int7
            if (Reader.BaseStream.Position == Reader.BaseStream.Length)
                return null;

            return (Opcodes)Reader.ReadByte();
        }

        public bool TryReadInitExpr (out Expression result, Opcodes? expected = null) {
            if (!TryReadExpression(out result))
                return false;

            if (!TryReadExpressionBody(ref result)) {
                // HACK
                throw new Exception("Failed to read body of " + result.Opcode);
            }

            if (expected.HasValue && result.Opcode != expected)
                return false;

            switch (result.Opcode) {
                case Opcodes.f32_const:
                case Opcodes.f64_const:
                case Opcodes.i32_const:
                case Opcodes.i64_const:
                case Opcodes.get_global:
                    break;

                default:
                    throw new Exception("Unsupported init_expr opcode:" + result.Opcode);
            }

            return (Reader.ReadByte() == (byte)Opcodes.end);
        }

        public bool TryReadExpression (out Expression result) {
            result = default(Expression);
            var opcode = ReadOpcode();
            if (!opcode.HasValue)
                return false;

            result.Opcode = opcode.Value;
            result.State = ExpressionState.BodyNotRead;

            Depth += 1;
            NumRead += 1;

            return true;
        }

        private bool GatherChildNodesUntil (ref ExpressionBody body, Predicate<Expression> pred) {
            var result = new List<Expression>();
            body.Type |= ExpressionBody.Types.children;

            var initialDepth = Depth;

            while (true) {
                Expression e;
                if (!TryReadExpression(out e))
                    return false;
                if (!TryReadExpressionBody(ref e))
                    return false;

                result.Add(e);

                if (pred(e) && (Depth == initialDepth)) {
                    body.children = result.Count > 0 ? result : null;
                    return true;
                }
            }
        }

        private int Depth = 0;

        public bool TryReadExpressionBody (ref Expression expr) {
            if (expr.State == ExpressionState.Uninitialized)
                throw new ArgumentException("Uninitialized expression");

            try {
                switch (expr.Opcode) {
                    case Opcodes.nop:
                    case Opcodes.unreachable:
                    case Opcodes.end:
                    case Opcodes.@return:
                    case Opcodes.drop:
                    case Opcodes.select:
                        break;

                    case Opcodes.block:
                    case Opcodes.loop:
                        expr.Body.U.type = (LanguageTypes)Reader.ReadLEBInt();
                        expr.Body.Type = ExpressionBody.Types.type;

                        if (!GatherChildNodesUntil(ref expr.Body, e => e.Opcode == Opcodes.end))
                            return false;

                        break;

                    case Opcodes.@if:
                        expr.Body.U.type = (LanguageTypes)Reader.ReadLEBInt();
                        expr.Body.Type = ExpressionBody.Types.type;

                        if (!GatherChildNodesUntil(
                            ref expr.Body, 
                            e => (e.Opcode == Opcodes.end) || (e.Opcode == Opcodes.@else))
                        )
                            return false;

                        break;

                    case Opcodes.@else:
                        if (!GatherChildNodesUntil(ref expr.Body, e => e.Opcode == Opcodes.end))
                            return false;

                        break;

                    case Opcodes.call:
                    case Opcodes.br:
                    case Opcodes.br_if:
                    case Opcodes.get_global:
                    case Opcodes.set_global:
                    case Opcodes.get_local:
                    case Opcodes.set_local:
                    case Opcodes.tee_local:
                        expr.Body.U.u32 = (uint)Reader.ReadLEBUInt();
                        expr.Body.Type = ExpressionBody.Types.u32;

                        break;

                    case Opcodes.br_table:
                        var target_count = (uint)Reader.ReadLEBUInt();
                        var target_table = new uint[target_count];
                        for (var i = 0; i < target_count; i++)
                            target_table[i] = (uint)Reader.ReadLEBUInt();

                        expr.Body.br_table = new br_table_immediate {
                            target_table = target_table,
                            default_target = (uint)Reader.ReadLEBUInt()
                        };

                        expr.Body.Type = ExpressionBody.Types.br_table;
                        break;

                    case Opcodes.call_indirect:
                        expr.Body.U.u32 = (uint)Reader.ReadLEBUInt();
                        expr.Body.Type = ExpressionBody.Types.u32;
                        // FIXME
                        var reserved = Reader.ReadLEBUInt();

                        break;

                    case Opcodes.i32_const:
                        expr.Body.U.i32 = (int)Reader.ReadLEBInt();
                        expr.Body.Type = ExpressionBody.Types.i32;
                        break;

                    case Opcodes.i64_const:
                        expr.Body.U.i64 = (int)Reader.ReadLEBInt();
                        expr.Body.Type = ExpressionBody.Types.i64;
                        break;

                    case Opcodes.f32_const:
                        expr.Body.U.f32 = (int)Reader.ReadSingle();
                        expr.Body.Type = ExpressionBody.Types.f32;
                        break;

                    case Opcodes.f64_const:
                        expr.Body.U.f64 = (int)Reader.ReadDouble();
                        expr.Body.Type = ExpressionBody.Types.f64;
                        break;

                    case Opcodes.i32_load8_s:
                    case Opcodes.i32_load8_u:
                    case Opcodes.i32_load16_s:
                    case Opcodes.i32_load16_u:
                    case Opcodes.i64_load8_s:
                    case Opcodes.i64_load8_u:
                    case Opcodes.i64_load16_s:
                    case Opcodes.i64_load16_u:
                    case Opcodes.i64_load32_s:
                    case Opcodes.i64_load32_u:
                    case Opcodes.i32_load:
                    case Opcodes.i64_load:
                    case Opcodes.f32_load:
                    case Opcodes.f64_load:
                        expr.Body.Type = ExpressionBody.Types.memory;
                        if (!ReadMemoryImmediate(out expr.Body.U.memory))
                            return false;

                        break;

                    case Opcodes.i32_store8:
                    case Opcodes.i32_store16:
                    case Opcodes.i64_store8:
                    case Opcodes.i64_store16:
                    case Opcodes.i64_store32:
                    case Opcodes.i32_store:
                    case Opcodes.i64_store:
                    case Opcodes.f32_store:
                    case Opcodes.f64_store:
                        expr.Body.Type = ExpressionBody.Types.memory;
                        if (!ReadMemoryImmediate(out expr.Body.U.memory))
                            return false;

                        break;

                    case Opcodes.i32_add:
                    case Opcodes.i32_sub:
                    case Opcodes.i32_mul:
                    case Opcodes.i32_div_s:
                    case Opcodes.i32_div_u:
                    case Opcodes.i32_rem_s:
                    case Opcodes.i32_rem_u:
                    case Opcodes.i32_and:
                    case Opcodes.i32_or:
                    case Opcodes.i32_xor:
                    case Opcodes.i32_shl:
                    case Opcodes.i32_shr_u:
                    case Opcodes.i32_shr_s:
                    case Opcodes.i32_rotr:
                    case Opcodes.i32_rotl:
                    case Opcodes.i64_add:
                    case Opcodes.i64_sub:
                    case Opcodes.i64_mul:
                    case Opcodes.i64_div_s:
                    case Opcodes.i64_div_u:
                    case Opcodes.i64_rem_s:
                    case Opcodes.i64_rem_u:
                    case Opcodes.i64_and:
                    case Opcodes.i64_or:
                    case Opcodes.i64_xor:
                    case Opcodes.i64_shl:
                    case Opcodes.i64_shr_u:
                    case Opcodes.i64_shr_s:
                    case Opcodes.i64_rotr:
                    case Opcodes.i64_rotl:
                    case Opcodes.f32_add:
                    case Opcodes.f32_sub:
                    case Opcodes.f32_mul:
                    case Opcodes.f32_div:
                    case Opcodes.f32_min:
                    case Opcodes.f32_max:
                    case Opcodes.f32_copysign:
                    case Opcodes.f64_add:
                    case Opcodes.f64_sub:
                    case Opcodes.f64_mul:
                    case Opcodes.f64_div:
                    case Opcodes.f64_min:
                    case Opcodes.f64_max:
                    case Opcodes.f64_copysign:
                        break;

                    case Opcodes.i32_eq:
                    case Opcodes.i32_ne:
                    case Opcodes.i32_lt_s:
                    case Opcodes.i32_le_s:
                    case Opcodes.i32_lt_u:
                    case Opcodes.i32_le_u:
                    case Opcodes.i32_gt_s:
                    case Opcodes.i32_ge_s:
                    case Opcodes.i32_gt_u:
                    case Opcodes.i32_ge_u:
                    case Opcodes.i64_eq:
                    case Opcodes.i64_ne:
                    case Opcodes.i64_lt_s:
                    case Opcodes.i64_le_s:
                    case Opcodes.i64_lt_u:
                    case Opcodes.i64_le_u:
                    case Opcodes.i64_gt_s:
                    case Opcodes.i64_ge_s:
                    case Opcodes.i64_gt_u:
                    case Opcodes.i64_ge_u:
                    case Opcodes.f32_eq:
                    case Opcodes.f32_ne:
                    case Opcodes.f32_lt:
                    case Opcodes.f32_le:
                    case Opcodes.f32_gt:
                    case Opcodes.f32_ge:
                    case Opcodes.f64_eq:
                    case Opcodes.f64_ne:
                    case Opcodes.f64_lt:
                    case Opcodes.f64_le:
                    case Opcodes.f64_gt:
                    case Opcodes.f64_ge:
                        break;

                    case Opcodes.i32_clz:
                    case Opcodes.i32_ctz:
                    case Opcodes.i32_popcnt:
                    case Opcodes.i64_clz:
                    case Opcodes.i64_ctz:
                    case Opcodes.i64_popcnt:
                    case Opcodes.f32_abs:
                    case Opcodes.f32_neg:
                    case Opcodes.f32_ceil:
                    case Opcodes.f32_floor:
                    case Opcodes.f32_trunc:
                    case Opcodes.f32_nearest:
                    case Opcodes.f32_sqrt:
                    case Opcodes.f64_abs:
                    case Opcodes.f64_neg:
                    case Opcodes.f64_ceil:
                    case Opcodes.f64_floor:
                    case Opcodes.f64_trunc:
                    case Opcodes.f64_nearest:
                    case Opcodes.f64_sqrt:
                        break;

                    case Opcodes.i32_trunc_s_f32:
                    case Opcodes.i32_trunc_s_f64:
                    case Opcodes.i32_trunc_u_f32:
                    case Opcodes.i32_trunc_u_f64:
                    case Opcodes.i32_wrap_i64:
                    case Opcodes.i64_trunc_s_f32:
                    case Opcodes.i64_trunc_s_f64:
                    case Opcodes.i64_trunc_u_f32:
                    case Opcodes.i64_trunc_u_f64:
                    case Opcodes.i64_extend_s_i32:
                    case Opcodes.i64_extend_u_i32:
                    case Opcodes.f32_convert_s_i32:
                    case Opcodes.f32_convert_u_i32:
                    case Opcodes.f32_convert_s_i64:
                    case Opcodes.f32_convert_u_i64:
                    case Opcodes.f32_demote_f64:
                    case Opcodes.f32_reinterpret_i32:
                    case Opcodes.f64_convert_s_i32:
                    case Opcodes.f64_convert_u_i32:
                    case Opcodes.f64_convert_s_i64:
                    case Opcodes.f64_convert_u_i64:
                    case Opcodes.f64_promote_f32:
                    case Opcodes.f64_reinterpret_i64:
                    case Opcodes.i32_reinterpret_f32:
                    case Opcodes.i64_reinterpret_f64:
                    case Opcodes.i32_eqz:
                    case Opcodes.i64_eqz:
                        break;
                
                    // multibyte opcodes not in the spec... 😑
                    /*
                    case Opcodes.i32_trunc_s_sat_f32:
                    case Opcodes.i32_trunc_u_sat_f32:
                    case Opcodes.i32_trunc_s_sat_f64:
                    case Opcodes.i32_trunc_u_sat_f64:
                    case Opcodes.i64_trunc_s_sat_f32:
                    case Opcodes.i64_trunc_u_sat_f32:
                    case Opcodes.i64_trunc_s_sat_f64:
                    case Opcodes.i64_trunc_u_sat_f64:
                        // convert
                        return false;
                    */

                    case Opcodes.i32_extend_8_s:
                    case Opcodes.i32_extend_16_s:
                    case Opcodes.i64_extend_8_s:
                    case Opcodes.i64_extend_16_s:
                    case Opcodes.i64_extend_32_s:
                        break;

                    case Opcodes.grow_memory:
                    case Opcodes.current_memory:
                        expr.Body.U.u32 = (uint)Reader.ReadLEBUInt();
                        expr.Body.Type = ExpressionBody.Types.u1;
                        break;

                    default:
                        return false;
                }

                expr.State = ExpressionState.Initialized;
                return true;
            } finally {
                Depth -= 1;
            }
        }

        public bool ReadMemoryImmediate (out memory_immediate memory) {
            memory.flags = (uint)Reader.ReadLEBUInt();
            memory.offset = (uint)Reader.ReadLEBUInt();
            return true;
        }
    }
}
