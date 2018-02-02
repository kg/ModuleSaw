using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ModuleSaw;
using Wasm.Model;

namespace WasmSaw {
    public class ExpressionDecoder {
        public readonly AbstractModuleReader Reader;
        public readonly TypeDecoders Types;

        public uint NumDecoded { get; private set; }

        private ArrayBinaryReader OpcodeStream, GlobalIndices, LocalIndices,
            MemoryImmediates, BrTables, BlockTypes,
            FunctionIndices, TypeIndices, BreakDepths;

        public ExpressionDecoder (TypeDecoders types) {
            Reader = types.Reader;
            Types = types;

            OpcodeStream = GetStream("opcode");
            GlobalIndices = GetStream("global_index");
            LocalIndices = GetStream("local_index");
            FunctionIndices = GetStream("function_index");
            TypeIndices = GetStream("type_index");
            MemoryImmediates = GetStream("memory_immediate");
            BrTables = GetStream("br_table");
            BlockTypes = GetStream("block_type");
            BreakDepths = GetStream("break_depth");
        }

        private ArrayBinaryReader GetStreamForOpcode (Opcodes opcode) {
            switch (opcode) {
                case Opcodes.br:
                case Opcodes.br_if:
                    return BreakDepths;
                case Opcodes.get_global:
                case Opcodes.set_global:
                    return GlobalIndices;
                case Opcodes.get_local:
                case Opcodes.set_local:
                case Opcodes.tee_local:
                    return LocalIndices;
                case Opcodes.call:
                    return FunctionIndices;
                case Opcodes.call_indirect:
                    return TypeIndices;
            }

            return null;
        }

        public bool Decode (
            out Expression e
        ) {
            if (!OpcodeStream.Read(out byte b)) {
                e = default(Expression);
                return false;
            }

            var opcode = (Opcodes)b;
            var stream = GetStreamForOpcode(opcode);

            e = new Expression {
                Opcode = opcode,
                State = ExpressionState.BodyNotRead
            };

            NumDecoded += 1;

            if (!DecodeExpressionBody(ref e, stream))
                return false;

            e.State = ExpressionState.Initialized;
            return true;
        }

        private int Depth = 0;

        private bool DecodeExpressionBody (ref Expression expr, ArrayBinaryReader stream) {
            var u32 = stream ?? Reader.UIntStream;
            var i32 = stream ?? Reader.IntStream;
            var i64 = stream ?? Reader.LongStream;
            var f32 = stream ?? Reader.SingleStream;
            var f64 = stream ?? Reader.DoubleStream;
            var mem = stream ?? MemoryImmediates;

            expr.Body.Type = ExpressionBody.Types.none;
            Depth += 1;

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
                        if (!BlockTypes.Read(out expr.Body.U.type))
                            return false;
                        expr.Body.Type = ExpressionBody.Types.type;

                        if (!DecodeChildren(ref expr))
                            return false;

                        break;

                    case Opcodes.@if:
                        if (!BlockTypes.Read(out expr.Body.U.type))
                            return false;
                        expr.Body.Type = ExpressionBody.Types.type;

                        if (!DecodeChildren(ref expr))
                            return false;

                        break;

                    case Opcodes.@else:
                        expr.Body.Type = ExpressionBody.Types.none;

                        if (!DecodeChildren(ref expr))
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
                        if (!u32.ReadU32LEB(out expr.Body.U.u32))
                            return false;

                        expr.Body.Type = ExpressionBody.Types.u32;

                        break;

                    case Opcodes.br_table:
                        if (!BrTables.ReadU32LEB(out uint target_count))
                            return false;

                        var target_table = new uint[target_count];
                        for (var i = 0; i < target_count; i++)
                            if (!BrTables.ReadU32LEB(out target_table[i]))
                                return false;

                        expr.Body.br_table = new br_table_immediate {
                            target_table = target_table
                        };

                        if (!BrTables.ReadU32LEB(out expr.Body.br_table.default_target))
                            return false;

                        expr.Body.Type = ExpressionBody.Types.br_table;
                        break;

                    case Opcodes.call_indirect:
                        if (!u32.ReadU32LEB(out expr.Body.U.u32))
                            return false;
                        expr.Body.Type = ExpressionBody.Types.u32;
                        // FIXME: reserved

                        break;

                    case Opcodes.i32_const:
                        if (!i32.ReadI32LEB(out expr.Body.U.i32))
                            return false;
                        expr.Body.Type = ExpressionBody.Types.i32;
                        break;

                    case Opcodes.i64_const:
                        if (!i64.ReadI64LEB(out expr.Body.U.i64))
                            return false;
                        expr.Body.Type = ExpressionBody.Types.i64;
                        break;

                    case Opcodes.f32_const:
                        if (!f32.Read(out expr.Body.U.f32))
                            return false;
                        expr.Body.Type = ExpressionBody.Types.f32;
                        break;

                    case Opcodes.f64_const:
                        if (!f64.Read(out expr.Body.U.f64))
                            return false;
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
                        if (!DecodeMemoryImmediate(mem, out expr.Body))
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
                        if (!DecodeMemoryImmediate(mem, out expr.Body))
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
                        if (!(stream ?? Reader.ByteStream).Read(out expr.Body.U.u8))
                            return false;
                        expr.Body.Type = ExpressionBody.Types.u1;
                        break;

                    default:
                        return false;
                }
            } finally {
                Depth -= 1;
            }

            expr.State = ExpressionState.Initialized;
            return true;
        }

        private bool DecodeMemoryImmediate (ArrayBinaryReader stream, out ExpressionBody body) {
            body = new ExpressionBody {
                Type = ExpressionBody.Types.memory
            };
            if (!stream.ReadU32LEB(out body.U.memory.flags))
                return false;
            return stream.ReadU32LEB(out body.U.memory.offset);
        }

        private bool DecodeChildren (ref Expression parent) {
            var initialDepth = Depth;
            parent.Body.Type |= ExpressionBody.Types.children;
            parent.Body.children = new List<Expression>(16);

            while (true) {
                Expression c;
                if (!Decode(out c))
                    return false;
                parent.Body.children.Add(c);

                if (Depth != initialDepth)
                    continue;

                if (
                    (c.Opcode == Opcodes.end) ||
                    (
                        (parent.Opcode == Opcodes.@if) && 
                        (c.Opcode == Opcodes.@else)
                    )
                ) {
                    return true;
                }
            }
        }

        private ArrayBinaryReader GetStream (string key) {
            return Reader.Streams.Open(key);
        }
    }
}
