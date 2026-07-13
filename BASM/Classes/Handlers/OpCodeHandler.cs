using BASM.Classes.DS;
using BASM.Classes.Managers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace BASM.Classes.Handlers {
    public class OpCodeHandler {
        static byte BITS => OpCodeManager.BITS;
        const byte DEBUG = 1;
        //// Cached
        public static OPCODE _opcode = new();
        public static ARG _dst = new();
        public static ARG _src = new();
        //



        static DerefferedLabel drlbl => OpCodeManager.drlbl;
        static Dictionary<string, OPCODE> opcodes = new Dictionary<string, OPCODE>();

        static byte[] ToBytes(long n, byte bits = 16, byte w = 0) {
            List<byte> bytes = new();
            long b = n;
            bytes.Add((byte)(b & 0xFF));
            if (bits == 32) {
                b >>= 8;
                bytes.Add((byte)(b & 0xFF));
            }
            if (w == 1) {
                b >>= 8;
                bytes.Add((byte)(b & 0xFF));
                if (bits == 32) {
                    b >>= 8;
                    bytes.Add((byte)(b & 0xFF));
                }
            }
            return bytes.ToArray();
        }
        public static bool IsDB_KW(string s) {
            if (s.Length < 2) return false;
            if (s[0] != 'd') return false;
            switch (s[1]) {
                case 'b':
                case 'w':
                case 'd':
                case 'q':
                    return true;
                default:
                    break;
            }
            return false;
        }
        public static bool IsRES_KW(string s) {
            if (s.Length < 4) return false;
            if (!(
                s[0] == 'r'&&
                s[1] == 'e'&&
                s[2] == 's'
                )) return false;
            switch (s[3]) {
                case 'b':
                case 'w':
                case 'd':
                case 'q':
                    return true;
                default:
                    break;
            }
            return false;
        }
        public static void Load(string file) {
            long gid = 0;
            if (!File.Exists(file)) {
                Debugger.Warn("Could not find File:\"{0}\"",file);
                file = "../../../Resources/" + file;
                Debugger.Log("looking for File:\"{0}\"", file);
            }
            if (!File.Exists(file)) {
                throw new FileNotFoundException($"Could not locate file:\"{file}\"");
            }

            File.ReadAllLines(file).ToList().ForEach(line => {
                if (line.StartsWith(';')) return;
                var parts = line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (line.StartsWith('#')) {
                    if (line.StartsWith("#GID")) {
                        gid = MemoryHandler.ParseIMM(parts[1]).imme;
                    }
                    return;
                }
                if (parts.Length < 3) return; // skip invalid lines
                var opcode = new OPCODE();
                opcode.name = parts[0].ToLower();
                opcode.typeId = gid; //Convert.ToInt64(parts[1], 16);
                opcode.flags = Convert.ToInt64(parts[2], 16);
                opcode.codes = parts.Skip(3).Select(p => Convert.ToInt64(p, 16)).ToArray();
                opcodes[parts[0].ToLower()] = opcode;
            });

            OPCODE.LogGIDS();
        }

        public static bool TryGet(string str, out OPCODE code) => opcodes.TryGetValue(str, out code);
        public static OPCODE parse(string opcodeStr) {
            if (!OpCodeHandler.TryGet(opcodeStr, out var code)) {
                Console.WriteLine($"Unknown opcode: {opcodeStr} '");
                throw new InvalidOperationException($"Unknown opcode: {opcodeStr}");
            }
            return code;
        }
        public static void ParseLegacyGID( 
                ARG src,
                ARG dst,
                OPCODE code,
                ref long opcode,
                ref byte d,
                ref byte w,
                ref byte mod,
                ref byte rm,
                ref byte reg,
                ref byte opcodeBranch,
                ref bool rmmField,
                ref bool immField
            ) {
            if (rm == 0 && code.ALU && code.typeId == OPCODE.XCHG_ID && w == 1) { // ax with alu
                opcode = code.codes[3] + reg;
                opcodeBranch = 1;
                rmmField = false;
            } else if (rm == 0 && code.ALU && immField) { // ax with alu
                opcode = code.codes[3];
                opcode += w;
                opcodeBranch = 1;
                rmmField = false;
            } else if (code.ALU && code.ZOP) {
                opcode = code.codes[0];
                opcode <<= 1; opcode += w;
                opcode <<= 3; opcode += code.codes[2];
                opcodeBranch = 2;
                rmmField = false;
            } else if (code.ALU && code.OOP) { // one operand
                opcode = code.codes[0];
                if (immField) opcode = code.codes[1];
                reg = (byte)code.codes[2];
                opcode += w;
                opcodeBranch = 3;
            } else if (code.CU && code.OOP) { // one operand control inst, push es
                if (src.usec == 1) {
                    opcode = code.codes[3 + rm];
                    //Debugger.Warn($"\tUsing segment register {rm} in ALU instruction: {srcStr} as {opcode}");
                    opcodeBranch = 41;
                } else if (rm == 0xff) {
                    opcode = code.codes[1];
                    opcodeBranch = 42;
                } else {
                    opcode = code.codes[0];
                    opcode += rm;
                    opcodeBranch = 43;
                }
                rmmField = false;
            } else if (code.OOP) { // one operand
                opcode = code.codes[0];
                if (immField) opcode = code.codes[1];
                else reg = (byte)code.codes[2];
                opcodeBranch = 5;
            } else if (immField) {  // mov imme
                opcode = code.codes[1];
                opcode <<= 1; opcode += w;
                opcode <<= 3; opcode += rm;
                opcodeBranch = 6;
                rmmField = false;
            } else if (code.ZOP) { // zero operand
                opcode = code.codes[0];
                opcodeBranch = 7;
            } else { // alu with reg/mem 
                opcode = code.codes[0];
                opcode += ((d << 1) + w);
                opcodeBranch = 8;
            }
        }


        public static ARG ParseOperand(string src, byte keyw = 0) {
            if (src.Length == 0) return new ARG();
           
            if (src.StartsWith('[') && src.EndsWith(']')) return MemoryHandler.Parse(src, keyw);
            if (MemoryHandler.TryParseIMM(src, out var imm, keyw)) return imm;
            if (RegistersHandler.TryParse(src, out var reg, keyw)) return reg;
            if (MemoryHandler.StartsWithImmKeyWord(src, out var k, out var i)) 
                return ParseOperand(src.Substring(i, src.Length - i), k);
            // pass some action and data to LH so it callbacks when label is found
            return LabelHandler.Parse(src, keyw);
        }

        public byte[] ParseZOP(OPCODE code) => [(byte)code.codes[0]];
        public static byte[] ParsePREF(OPCODE code, string[] words) {
            if (words.Length == 0) return [(byte)code.codes[0]];

            List<byte> bytes = new();
            bytes.Add((byte)code.codes[0]);

            List<string> ws = new(words);
            ws.RemoveAt(0);
            var opcodeStr = ws[0];

            if (!OpCodeHandler.TryGet(opcodeStr, out var _code)) {
                Console.WriteLine($"Unknown opcode: {opcodeStr} '{ws[1]}' '{ws[2]}'");
                throw new InvalidOperationException($"Unknown opcode: {opcodeStr}");
            }
            return Parse(_code, ws.ToArray());
        }
          
        public static byte[] Parse(OPCODE code, ARG dst, ARG src) {


            byte bits = BITS;
            int maxSize = 0;
            if (dst.isReg && dst.size > maxSize) maxSize = dst.size;
            if (src.isReg && src.size > maxSize) maxSize = src.size;

            byte w = 0;
            if (maxSize > 1) w = 1;

            byte d = 1; // 1 = to reg field
            bool oop = false;
            //Debugger.Info($"\tBefore swap {dst.type} {src.type}");
            //if (dstStr.Length > 0 && src.isReg) {
            if (src.isReg) {
                d = 0;
                (src, dst) = (dst, src);
            }
            //Debugger.Info($"\tAfter swap {dst.type} {src.type}");

            ARG Rm = src;
            ARG Reg = dst;

            if (Reg.type == RM.Type || Reg.type == SIB.Type) (Rm, Reg) = (Reg, Rm);
            else if (Rm.type == IMM.Type || Rm.type == LABEL.Type) (Rm, Reg) = (Reg, Rm);
            else if (Reg.type == Rm.type && Rm.type == REG.Type && (Rm.usec & 1) > 0) (Rm, Reg) = (Reg, Rm);

            if(Reg.size == 4 || Rm.size == 4) Rm.usec |= 4; // force 32 bit for 32 bit registers
            if (Rm.isSizeKeyW) {
                if (Rm.isByte) w = 0;
                else if (Rm.isWord) w = 1;
                else if (Rm.isDWord) { w = 1; Rm.usec |= 4; } else if (Rm.isQWord) { w = 1; Rm.usec |= 4; }
            }


            if (!Reg.isSolved) {
                drlbl.state |= 1;
                drlbl.labels.Enqueue(Reg.label);
            }
            if (!Rm.isSolved) {
                drlbl.state |= 1;
                drlbl.labels.Enqueue(Rm.label);
            }
            //Debugger.Info($"\tAfter swap {Reg.type} {Rm.type}");

            byte reg = (byte)(oop ? -1 : Reg.value);
            byte rm = Rm.value;
            byte mod = Rm.mod;

            bool immField = false;
            bool relImm = true;
            string dis = "";
            if (src.type == IMM.Type || src.type == LABEL.Type) {
                immField = true;
                var imm = ((IMM)(src)).imme;
                dis = $"imme: 0x{imm:X}";
            } else if (src.type == RM.Type && mod != 3 && mod != 0) {
                var imm = ((IMM)(src)).imme;
                dis = $"disp: 0x{imm:X}";
            }

            var opcode = code.codes[0];


            byte opcodeBranch = 0;
            bool rmmField = !code.ZOP;
            switch (code.typeId) {
                case OPCODE.MOV_ID:
                    opcode = code.codes[0];
                    if (immField) {
                        if (mod == 3) {
                            opcode = code.codes[1];
                            opcode <<= 1; opcode += w;
                            opcode <<= 3; opcode += rm;
                            rmmField = false;
                        } else {
                            //MEM MOV 
                            opcode = code.codes[2] + w;
                            rmmField = true;
                        }
                    } else if(code.FLAG_NZ(0x10)) {
                        if (Rm.size == Reg.size) return Parse(OpCodeHandler.parse("mov"), dst, src);
                        if (Rm.isMem) {
                            if (Rm.size > Reg.size)
                            throw new InvalidOperationException($"Invalid MOV operation: cannot move from memory of size {Rm.size} to register of size {Reg.size}");
                        } else if (Reg.size > Rm.size) throw new InvalidOperationException($"Invalid MOV operation: cannot move from register of size {Reg.size} to memory of size {Rm.size}");
                        opcode = code.codes[0];

                        if (Rm.isMem && Rm.size > 1) opcode += 1; 
                        else if(Reg.size > 1) opcode += 1; 

                        opcodeBranch = 8;  
                    } else {
                        opcode = code.codes[0]; 
                        opcode += ((d << 1) + w);
                        opcodeBranch = 8;
                        //SEG MOV
                        if ((Reg.usec & 1) > 0) opcode = code.codes[3] + (d << 1); 
                    }
                    break;

                case OPCODE.XCHG_ID:
                    opcode = code.codes[0];
                    if (rm == 0 && w == 1) { // ax with xchg
                        opcode = code.codes[3] + reg;
                        rmmField = false;
                    }
                    break;
                case OPCODE.ADD_ID:
                    if (immField) {
                        if (rm == 0) { // ax with alu
                            opcode = code.codes[3];
                            opcode += w;
                            rmmField = false;
                        } else {
                            opcode = code.codes[1];
                            reg = (byte)code.codes[2];
                        }
                    } else {
                        opcode = code.codes[0];
                        opcode += ((d << 1) + w);
                    }
                    break;
                case OPCODE.PUSH_ID:
                    w = 1;
                    if ((src.usec & 1) > 0) {
                        opcode = code.codes[3 + rm];
                        //Debugger.Warn($"\tUsing segment register {rm} in ALU instruction: {srcStr} as {opcode}");
                        opcodeBranch = 41;
                    } else if (immField) {
                        opcode = code.codes[1];
                        opcodeBranch = 42;
                    } else {
                        opcode = code.codes[0];
                        opcode += rm;
                        opcodeBranch = 43;
                    }
                    rmmField = false;
                    break;
                case OPCODE.CALL_ID: {
                        if (Reg.keyw == 2) code = OpCodeHandler.parse("callf");
                        if (Rm.keyw == 2) code = OpCodeHandler.parse("callf");

                        drlbl.Rel = true;
                        w = 1;
                        if (immField) {
                            opcode = code.codes[1];
                            rmmField = false;
                            if (Reg.keyw == 0) Reg.imme -= (drlbl.IP + 2 + w);
                        } else {
                            opcode = code.codes[0];
                            reg = (byte)code.codes[2];
                        }
                    }
                    break;
                case OPCODE.JMP_ID: {
                        if (Reg.keyw == 2) code = OpCodeHandler.parse("jmpf");
                        if (Rm.keyw == 2) code = OpCodeHandler.parse("jmpf");

                        if (immField) {

                            opcode = code.codes[1];
                            rmmField = false;
                            if (Rm.keyw == 0) {
                                drlbl.Rel = true;
                            }
                            if ((opcode & 1) == 0) {
                                w = 1;
                                relImm = false;
                            } else {
                                if (Reg.keyw == 0) {
                                    Reg.imme -= (drlbl.IP + 2);
                                    if (LABEL.relIP_Size(Reg.imme) > 1) {
                                        w = 1;
                                        Reg.imme -= w;
                                        opcode = code.codes[3];
                                    }
                                }

                            }

                            if ((opcode & 1) > 0) {

                            } // else jmpf
                        } else {
                            opcode = code.codes[0];
                            reg = (byte)code.codes[2];
                        }
                    }
                    break;
                case OPCODE.INC_ID:
                    opcode = code.codes[0] + w;
                    if (immField) throw new("Immediate not allowed for this operation");
                    reg = (byte)code.codes[2];
                    break;
                case OPCODE.ROL_ID:
                    opcode = code.codes[0];
                    if (immField) opcode = code.codes[1];
                    reg = (byte)code.codes[2];
                    break;
                case OPCODE.IO_ID:
                    opcode = code.codes[0];
                    if (immField) opcode = code.codes[1];
                    opcode += w;
                    break;
                case OPCODE.JC_ID:
                    w = 0;
                    if (Reg.keyw == 0) {
                        var relIp = LABEL.relIP(drlbl.IP, Reg.imme, 2);
                        if (LABEL.relIP_Size(relIp) > 1) {
                            var bytes = new List<byte>();
                            if (code.FLAG_Z(1)) {
                                relIp = LABEL.relIP(drlbl.IP, Reg.imme, 4);
                                bytes.AddRange(NumberSystem.ToByteArray_BE((ulong)(0xF80+(code.codes[1]&0xF))));
                                bytes.AddRange(NumberSystem.ToByteArray_LE(relIp, 2));
                                return bytes.ToArray();
                            }
                            relIp = LABEL.relIP(drlbl.IP, Reg.imme, 5);

                            bytes = new List<byte>() {
                                (byte)(code.codes[1]+1),
                                3,
                                (byte)OpCodeHandler.parse("jmp").codes[3] };

                            bytes.AddRange(NumberSystem.ToByteArray_LE(relIp, 2));
                            return bytes.ToArray();
                        }
                        Reg.imme = relIp;
                    }
                    drlbl.Rel = true;
                    opcode = code.codes[1];
                    rmmField = false;
                    break;
                case OPCODE.INT_ID:
                    drlbl.Rel = true;
                    opcode = code.codes[1];
                    rmmField = false;
                    break;
                default:
                ///LEGACY
                case OPCODE.DEF_ID:
                    OpCodeHandler.ParseLegacyGID(
                        src,
                        dst,
                        code,
                        ref opcode,
                        ref d,
                        ref w,
                        ref mod,
                        ref rm,
                        ref reg,
                        ref opcodeBranch,
                        ref rmmField,
                        ref immField
                    );
                    break;
            }

            List<byte> inst = new List<byte>();
            string pref = "";
            byte x66 = bits, x67 = bits;
            // prefixes
            if ((Rm.usec & 8) != 0) {
                var segPre = new byte[] { 0x26, 0x2E, 0x36, 0x3E, 0x64, 0x65 };
                inst.Add(segPre[Rm.seg]);
            }
            if ((Rm.usec & 2) != 0 && BITS == 16) {
                inst.Add(0x67);
                x67 = 32;
            }
            if (((Rm.usec & 4) != 0) && BITS == 16) {
                inst.Add(0x66);
                x66 = 32;
            }

            if (inst.Count > 0) {
                bits = 32;
                var sb = new StringBuilder("prefixes:");

                foreach (var p in inst) sb.Append($"{p:X} ");

                pref = sb.ToString();
            }
            if (DEBUG > 0) Debugger.Warn($"\tRead opcode: {code.name} as {opcode:X} from branch {opcodeBranch}");
            inst.AddRange(NumberSystem.ToByteArray_BE((ulong)opcode));

            if ((code.typeId == OPCODE.JMP_ID ||
                code.typeId == OPCODE.JC_ID ||
                code.typeId == OPCODE.CALL_ID) && relImm) {

            }
            if (rmmField) {

                int rmm = 0; rmm += mod;
                rmm <<= 3; rmm += reg;
                rmm <<= 3; rmm += rm;
                inst.Add((byte)rmm);
            }

            if ((Rm.usec & 2) != 0 && rm == 4) inst.Add(Rm.sib);


            if (rmmField) {
                if (mod != 3 && mod != 0) {
                    var imm = Rm.imme;
                    inst.AddRange(ToBytes(imm, x67, (byte)((mod == 2) ? 1 : 0)));
                } else if (mod == 0 && rm == 6) {
                    var imm = Rm.imme;
                    inst.AddRange(ToBytes(imm, x67, 1));
                }
            }
            if (immField) {

                if (Reg is IMM imm) {
                    var v = imm.imme;
                    inst.AddRange(ToBytes(v, x66, w));

                    if (imm.usec == 1) {
                        v = imm.imme1;
                        inst.AddRange(ToBytes(v, x66, w));
                    }
                }
            }


            var l = $"{pref}opcode:{opcode} d:{d} w:{w} mod:{mod} reg:{reg} rm:{rm} {dis}";
            if (DEBUG > 0) Debugger.Log($"\tParsed instruction: {l}");

            return [.. inst];
        }
        public static byte[] Parse(OPCODE code, string[] words) {
            if (code.typeId == OPCODE.NOP_ID) {
                if (code.PREF) return ParsePREF(code, words);
                if (code.ZOP) return [(byte)code.codes[0]];
            }
            var dstStr = words[1];
            var srcStr = words[2];
            if(DEBUG > 0) Console.WriteLine($"\tParsed line: opcode='{words[0]}', dst='{words[1]}', src='{words[2]}'");

            _dst = ParseOperand(dstStr);
            _src = ParseOperand(srcStr);

            if (_dst.isMem && _src.isMem) {
                Console.WriteLine($"Invalid operands: both dst and src cannot be memory references.");
                throw new InvalidOperationException($"Invalid operands: both dst and src cannot be memory references.");
            }
            return Parse(code, _dst, _src);
        }
    }
}
