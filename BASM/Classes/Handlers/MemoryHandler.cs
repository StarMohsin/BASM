using BASM.Classes.DS;
using BASM.Classes.Managers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BASM.Classes.Handlers {
    public class MemoryHandler {

        private static readonly HashSet<string> ImmKeyWs = new() { 
            "rel", "abs", "far", "near", "short", "byte", "word", "dword", "qword" ,
            "db","dw","dd","dq"
        };

        public static bool IsImmKeyWord(string s) => ImmKeyWs.Contains(s);
        public static bool StartsWithImmKeyWord(string s,out byte k,out int i) {
            i = k = 0xFF;
            if (string.IsNullOrEmpty(s)) return false;

            k = 0;
            foreach (string keyword in ImmKeyWs) {
                if (s.StartsWith(keyword, StringComparison.Ordinal)) {
                    i = keyword.Length;
                    if (s[i] == ' ') i++;
                    return true;
                }
                k++;
            } 
            return false;
        }

        public static bool TryParseIMM(string src, out IMM imm,byte keyw = 0) {
            imm = new DS.IMM();
            imm.mod = 0xFF;
            imm.keyw = keyw;

            if (src.Length == 0) return false; // min str "\"\"" 
            if (src.StartsWith('-')) {
                TryParseIMM(src.Substring(1), out imm, keyw);
                imm.imme = -imm.imme;
            }
            var parts = src.Split(':');

            if(parts.Length > 1) {
                if (parts.Length > 2) return false;

                if (!TryParseIMM(parts[0],out var seg)) return false;
                if (!TryParseIMM(parts[1],out var off)) return false;

                imm.imme = off.imme;
                imm.imme1 = seg.imme;
                imm.usec = 1;
                imm.size = (byte)(off.size + seg.size);
                return true;
            }
            char fc = src[0];
            char lc = src[src.Length - 1];

            if (src.StartsWith("0x")) {  
                if((src.Length - 2) == 0) return false;
                  
                imm.imme = Convert.ToInt64(src, 16);
            } else if (
                lc >= '0' &&
                lc <= '9' &&
                long.TryParse(src, out var imme)) imm.imme = imme;
            else {
                if (src.Length < 2) return false; // min str "\"\"" 
                bool m = false;
                char lc1 = src[src.Length - 2];

                if ((lc1 >= '0' && lc1 <= '9') ||
                    (lc1 >= 'A' && lc1 <= 'H')) {
                    switch (lc) {
                        case 'h': imm.imme = Convert.ToInt64(src.Substring(0, src.Length - 1), 16);     m = true;   break;
                        case 'd': imm.imme = Convert.ToInt64(src.Substring(0, src.Length - 1), 10);     m = true;   break;
                        case 'o': imm.imme = Convert.ToInt64(src.Substring(0, src.Length - 1),  8);     m = true;   break;
                        case 'b': imm.imme = Convert.ToInt64(src.Substring(0, src.Length - 1),  2);     m = true;   break;
                    }
                }
                if (!m) {

                    if (src.Length >= 3 && fc == '\'' && lc == '\'') {
                        imm.size = 1;
                        imm.imme = src[1];
                        return true;
                    }
                    if (src.Length >= 2 && fc == '\"' && lc == '\"') {
                        imm.size = 1;
                        imm.imme = src[1];

                        Debugger.Error("String constant captured");
                        throw new Exception("Not implemented");
                    }
                    return false;
                } 
            } 
             
            imm.size = getSize(imm.imme);
            return true;
        }
        public static IMM ParseIMM(string src) {
            TryParseIMM(src, out var imm);
            return imm;
        }

        public static bool TryParse(string src,out long n) {
            if (TryParseIMM(src, out var imm)) {
                n = imm.imme;
                return true;
            }
            n = 0;
            return false;
        }
        public static byte getSize(ulong _size) { 
            if (_size <= 0xFF) _size = 1;
            else if (_size <= 0xFFFF) _size = 2;
            else if (_size <= 0xFFFFFFFF) _size = 4;
            else if (_size <= 0xFFFFFFFFFFFFFFFF) _size = 8;
            else _size = 8;
            return (byte)_size;
        }
        public static byte getSize(long _size) => getSize((ulong)Math.Abs(_size));
        private static byte getRM(int reg) {
            return reg switch {
                6 => 0, // si
                7 => 1, // di
                5 => 2, // bp
                3 => 3, // bx
                _ => 0xFF,
            };
        } 
        public SIB _Parse(string mem, byte keyw = 0) {
            var rm = new SIB();
            rm.keyw = keyw;
            if (!mem.StartsWith('[') || !mem.EndsWith(']')) return rm;
            rm.mod = 0; 
            int len = mem.Length;

            int ptrCount = 0;
            int processWord(string _word) {
                if (_word.Length == 0) return -1;

                if (_word.EndsWith(':')) {
                    _word = _word.TrimEnd(':');
                    if (RegistersHandler.TryParse(_word, out var seg)) {
                        rm.seg = seg.value;
                        rm.usec |= 1;
                    }
                    return 1;
                }

                if (RegistersHandler.TryParse(_word, out var reg)) {
                    if (reg.size > 2) {
                        if (ptrCount > 0) {
                            if (reg.value == 4 && rm.sib != 0x24) { // sp
                                rm.sib = (byte)((rm.value << 3)|4);
                                rm.value = 4;
                            } else {
                                rm.sib = (byte)((reg.value << 3)|rm.value);
                            }
                            return 1;
                        }
                        rm.sib = (byte)(0x20|reg.value);
                        if (reg.value == 4) rm.value = 4; 
                        else rm.value = reg.value;
                        rm.usec |= 2;
                        ptrCount++;
                        return 1;
                    }
                    if (rm.value == 0xFF) rm.value = 0; 
                    if (rm.value > 3) {
                        var _nrm = getRM(reg.value);
                        var _orm = 4 - rm.value;

                        byte _base = 0xFF;
                        byte _indx = 0xFF;
                        if (_orm > 1) { _base = (byte)(_orm & 0xFE); _indx = (byte)(_nrm & 1); }
                        if (_nrm > 1) { _base = (byte)(_nrm & 0xFE); _indx = (byte)(_orm & 1); }
                        if (_base == 0xFF) {
                            Console.WriteLine($"[ERROR] Invalid memory operand: {mem} {_orm} {_nrm}");
                            throw new Exception($"Invalid memory operand: {mem}");
                        }
                        if (_base == 3) _base = 0;

                        rm.value = (byte)(_base | _indx);

                    } else {
                        rm.value += (byte)(4 + getRM(reg.value));
                    }
                    return 1;
                } else if (TryParseIMM(_word, out var imm)) {
                    switch (imm.size) {
                        case 1: rm.size = 1; rm.mod = 1; break;
                        case 2: rm.size = 2; rm.mod = 2; break;
                        case 4: rm.size = 3; rm.mod = 2; break;
                        case 8: rm.size = 4; rm.mod = 2; break;
                    }
                    rm.imme = imm.imme;
                    return 2;
                } else {
                    var lbl = LabelHandler.Parse(_word);
                    if (lbl.isSolved) rm.imme = lbl.imme;
                    rm.unsolved = lbl.unsolved;
                    rm.label = lbl.label;
                }
                return 0;
            }
            rm.value = 0xFF; // default to disp only
            var word = new StringBuilder();
            bool mul = false;
            for (int i = 1; i < len; i++) {
                char c = mem[i];
                if (c == ' ' ||
                    c == '+' ||
                    c == '-' ||
                    c == ']' ||
                    c == '*') {
                    if (word.Length == 0) continue;

                    var r = processWord(word.ToString());

                    if(mul)
                    if (c == '*' && r == 1) mul = true;

                    word.Clear();
                    continue;
                } 
                word.Append(c);
                if (c == ':') {
                    if (word.Length == 0) continue;
                    processWord(word.ToString());

                    word.Clear();
                    continue;
                }
            }
            if ((rm.usec & 2) == 0) {
                if (rm.value == 0xFF) {
                    rm.value = 6;
                    rm.mod = 0;
                } else if (rm.value == 6) {
                    rm.mod = 1;
                    rm.imme = 0;
                    rm.size = 1;
                }
            }
            return rm;
        } 
        public static SIB Parse(string mem, byte keyw = 0) {
            var rm = new SIB();
            rm.keyw = keyw;
            if (!mem.StartsWith('[') || !mem.EndsWith(']')) return rm;
            rm.mod = 0;
            int len = mem.Length; 
            
            mem = mem.Substring(1, len - 2);
            len = mem.Length;
            rm.value = 0xFF; // default to disp only

            int ptrCount = 0;
            int processWord(string _word) {
                if (_word.Length == 0) return -1;

                var parts = _word.Split(':');
                if (parts.Length>1) {
                    if (RegistersHandler.TryParse(parts[0], out var seg)) {
                        rm.seg = seg.value;
                        rm.usec |= 8;
                    }
                    processWord(parts[1]);
                    return 1;
                }

                if (RegistersHandler.TryParse(_word, out var reg)) {
                    if (reg.size > 2) {
                        if (ptrCount > 0) {
                            if (reg.value == 4 && rm.sib != 0x24) { // sp
                                rm.sib = (byte)((rm.value << 3) | 4);
                            } else {
                                rm.sib = (byte)((reg.value << 3) | rm.value);
                            }
                            rm.value = 4;
                            return 1;
                        }
                        rm.sib = (byte)(0x20 | reg.value);
                        if (reg.value == 4) rm.value = 4;
                        else rm.value = reg.value;
                        rm.usec |= 2;
                        ptrCount++;
                        return 1;
                    }
                    if (rm.value == 0xFF) rm.value = 0;
                    if (rm.value > 3) {
                        var _nrm = getRM(reg.value);
                        var _orm = 3 & rm.value;

                        byte _base = 0xFF;
                        byte _indx = 0xFF;
                        if (_orm > 1) { _base = (byte)((_orm & 2)^2); _indx = (byte)(_nrm & 1); }
                        if (_nrm > 1) { _base = (byte)((_nrm & 2)^2); _indx = (byte)(_orm & 1); }
                        if (_base == 0xFF) {
                            Console.WriteLine($"[ERROR] Invalid memory operand: {mem} {_orm} {_nrm}");
                            throw new Exception($"Invalid memory operand: {mem}");
                        }
                        if (_base == 3) _base = 0;

                        rm.value = (byte)(_base | _indx);

                    } else {
                        rm.value += (byte)(4 + getRM(reg.value));
                    }
                    return 1;
                } else if (TryParseIMM(_word, out var imm)) {
                    switch (imm.size) {
                        case 1: rm.size = 1; rm.mod = 1; break;
                        case 2: rm.size = 2; rm.mod = 2; break;
                        case 4: rm.size = 3; rm.mod = 2; break;
                        case 8: rm.size = 4; rm.mod = 2; break;
                    }
                    rm.imme += imm.imme;
                    return 2;
                } else {
                    var lbl = LabelHandler.Parse(_word);
                    if (lbl.isSolved) rm.imme += lbl.imme;
                    rm.unsolved = lbl.unsolved;
                    rm.label = rm.imme+"+"+lbl.label;
                }
                return 0;
            }
            EqParser.Parse(mem, (string lbl, out long v) => {
                v = 0;
                if (MemoryHandler.TryParse(lbl, out v)) return true;
                if (RegistersHandler.TryParse(lbl, out var reg)) return false;
                if (LabelHandler.TryGetValue(lbl, out LABEL imm)) {
                    v = imm.imme;
                    return true;
                }
                return false;
            }).FoldConstants().Enum(_ => {
                processWord(_);
            });
            Console.WriteLine("MEM " + rm.imme);

            if ((rm.usec & 2) == 0) {
                if (rm.value == 0xFF) {
                    rm.value = 6;
                    rm.mod = 0;
                } else if (rm.value == 6) {
                    rm.mod = 1;
                    rm.imme = 0;
                    rm.size = 1;
                }
            }
            if(keyw != 0) {
                if(keyw == 5) rm.size = 1;
                else if(keyw == 6) rm.size = 2;
                else if(keyw == 7) rm.size = 4;
                else if(keyw == 8) rm.size = 8;
            }
            return rm;
        }
    }
}
