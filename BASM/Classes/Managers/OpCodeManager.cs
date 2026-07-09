using BASM.Classes.DS;
using BASM.Classes.Handlers;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks; 

namespace BASM.Classes.Managers {
    public class OpCodeManager { 
        static byte BITS = 16;
        static string opcodeFile = "opcodes.asm";
 
        static MemoryHandler memHandler = new MemoryHandler();

        public static byte NASM = 0x1; // flags to follow nasm syntax
        static byte[] ToBytes(long n,byte bits = 16, byte w = 0) {
            List<byte> bytes = new();
            long b = n;
            bytes.Add((byte)(b & 0xFF));
            if (bits == 32) {
                b >>= 8;
                bytes.Add((byte)(b & 0xFF));
            }
            if(w == 1) {
                b >>= 8;
                bytes.Add((byte)(b & 0xFF));
                if (bits == 32) {
                    b >>= 8;
                    bytes.Add((byte)(b & 0xFF));
                }
            }
            return bytes.ToArray();
        }
        public static ARG ParseOperand(string src, byte keyw = 0) {
            if (src.Length == 0) return new ARG();
            if (MemoryHandler.StartsWithImmKeyWord(src,out var k,out var i)) {
                return ParseOperand(src.Substring(i, src.Length - i),k);
            }
            if (src.StartsWith('[') && src.EndsWith(']')) return memHandler.Parse(src,keyw);
            else if (MemoryHandler.TryParseIMM(src,out var imm,keyw)) return imm;
            else if (RegistersHandler.TryParse(src,out var reg,keyw)) return reg;
            // pass some action and data to LH so it callbacks when label is found
            return LabelHandler.Parse(src,keyw);
        }

        public byte[] ParseZOP(OPCODE code) => [(byte)code.codes[0]];
        public static byte[] ParsePREF(OPCODE code, string[] words) {
            if (words.Length == 0) return [(byte)code.codes[0]];

            List<byte> bytes = new();
            bytes.Add((byte)code.codes[0]);

            List<string> ws = new(words);
            ws.RemoveAt(0);
            var opcodeStr = ws[0];

            if (!OpCodeHandler.TryGet(opcodeStr,out var _code)) {
                Console.WriteLine($"Unknown opcode: {opcodeStr} '{ws[1]}' '{ws[2]}'");
                throw new InvalidOperationException($"Unknown opcode: {opcodeStr}");
            }
            return Parse(_code, ws.ToArray());
        }

        public static long IP = 0,ORG = 0;

        public static byte[] Parse(OPCODE code,ARG dst,ARG src) {


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
                             relIp = LABEL.relIP(drlbl.IP, Reg.imme, 5);
                            var bytes = new List<byte>() { 
                                (byte)(code.codes[1]+1),
                                3,
                                (byte)OpCodeHandler.parse("jmp").codes[3] };
                            bytes.AddRange(NumberSystem.ToByteArray(relIp,2));
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
            if (((Rm.usec & 4) != 0 || Reg.size == 4 || Rm.size == 4) && BITS == 16) {
                inst.Add(0x66);
                x66 = 32;
            }

            if (inst.Count > 0) {
                bits = 32;
                var sb = new StringBuilder("prefixes:");

                foreach (var p in inst) sb.Append($"{p:X} ");

                pref = sb.ToString();
            }
            Debugger.Warn($"\tRead opcode: {code.name} as {opcode:X} from branch {opcodeBranch}");
            inst.AddRange(NumberSystem.ToByteArray((ulong)opcode)); 

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
            Debugger.Log($"\tParsed instruction: {l}");
             
            return [.. inst];
        }
        public static byte[] Parse(OPCODE code, string[] words) {

            if (code.PREF) return ParsePREF(code,words);
            if (code.ZOP) return [(byte)code.codes[0]];
            var dstStr = words[1];
            var srcStr = words[2];
            Console.WriteLine($"\tParsed line: opcode='{words[0]}', dst='{words[1]}', src='{words[2]}'");

            _dst = ParseOperand(dstStr);
            _src = ParseOperand(srcStr);

            if (_dst.isMem && _src.isMem) {
                Console.WriteLine($"Invalid operands: both dst and src cannot be memory references.");
                throw new InvalidOperationException($"Invalid operands: both dst and src cannot be memory references.");
            }
            return Parse(code, _dst, _src);
        }
        public static string[] Split(string line) {
            var sb = new StringBuilder();
            string opcode = "";
            string dst = "";
            string src = "";
            string lbl = "";
            int i = 0;
            int len = line.Length;

            for (; i < len; i++) if (line[i] != ' ') break;
            for (; i < len; i++) {
                if (line[i] == ' ') break;
                if (line[i] == '\t') break;
                if (line[i] == ':') { lbl = sb.ToString(); goto LABEL_FOUND; }
                if (line[i] == ';') goto force_compl;
                sb.Append(line[i]);
            }
            lbl = opcode = sb.ToString();

            // look for dst string 
            sb.Clear();
            for (; i < len; i++) if (line[i] != ' ') break;
            for (; i < len; i++) {
                if (line[i] == ' ') {
                    if (sb.Length != 0 && MemoryHandler.IsImmKeyWord(sb.ToString())) sb.Append(' ');
                    for (; i < len; i++) if (line[i] != ' ') break;// eat all trailing spaces till next word
                    if (i == len) continue;
                }
                if (line[i] == ';') goto force_compl;
                if (line[i] == ',') {
                    i++;
                    break;
                }
                sb.Append(line[i]);
            }
            dst = sb.ToString();

            var dbKey = dst.ToLower();
            if (
                dbKey.StartsWith("db ") ||
                dbKey.StartsWith("dw ") ||
                dbKey.StartsWith("dd ") ||
                dbKey.StartsWith("dq ")) goto LABEL_FOUND;
            // looks for src string
            sb.Clear();
            for (; i < len; i++) if (line[i] != ' ') break;
            for (; i < len; i++) {
                if (line[i] == ' ') {
                    if (sb.Length != 0 && MemoryHandler.IsImmKeyWord(sb.ToString())) sb.Append(' ');
                    for (; i < len; i++) if (line[i] != ' ') break;// eat all trailing spaces till next word
                    if (i == len) continue;
                }
                if (line[i] == ';') goto force_compl;
                sb.Append(line[i]);
            }
            src = sb.ToString();

            // no src string = no 2nd opearand, swap src with dst
            if (src.Length == 0) return new string[] { opcode, src, dst };

            return new string[] { opcode, dst, src };
        force_compl: 

            dbKey = sb.ToString().ToLower();
            if (
                dbKey.StartsWith("db") ||
                dbKey.StartsWith("dw") ||
                dbKey.StartsWith("dd") ||
                dbKey.StartsWith("dq")) goto LABEL_FOUND;

            src = sb.ToString();
            if (src.Length == 0) return new string[] { opcode, src, dst };
            return new string[] { opcode, dst, src };
        LABEL_FOUND:
            i++;
            for (; i < len; i++) if (line[i] != ' ') break;
            return new string[] { "LABEL", lbl, (i < len && line[i] == '{') ? "{" : dst };
        }
        //// Cached
        public static OPCODE _opcode = new();
        public static ARG _dst = new();
        public static ARG _src = new();
        //



