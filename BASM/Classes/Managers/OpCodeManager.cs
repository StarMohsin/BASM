using BASM.Classes.DS;
using BASM.Classes.Handlers;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection.Emit;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks; 

namespace BASM.Classes.Managers {
    public class OpCodeManager { 
        public static byte BITS = 16;
        public static long IP = 0, ORG = 0;
        const bool DEBUG = false;

        static string opcodeFile = "opcodes.asm"; 

        public static byte NASM = 0x1; // flags to follow nasm syntax
        internal static void Load() => OpCodeHandler.Load(opcodeFile);

        // leaving it here, As it has some core logic for the assembler,
        // but it's not used in the current implementation. 
        // this function was perfect for parsing, but lacked multiple operands, so I had to rewrite it.
        public static string[] __Split(string line) {
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
            // if (src.Length == 0) return new string[] { opcode, src, dst };

            return new string[] { opcode, dst, src };
        force_compl: 

            dbKey = sb.ToString().ToLower();
            if (
                dbKey.StartsWith("db") ||
                dbKey.StartsWith("dw") ||
                dbKey.StartsWith("dd") ||
                dbKey.StartsWith("dq")) goto LABEL_FOUND;

            src = sb.ToString();
            // if (src.Length == 0) return new string[] { opcode, src, dst };
            return new string[] { opcode, dst, src };
        LABEL_FOUND:
            i++;
            for (; i < len; i++) if (line[i] != ' ') break;
            return new string[] { "LABEL", lbl, (i < len && line[i] == '{') ? "{" : dst };
        }
        public static string[] Split(string line) {
            var sb = new StringBuilder();
            int i = 0;
            int len = line.Length;

            for (; i < len; i++) if (line[i] != ' ') break; // skip leading spaces
            for (; i < len; i++) {
                // break on space, tab or semicolon(for comments)
                if (line[i] == ' ') break;
                if (line[i] == '\t') break; 
                if (line[i] == ';') {
                    len = i;
                    break;
                }
                sb.Append(line[i]);
            }
            for (; i < len; i++) if (line[i] != ' ') break; // skip spaces after opcode
            if (sb.Length == 0) return [];

            // if opcode ends with a colon, it's a label, return it as such
            if (sb[sb.Length-1] == ':') { 
                sb = sb.Remove(sb.Length-1, 1);
                return new string[] { "LABEL", sb.ToString(), (i < len && line[i] == '{') ? "{" : "" };
            } else {
                char[] dbKey = [' ', ' ', ' '];

                for (int j = i, k =0; j < len && k < 3; j++,k++) dbKey[k] = line[j];

                // if the next word is a db/dw/dd/dq keyword, treat it as a label
                if (dbKey[2] == ' ' &&
                    dbKey[0] == 'd') {
                    switch (dbKey[1]) {
                        case 'b':
                        case 'w':
                        case 'd':
                        case 'q':
                            return new string[] { "LABEL", sb.ToString(), dbKey[0] + "" + dbKey[1], line.Substring(i, len - i) };
                    }
                }
            }
            var opcode = sb.ToString();
            List<string> args = [opcode];
            // look for dst string 
            sb.Clear();
            for (; i < len; i++) if (line[i] != ' ') break; // skip spaces after opcode
            for (; i < len; i++) {
                if (line[i] == ' ') {
                    if (sb.Length != 0 && MemoryHandler.IsImmKeyWord(sb.ToString())) sb.Append(' ');
                    for (; i < len; i++) if (line[i] != ' ') break;// eat all trailing spaces till next word
                    if (i == len) continue;
                }
                if (line[i] == ';') { // comment, break the loop and return the arguments
                    len = i;
                    break;
                }
                if (line[i] == ',') { // argument separator, add the current argument to the list and clear the string builder for the next argument
                    i++;
                    args.Add(sb.ToString());
                    sb.Clear();
                    continue;
                }
                sb.Append(line[i]);
            }
            args.Add(sb.ToString());


            // args should always have 3 elements, if not, add empty strings to the list
            // opcode, dst, src
            for (int j= args.Count; j<3;j++) args.Add("");
            return args.ToArray(); 
        }

        //// Cached
        public static OPCODE _opcode = new();
        public static DerefferedLabel drlbl = new();
        public string cLabel = "";


