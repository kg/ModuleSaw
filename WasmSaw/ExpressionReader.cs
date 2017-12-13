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
                return false;
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
                    Console.WriteLine("Unsupported init_expr opcode: {0}", result.Opcode);
                    // HACK: Skip the end opcode
                    Reader.ReadByte();
                    return false;
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

            Console.Write("{0} 0x{1:X2}:{2} ", new string(' ', Depth), (int)result.Opcode, result.Opcode);
            Depth += 1;

            return true;
        }

        private bool GatherChildNodesUntilEnd (out Expression[] result) {
            Console.WriteLine();

            result = null;

            var list = new List<Expression>();

            while (true) {
                Expression e;
                if (!TryReadExpression(out e))
                    return false;
                if (!TryReadExpressionBody(ref e))
                    return false;

                if (e.Opcode == Opcodes.end) {
                    result = list.ToArray();
                    return true;
                }

                list.Add(e);
            }
        }

        private bool TryReadChildNodes (ref Expression expr, int count) {
            Console.WriteLine();

            var result = new Expression[count];
            for (var i = 0; i < count; i++) {
                if (!TryReadExpression(out result[i]))
                    return false;
                if (!TryReadExpressionBody(ref result[i]))
                    return false;
            }
            expr.Body.children = result;
            return true;
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
                        break;

                    case Opcodes.block:
                    case Opcodes.loop:
                        expr.Body.U.type = (LanguageTypes)Reader.ReadLEBInt();
                        Console.Write(expr.Body.U.type);

                        if (!GatherChildNodesUntilEnd(out expr.Body.children))
                            return false;

                        break;

                    case Opcodes.@if:
                        return false;

                    case Opcodes.i32_const:
                        expr.Body.U.i32 = (int)Reader.ReadLEBInt();
                        Console.Write(expr.Body.U.i32);
                        break;

                    case Opcodes.i64_const:
                        expr.Body.U.i64 = (int)Reader.ReadLEBInt();
                        break;

                    case Opcodes.f32_const:
                        expr.Body.U.f32 = (int)Reader.ReadSingle();
                        break;

                    case Opcodes.f64_const:
                        expr.Body.U.f64 = (int)Reader.ReadDouble();
                        break;

                    case Opcodes.get_local:
                    case Opcodes.get_global:
                        expr.Body.U.u32 = (uint)Reader.ReadLEBUInt();
                        Console.Write(expr.Body.U.u32);
                        break;

                    case Opcodes.set_local:
                    case Opcodes.tee_local:
                    case Opcodes.set_global:
                        expr.Body.U.u32 = (uint)Reader.ReadLEBUInt();
                        Console.Write(expr.Body.U.u32);

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

                    default:
                        return false;
                }

                expr.State = ExpressionState.Initialized;
                return true;
            } finally {
                Console.WriteLine();
                Depth -= 1;
            }
        }
    }
}
