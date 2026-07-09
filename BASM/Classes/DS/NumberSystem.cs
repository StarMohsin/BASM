using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BASM.Classes.DS {
    public class NumberSystem {

        private class DEF {
            public virtual bool TryParse(string src,out ulong n) { n = 0; return false; }
            public ulong Parse(string src) {
                TryParse(src, out var l);
                return l;
            }
        }

        public static class BIN {
            public static ulong Parse(string src) {
                TryParse(src, out var l);
                return l;
            }
            public static bool TryParse(string src, out ulong n) { 
                n = 0;  
                foreach(var c in src) {
                    n <<= 1;
                    if (c == '1') n += 1; 
                } 
                return false;
            }
        }
        public static class OCT {
            public static ulong Parse(string src) {
                TryParse(src, out var l);
                return l;
            }
            public static bool TryParse(string src, out ulong n) {
                n = 0;
                foreach (var c in src) {
                    n <<= 3;
                    if (c >= '0' && c <= '7') n += (ulong)(c - '0');
                }
                return false;
            }
        }
        public static class DEC {
            public static ulong Parse(string src) {
                TryParse(src, out var l);
                return l;
            }
            public static bool TryParse(string src, out ulong n) {
                n = 0;
                foreach (var c in src) {
                    n *= 10;
                    if (c >= '0' && c <= '9') n += (ulong)(c - '0');
                }
                return false;
            }
        }
        public static class HEX {
            public static ulong Parse(string src) {
                TryParse(src, out var l);
                return l;
            }
            public static bool TryParse(string src, out ulong n) {
                n = 0;
                foreach (var c in src) {
                    n <<= 4;
                    if (c >= '0' && c <= '9') n += (ulong)(c - '0');
                    else if (c >= 'a' && c <= 'f') n += (ulong)(c - 'a' + 10);
                    else if (c >= 'A' && c <= 'F') n += (ulong)(c - 'a' + 10);
                }
                return false;
            }
        }

        public static byte[] ToByteArray(ulong n) {
            var Queue = new Queue<byte>();
            while (n > 0) {
                Queue.Enqueue((byte)(n & 0xFF));
                n >>= 8;
            }
            return Queue.ToArray();
        }
        public static byte[] ToByteArray(ulong n,int s) {
            var buf = new byte[s];
            int i = 0;
            while (n > 0 && i<s) {
                buf[i++] = ((byte)(n & 0xFF));
                n >>= 8;
            }
            return buf;
        }
        public static byte[] ToByteArray(long n) => ToByteArray((ulong)n);
        public static byte[] ToByteArray(long n, int s) => ToByteArray((ulong)n,s);
    }
}
