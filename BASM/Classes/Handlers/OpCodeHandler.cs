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
        static Dictionary<string, OPCODE> opcodes = new Dictionary<string, OPCODE>();

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
    }
}
