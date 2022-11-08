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

        public int CurrentLimit { get; set; } = int.MaxValue;

        public uint NumDecoded { get; private set; }

        private ArrayBinaryReader OpcodeStream, GlobalIndices, LocalIndices,
            MemoryAlignments, MemoryOffsets, BrTables, BlockTypes,
            FunctionIndices, TypeIndices, BreakDepths;

        public ExpressionDecoder (TypeDecoders types) {
            Reader = types.Reader;
            Types = types;

            OpcodeStream = GetStream("opcode");
            GlobalIndices = GetStream("global_index");
            LocalIndices = GetStream("local_index");
            FunctionIndices = GetStream("function_index");
            TypeIndices = GetStream("type_index");
            MemoryAlignments = GetStream("memory_alignment");
            MemoryOffsets = GetStream("memory_offset");
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

        public bool Decode (out Expression e) {
            return Decode(out e, true, out bool temp);
        }

        public bool Decode (
            out Expression e, bool recursive, out bool needToDecodeChildren
        ) {
            needToDecodeChildren = false;

            if ((CurrentLimit <= 0) || !OpcodeStream.Read(out byte b)) {
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
            CurrentLimit--;

            if (!DecodeExpressionBody(ref e, stream, recursive, out needToDecodeChildren)) {
                if (!needToDecodeChildren)
                    return false;
            }

            if (recursive || !needToDecodeChildren) {
                e.State = ExpressionState.Initialized;
#if DEBUG
                if (!e.ValidateBody())
                    throw new Exception("Decoded expression had an invalid body " + e);
#endif
            }

            // Console.WriteLine("< decoded {0}", e);

            return true;
        }

        private bool DecodeExpressionBody (ref Expression expr, ArrayBinaryReader stream) {
            return DecodeExpressionBody(ref expr, stream, true, out bool temp);
        }

        private uint MostRecentLocalIndex;

        private bool DecodeExpressionBody (ref Expression expr, ArrayBinaryReader stream, bool recursive, out bool needToDecodeChildren) {
            var u32 = stream ?? Reader.UIntStream;
            var i32 = stream ?? Reader.IntStream;
            var i64 = stream ?? Reader.LongStream;
            var f32 = stream ?? Reader.SingleStream;
            var f64 = stream ?? Reader.DoubleStream;

            expr.Body.Type = ExpressionBody.Types.none;
            needToDecodeChildren = false;

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
                case Opcodes.@if:
                    if (!BlockTypes.Read(out expr.Body.U.type))
                        return false;
                    expr.Body.Type = ExpressionBody.Types.type;

                    if (recursive) {
                        if (!DecodeChildren(ref expr))
                            return false;
                    } else {
                        needToDecodeChildren = true;
                        return false;
                    }

                    break;

                case Opcodes.@else:
                    expr.Body.Type = ExpressionBody.Types.none;

                    if (recursive) {
                        if (!DecodeChildren(ref expr))
                            return false;
                    } else {
                        needToDecodeChildren = true;
                        return false;
                    }

                    break;

                case Opcodes.call:
                case Opcodes.br:
                case Opcodes.br_if:
                case Opcodes.get_global:
                case Opcodes.set_global:
                    if (!u32.ReadU32LEB(out expr.Body.U.u32))
                        return false;

                    expr.Body.Type = ExpressionBody.Types.u32;

                    break;

                case Opcodes.get_local:
                case Opcodes.set_local:
                case Opcodes.tee_local:
                    if (!u32.ReadU32LEB(out expr.Body.U.u32))
                        return false;

                    MostRecentLocalIndex = expr.Body.U.u32;

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
                    if (!DecodeMemoryImmediate(OpcodesInfo.MemorySizeForOpcode[expr.Opcode], out expr.Body))
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
                    if (!DecodeMemoryImmediate(OpcodesInfo.MemorySizeForOpcode[expr.Opcode], out expr.Body))
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

                default: {
                    if (expr.Opcode >= (Opcodes)ExpressionEncoder.FakeOpcodes.FirstFakeOpcode)
                        return DecodeFakeOpcode(ref expr, stream, recursive, ref needToDecodeChildren);

                    return false;
                }
            }

            expr.State = ExpressionState.Initialized;

#if DEBUG
            if (!expr.ValidateBody())
                throw new Exception("Decoded expression had an invalid body " + expr);
#endif
            return true;
        }

        private bool DecodeFakeOpcode (ref Expression expr, ArrayBinaryReader stream, bool recursive, ref bool needToDecodeChildren) {
            switch (expr.Opcode) {
                case ExpressionEncoder.FakeOpcodes.read_prior_local: {
                    expr.Opcode = Opcodes.get_local;
                    expr.Body.Type = ExpressionBody.Types.u32;
                    expr.Body.U.u32 = MostRecentLocalIndex;
                    return true;
                }

                case ExpressionEncoder.FakeOpcodes.ldc_i32_zero:
                case ExpressionEncoder.FakeOpcodes.ldc_i32_one:
                case ExpressionEncoder.FakeOpcodes.ldc_i32_two:
                case ExpressionEncoder.FakeOpcodes.ldc_i32_minus_one: {
                    expr.Body.U.i32 = ExpressionEncoder.FakeOpcodes.ReverseConstants[expr.Opcode];
                    expr.Body.Type = ExpressionBody.Types.i32;
                    expr.Opcode = Opcodes.i32_const;
                    return true;
                }

                case ExpressionEncoder.FakeOpcodes.i32_load_natural: {
                    var offset = expr.Body.U.u32;
                    expr.Opcode = Opcodes.i32_load;
                    expr.Body.Type = ExpressionBody.Types.memory;
                    expr.Body.U.memory.offset = offset;
                    expr.Body.U.memory.alignment_exponent = 2;
                    return true;
                }

                case ExpressionEncoder.FakeOpcodes.i32_store_natural: {
                    var offset = expr.Body.U.u32;
                    expr.Opcode = Opcodes.i32_store;
                    expr.Body.Type = ExpressionBody.Types.memory;
                    expr.Body.U.memory.offset = offset;
                    expr.Body.U.memory.alignment_exponent = 2;
                    return true;
                }

                default:
                    throw new Exception ("Unhandled fake opcode " + expr.Opcode);
                    return false;
            }
        }

        private bool DecodeMemoryImmediate (uint natural_alignment, out ExpressionBody body) {
            var mem = new memory_immediate {
                EXT_natural_alignment = natural_alignment,
                EXT_natural_exponent = (uint)Math.Log(natural_alignment, 2)
            };

            if (
                MemoryAlignments.ReadI32LEB(out mem.EXT_relative_alignment_exponent) &&
                MemoryOffsets.ReadU32LEB(out mem.offset)
            ) {
                var ae = mem.EXT_natural_exponent + mem.EXT_relative_alignment_exponent;
                // FIXME
                if (ae < 0) 
                    throw new Exception();

                mem.alignment_exponent = (uint)ae;
                body = new ExpressionBody {
                    Type = ExpressionBody.Types.memory,
                    U = {
                        memory = mem
                    }
                };
                return true;
            } else {
                body = default(ExpressionBody);
                return false;
            }

            /*
            memory.alignment_exponent = (uint)Reader.ReadLEBUInt();
            memory.offset = (uint)Reader.ReadLEBUInt();

            memory.EXT_natural_alignment = natural_alignment;
            memory.EXT_natural_exponent = (uint)Math.Log(natural_alignment, 2);
            memory.EXT_relative_alignment_exponent = (int)memory.EXT_natural_exponent - (int)memory.alignment_exponent;
            */
        }

        private bool DecodeChildren (ref Expression parent) {
            parent.Body.Type |= ExpressionBody.Types.children;
            parent.Body.children = parent.Body.children ?? new List<Expression>(16);

            return DecodeChildrenNonRecursive(parent.Body.children);
        }

        private bool DecodeChildrenNonRecursive (List<Expression> target) {
            var stack = new Stack<(List<Expression>, bool)>();
            var current = target;

            while (true) {
                bool decoded = Decode(out Expression c, false, out bool needToDecodeChildren);

                if (!decoded || (c.Opcode == Opcodes.end)) {
                    if (!decoded) {
                        if (CurrentLimit <= 0)
                            return true;
                        else
                            return false;
                    }

                    current.Add(c);

                    bool wasElse = false;
                    do {
                        if (stack.Count <= 0)
                            return true;

                        var tup = stack.Pop();
                        current = tup.Item1;
                        wasElse = tup.Item2;
                    } while (wasElse);

                    continue;
                }

                if (needToDecodeChildren) {
                    c.Body.Type |= ExpressionBody.Types.children;
                    c.Body.children = c.Body.children ?? new List<Expression>(16);
                    current.Add(c);
                    stack.Push((current, c.Opcode == Opcodes.@else));
                    current = c.Body.children;
                    continue;
                } else
                    current.Add(c);
            }
        }

        private ArrayBinaryReader GetStream (string key) {
            return Reader.Streams.Open(key);
        }
    }
}
