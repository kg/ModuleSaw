using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ModuleSaw;

namespace Wasm.Model {
    public interface ExpressionReaderListener {
        void BeginHeader ();
        void EndHeader (ref Expression expression, bool successful);

        void BeginBody (ref Expression expression, bool readingChildNodes);
        void EndBody (ref Expression expression, bool readChildNodes, bool successful);
    }

    public class ExpressionReader {
        public readonly BinaryReader Reader;
        public uint NumRead { get; private set; }

        private readonly Stream BaseStream;
        private readonly long BaseStreamLength;

        public ExpressionReader (BinaryReader reader) {
            Reader = reader;
            BaseStream = reader.BaseStream;
            BaseStreamLength = BaseStream.Length;
        }

        public bool TryReadInitExpr (out Expression result) {
            if (!TryReadExpression(out result))
                return false;

            if (!TryReadExpressionBody(ref result)) {
                // HACK
                throw new Exception("Failed to read body of " + result.Opcode);
            }

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

            return (Reader.ReadByteButFast() == (byte)Opcodes.end);
        }

        int Depth = 0;

        public bool TryReadExpression (out Expression result, ExpressionReaderListener listener = null) {
            listener?.BeginHeader();
            result = default(Expression);

            if (BaseStream.Position >= BaseStreamLength) {
                listener?.EndHeader(ref result, false);
                return false;
            }

            result.Opcode = (Opcodes)Reader.ReadByteButFast();
            if (!OpcodesInfo.KnownOpcodes[(int)result.Opcode])
                throw new Exception($"Unrecognized opcode {result.Opcode} 0x{(int)result.Opcode:X2}");
            result.State = ExpressionState.BodyNotRead;

            NumRead += 1;

            // Console.WriteLine("{1} read {0}", result, new string('<', Math.Min(Depth + 1, 32)));
            listener?.EndHeader(ref result, true);
            return true;
        }

        public bool TryReadExpressionBody (ref Expression expr, ExpressionReaderListener listener = null) {
            bool needToReadChildren;

            if (TryReadExpressionBodyNonRecursive(ref expr, out needToReadChildren, listener))
                return true;

            if (!needToReadChildren)
                return false;

            return ReadExpressionChildren(ref expr, listener);
        }

        private bool ReadExpressionChildren (ref Expression expr, ExpressionReaderListener listener) {
            var pseudostack = new List<Expression>();
            var parent = expr;
            Expression child;

            Depth++;
            while (TryReadExpression(out child, listener)) {
                if (!TryReadExpressionBodyNonRecursive(ref child, out bool needToReadChildren, listener)) {
                    if (!needToReadChildren)
                        // FIXME: Fire EndBody events
                        return false;

                    Depth++;
                    pseudostack.Add(parent);
                    parent = child;
                    continue;
                }

                parent.Body.children.Add(child);

                // FIXME: Special-case handling for else?
                if (child.Opcode == Opcodes.end) {
                    parent.State = ExpressionState.Initialized;
                    listener?.EndBody(ref parent, true, true);

                    bool wasElse = false;
                    do {
                        if (pseudostack.Count == 0) {
                            expr.State = ExpressionState.Initialized;
                            return true;
                        }

                        var outer = pseudostack[pseudostack.Count - 1];
                        outer.Body.children.Add(parent);
                        wasElse = parent.Opcode == Opcodes.@else;
                        parent = outer;
                        pseudostack.RemoveAt(pseudostack.Count - 1);
                        Depth--;
                    } while (wasElse);
                }
            }

            // The last opcode in a function body may be either 'end' or 'unreachable'
            if (
                (child.Opcode != Opcodes.end) && 
                (child.Opcode != Opcodes.unreachable)
            )
                return false;
            
            for (int i = pseudostack.Count - 1; i >= 0; i--) {
                var item = pseudostack[i];
                listener?.EndBody(ref item, true, true);
            }

            return true;
        }

        private bool TryReadExpressionBodyNonRecursive (ref Expression expr, out bool needToReadChildren, ExpressionReaderListener listener) {
            try {
                return TryReadExpressionBodyNonRecursive_Impl(ref expr, out needToReadChildren, listener);
            } catch {
                expr.State = ExpressionState.InitializedWithError;
                needToReadChildren = false;
                throw;
            }
        }