        // leaving it here, As it has some core logic for the assembler,
        // but it's not used in the current implementation.
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
            if (opcodeStr == "label") {
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
                var align = (byte)org.imme;
                var ip = (IP + align - 1) & ~(align - 1);
                var relIP = ip - IP; 
                bytes = new byte[relIP];
                for (int i = 0; i < relIP; i++) bytes[i] = 0x90;
                if (LabelHandler.parseStage == 1) {
                    drlbl.state |= 1;
                    drlbl.labels.Enqueue($"ALIGN");
                }
                Debugger.Info("Aligning IP to {0} with {1}", org.imme.ToString("X"),relIP.ToString("X"));
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

            bytes = OpCodeHandler.Parse(_opcode = code, words);
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

        public class Token {
            public enum TOKENS {
                Empty,
                Inst,
                RES, // Reserved, RESB/RESW/RESD/RESQ Keyword
                DB, // Data, DB/DW/DD/DQ keyword
                CB, // CodeBlock terminator '}', used to close code blocks opened by '{' in ALIGN or LABEL
                ALIGN,
                LABEL
            }; 
            public TOKENS Type = 0;
            public long Size = 0;
            public byte[] Bytes = [];
            public long[] Regs = [];
            public long[] Data = [];
            public string Str = "";
            public DerefferedLabel drlbl = null;

            public bool Empty => Type == TOKENS.Empty;

            public Token Clone() => new() { 
                Type = Type,
                Size = Size,
                Bytes = Bytes, 
                Regs = Regs,
                Data = Data,
                Str = Str,
                drlbl = drlbl
            };
        }
        public class CodeBlock {
            public Action<CodeBlock,int, DerefferedLabel> onClose = null;
            public Token Owner = null;
            public long[] Regs = [];
            public void Close(int ti, DerefferedLabel drlbl) => onClose?.Invoke(this,ti, drlbl);
        }

        static Stack<CodeBlock> codeBlocks = new();
        public static Token ParseAsToken( 
            int TI,
            string line
        ) {
            byte bits = BITS;
            var words = Split(line);

            if (words.Length == 0) return new();

            var opcodeStr = words[0].ToLower();
            if (opcodeStr.Length == 0) return new();

            drlbl.line = line;
            drlbl.IP = IP;
            drlbl.ORG = ORG;
            Token T = new();
        start:
            char db = '\0';
            if (opcodeStr == "}") {
                if (codeBlocks.Count == 0) return new();
                var _drlbl = drlbl;
                codeBlocks.Pop().Close(TI, _drlbl);
                drlbl = new();
                return new() { Type = Token.TOKENS.CB, drlbl = _drlbl }; 
            } else if (opcodeStr == "org") {
                var org = MemoryHandler.ParseIMM(words[1]);
                Debugger.Info("Changing org to {0}", org.imme.ToString("X"));
                ORG = org.imme;
                return new();
            } else if (opcodeStr == "bits") {
                var org = MemoryHandler.ParseIMM(words[1]);
                Debugger.Info("Changing bits to {0}", org.imme.ToString("X"));
                BITS = (byte)org.imme;
                return new();
            } else if (opcodeStr == "align") {
                var org = MemoryHandler.ParseIMM(words[1]); 
                var align = org.imme;
                if (align == 0) throw new InvalidOperationException($"Can't align to \'{align}\'");


                var ip = (IP + align - 1) & ~(align - 1);
                var relIP = ip - IP;

                var pbLen = words.Length - 3;

                T.Bytes = new byte[pbLen];
                if (words.Length > 3 && words[3] == "{") {
                    codeBlocks.Push(new() {
                        onClose = (cb, ti, drlbl) => {
                            cb.Owner.Data = [1, ti];
                        },
                        Owner = T,
                        Regs = [IP]
                    });
                    pbLen = 0;
                } else {
                    T.Data = [0, 0];
                    for (int i = 0; i < pbLen; i++) {
                        if (MemoryHandler.TryParse(words[i + 3], out var _imm2)) T.Bytes[i] = (byte)_imm2;
                    }
                }

                if (relIP == 0 && pbLen > 0) relIP += align;
                IP += relIP;
                
                T.Size = relIP;
                T.Type = Token.TOKENS.ALIGN;

                byte b = 0x90;

                if(MemoryHandler.TryParse(words[2], out var _imm)) b = (byte)_imm;


                //for (int i = 0; i < relIP; i++) T.Bytes[i] = 0x90; 
                if (LabelHandler.parseStage == 1) {
                    drlbl.state |= 1;
                    drlbl.labels.Enqueue($"ALIGN");
                }
                T.Regs = [align, b];

                T.drlbl = drlbl;
                Debugger.Info("Aligning IP to {0} with {1}", org.imme.ToString("X"), relIP.ToString("X"));
                goto done;
            } else if (opcodeStr == "label") {
                var label = words[1];

                long li = ORG + IP;
                Debugger.Info("Saving LABEL {0} with 0x{1}", label, li.ToString("X"));

                LabelHandler.AddLabel(label, li, TI);
                if (words[2] == "{") {
                    LabelHandler.PushLabel();
                    codeBlocks.Push(new() { onClose = (_,_,_) => LabelHandler.PopLabel(), Owner = T });
                }
                if (words[2].StartsWith('d')) {
                    T = ParseAsToken(TI, words[3]);
                    T.Str = label;
                    T.Regs = [li];
                    return T;
                }
                return new() { Type = Token.TOKENS.LABEL, Regs = [li], Str = label };
            } else if (
                opcodeStr.StartsWith("db") ||
                opcodeStr.StartsWith("dw") ||
                opcodeStr.StartsWith("dd") ||
                opcodeStr.StartsWith("dq")) {
                db = opcodeStr[1];
                T.Regs = [IP];
                goto AddDB;
            } else if (OpCodeHandler.IsRES_KW(opcodeStr)) {
                if (!MemoryHandler.TryParse(words[2], out var _imm)) return new();

                IP += _imm;
                T.Type = Token.TOKENS.RES;
                T.Size = _imm;
                return T;
            }

            if (!OpCodeHandler.TryGet(opcodeStr, out var code)) {
                Console.WriteLine($"Unknown opcode: {opcodeStr} '{words[1]}' '{words[2]}'");
                throw new InvalidOperationException($"Unknown opcode: {opcodeStr}");
            }

            if (words[2].Length == 0)
                (words[1], words[2]) = (words[2], words[1]); // swap dst and src for token parsing
            T.Bytes = OpCodeHandler.Parse(_opcode = code, words);
            T.Type = Token.TOKENS.Inst;
            T.Size = drlbl.Size = (byte)T.Bytes.Length;
            goto done;
        AddDB:
            T.Bytes = DB_Handler.parseDB(db, line, IP);
            T.Type = Token.TOKENS.DB;
            T.Size = drlbl.Size = (byte)T.Bytes.Length;
        done:
            IP += drlbl.Size;
            if ((drlbl.state & 1) > 0) LabelHandler.AddDereferredLabel(T.drlbl = drlbl);
            drlbl = new();
            return T;
        }

    }
}
