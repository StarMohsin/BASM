using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using BASM.Classes.DS;

namespace BASM.Classes.Handlers {
    public class RegistersHandler {

        private static byte getG(char a) {
            return a switch {
                'a' => 0,
                'c' => 1,
                'd' => 2,
                'b' => 3,
                _ => 0xFF,
            };
        }
        private static byte getP(char a) {
            return a switch {
                's' => 0,
                'b' => 1,
                _ => 0xFF,
            };
        }
        private static byte getI(char a) {
            return a switch { 
                's' => 2,
                'd' => 3,
                _ => 0xFF,
            };
        }
        public static bool TryParse(string Reg,out REG r,byte keyw = 0) {
            int len = Reg.Length;
            r = new REG();
            r.keyw = keyw;
            if (len < 2) return false;
            if (len > 3) return false;
            var reg = Reg.ToLower();

            char c = reg[len - 1];
            char a = reg[len - 2];

            r.size = 1;
            if (c == 's') {
                r.size = 2;
                r.usec = 1;
                switch (a) {
                    case 'e': r.value = 0; break;
                    case 'c': r.value = 1; break;
                    case 's': r.value = 2; break;
                    case 'd': r.value = 3; break;
                    case 'f': r.value = 4; break;
                    case 'g': r.value = 5; break;
                    default: return false;
                }

                goto done;
            } 
            switch (c) {
                case 'l': r.size = 1; r.value += getG(a); break;
                case 'h': r.size = 1; r.value += (byte)(4 + getG(a)); break;
                case 'x': r.size = 2; r.value += getG(a); break;  
                case 'p': r.size = 2; r.value += (byte)(4 + getP(a)); break;
                case 'i': r.size = 2; r.value += (byte)(4 + getI(a)); break;
                default: return false;
            }
            if (len < 3) goto done;

            char e = reg[len - 3];
            switch (e) {
                case 'e': r.size = 4; break;
                case 'r': r.size = 8; break; 
            }
        done:
            r.mod = 3;
            return true;
        }
        public static REG Parse(string reg) {
            TryParse(reg, out var r);
            return r;
        }
    }
}