        public static DerefferedLabel drlbl = new();
        public string cLabel = "";
        public static byte[] Parse(string line) {
            byte bits = BITS;
            var words = Split(line);
            var opcodeStr = words[0].ToLower();
            if (opcodeStr.Length == 0) return new byte[0];

            drlbl.line = line;
            drlbl.IP = IP;
            drlbl.ORG = ORG;
            byte[] bytes = [];
        start:
            char db = '\0';
            if (opcodeStr == "label" ||
                words[2].StartsWith("db") ||
                words[2].StartsWith("dw") ||
                words[2].StartsWith("dd") ||
                words[2].StartsWith("dq")) {
                var label = words[1];

                long li = ORG + IP;
                Debugger.Info("Saving LABEL {0} with 0x{1}", label, li.ToString("X"));

                LabelHandler.AddLabel(label, li);
                if (words[2] == "{") LabelHandler.PushLabel();
                if (words[2].StartsWith('d')) {
                    db = words[2][1]; goto AddDB;
                }
                return new byte[0];
            } else if (opcodeStr == "}") {
                LabelHandler.PopLabel();
                return new byte[0];

            } else if (opcodeStr == "org") {
                var org = MemoryHandler.ParseIMM(words[2]);
                Debugger.Info("Changing org to {0}", org.imme.ToString("X"));
                ORG = org.imme;
                return new byte[0];
            } else if (opcodeStr == "bits") {
                var org = MemoryHandler.ParseIMM(words[2]);
                Debugger.Info("Changing bits to {0}", org.imme.ToString("X"));
                BITS = (byte)org.imme;
                return new byte[0];
            } else if (opcodeStr == "align") {
                var org = MemoryHandler.ParseIMM(words[2]);
                Debugger.Info("Aligning IP to {0}", org.imme.ToString("X"));
                var align = (byte)org.imme;
                var ip = (IP + align - 1) & ~(align - 1);
                var relIP = ip - IP; 
                bytes = new byte[relIP];
                for (int i = 0; i < relIP; i++) bytes[i] = 0x90;
                if (LabelHandler.parseStage == 1) {
                    drlbl.state |= 1;
                    drlbl.labels.Enqueue($"ALIGN");
                }
                goto done;
            } else if (
                opcodeStr.StartsWith("db") ||
                opcodeStr.StartsWith("dw") ||
                opcodeStr.StartsWith("dd") ||
                opcodeStr.StartsWith("dq")) {
                db = opcodeStr[1];
                goto AddDB;
            } else if (OpCodeHandler.IsRES_KW(opcodeStr)) {
                if (!MemoryHandler.TryParse(words[2], out var _imm)) return [];

                IP += _imm;
                return new byte[_imm];
            }

            if (!OpCodeHandler.TryGet(opcodeStr, out var code)) {
                Console.WriteLine($"Unknown opcode: {opcodeStr} '{words[1]}' '{words[2]}'");
                throw new InvalidOperationException($"Unknown opcode: {opcodeStr}");
            }

            bytes = Parse(_opcode = code, words);
            goto done;
        AddDB:
            bytes = DB_Handler.parseDB(db, line, IP);
        done: 
            drlbl.Size = (byte)bytes.Length;
            IP += drlbl.Size; 
            if ((drlbl.state & 1) > 0) LabelHandler.AddDereferredLabel(drlbl);
            drlbl = new();
            return bytes;
        }

        internal static void Load() => OpCodeHandler.Load(opcodeFile);
    }
}