        private bool TryReadExpressionBodyNonRecursive_Impl (ref Expression expr, out bool needToReadChildren, ExpressionReaderListener listener) {
            if (expr.State == ExpressionState.Uninitialized)
                throw new ArgumentException("Uninitialized expression");

            bool readError = false;

            switch (expr.Opcode) {
                case Opcodes.block:
                case Opcodes.loop:
                case Opcodes.@if:
                    listener?.BeginBody(ref expr, true);
                    expr.Body.U.type = (LanguageTypes)Reader.ReadByteButFast();
                    expr.Body.Type = ExpressionBody.Types.type | ExpressionBody.Types.children;
                    expr.Body.children = new List<Expression>(16);
                    needToReadChildren = true;
                    return false;
                case Opcodes.@else:
                    listener?.BeginBody(ref expr, true);
                    expr.Body.Type = ExpressionBody.Types.children;
                    expr.Body.children = new List<Expression>(16);
                    needToReadChildren = true;
                    return false;
                default:
                    needToReadChildren = false;
                    break;
            }

            listener?.BeginBody(ref expr, false);
            ulong? operand_u;
            long? operand_l;

            switch (expr.Opcode) {
                case Opcodes.nop:
                case Opcodes.unreachable:
                case Opcodes.end:
                case Opcodes.@return:
                case Opcodes.drop:
                case Opcodes.select:
                    break;

                case Opcodes.call:
                case Opcodes.br:
                case Opcodes.br_if:
                case Opcodes.get_global:
                case Opcodes.set_global:
                case Opcodes.get_local:
                case Opcodes.set_local:
                case Opcodes.tee_local:
                    operand_u = Reader.ReadLEBUInt();
                    if (!operand_u.HasValue) {
                        readError = true;
                        break;
                    }

                    expr.Body.U.u32 = (uint)operand_u;
                    expr.Body.Type = ExpressionBody.Types.u32;

                    break;

                case Opcodes.br_table:
                    operand_u = Reader.ReadLEBUInt();
                    if (!operand_u.HasValue) {
                        readError = true;
                        break;
                    }

                    var target_count = (uint)operand_u;
                    if (target_count > 32767)
                        throw new Exception($"br_table had {target_count} targets");

                    var target_table = new uint[target_count];
                    for (var i = 0; i < target_count; i++) {
                        var lu = Reader.ReadLEBUInt();
                        if (!lu.HasValue) {
                            readError = true;
                            break;
                        } else {
                            target_table[i] = (uint)lu.Value;
                        }
                    }

                    var dt = Reader.ReadLEBUInt();
                    if (!dt.HasValue)
                        readError = true;

                    expr.Body.br_table = new br_table_immediate {
                        target_table = target_table,
                        default_target = (uint)(dt ?? 0)
                    };

                    expr.Body.Type = ExpressionBody.Types.br_table;
                    break;

                case Opcodes.call_indirect:
                    operand_u = Reader.ReadLEBUInt();
                    if (!operand_u.HasValue) {
                        readError = true;
                        break;
                    }

                    expr.Body.U.u32 = (uint)operand_u;
                    expr.Body.Type = ExpressionBody.Types.u32;
                    // HACK
                    var reserved = Reader.ReadLEBUInt();

                    break;

                case Opcodes.i32_const:
                    operand_l = Reader.ReadLEBInt();
                    if (!operand_l.HasValue) {
                        readError = true;
                        break;
                    }

                    expr.Body.U.i32 = (int)operand_l;
                    expr.Body.Type = ExpressionBody.Types.i32;
                    break;

                case Opcodes.i64_const:
                    operand_l = Reader.ReadLEBInt();
                    if (!operand_l.HasValue) {
                        readError = true;
                        break;
                    }

                    expr.Body.U.i64 = (int)operand_l;
                    expr.Body.Type = ExpressionBody.Types.i64;
                    break;

                case Opcodes.f32_const:
                    expr.Body.U.f32 = Reader.ReadSingle();
                    expr.Body.Type = ExpressionBody.Types.f32;
                    break;

                case Opcodes.f64_const:
                    expr.Body.U.f64 = Reader.ReadDouble();
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
                    if (!ReadMemoryImmediate(OpcodesInfo.MemorySizeForOpcode[expr.Opcode], out expr.Body.U.memory)) {
                        listener?.EndBody(ref expr, false, false);
                        return false;
                    }

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
                    if (!ReadMemoryImmediate(OpcodesInfo.MemorySizeForOpcode[expr.Opcode], out expr.Body.U.memory)) {
                        listener?.EndBody(ref expr, false, false);
                        return false;
                    }

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
                    operand_u = Reader.ReadLEBUInt();
                    if (!operand_u.HasValue) {
                        readError = true;
                        break;
                    }

                    expr.Body.U.u32 = (uint)operand_u;
                    expr.Body.Type = ExpressionBody.Types.u1;
                    break;

                default:
                    return false;
            }

            expr.State = readError ? ExpressionState.InitializedWithError : ExpressionState.Initialized;
            listener?.EndBody(ref expr, false, !readError);

            if (readError)
                throw new EndOfStreamException($"Hit EOF while reading {expr.Opcode}");
            return !readError;
        }

        public bool ReadMemoryImmediate (uint natural_alignment, out memory_immediate memory) {
            ulong? ae = Reader.ReadLEBUInt(), o = Reader.ReadLEBUInt();
            if (!ae.HasValue || !o.HasValue) {
                memory = default;
                return false;
            }

            memory.alignment_exponent = (uint)ae;
            memory.offset = (uint)o;

            // HACK: We want to encode memory immediates' alignments relative to the natural alignment
            //  of the load/store operation. Most load/store operations will be naturally aligned, so
            //  doing this will mean all their alignments are encoded as 0 instead of 1/2/4/8. This makes
            //  the alignment stream more compressible.
            memory.EXT_natural_alignment = natural_alignment;
            memory.EXT_natural_exponent = (uint)Math.Log(natural_alignment, 2);
            memory.EXT_relative_alignment_exponent = (int)memory.alignment_exponent - (int)memory.EXT_natural_exponent;

            return true;
        }
    }
}
