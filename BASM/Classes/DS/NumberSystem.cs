using System;
using System.Collections.Generic;
using System.Drawing;
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

        public static byte getSize(ulong size) {
            if (size <= 0xFF) return 1;
            if (size <= 0xFFFF) return 2;
            if (size <= 0xFFFFFFFF) return 4;
            return 8;
        }
        public static byte[] ToByteArray_LE(ulong n,byte s = 0) {
            if(s > 8) throw new ArgumentOutOfRangeException("s", "Size must be less than 9 bytes.");
            if (s == 0) s = getSize(n);
            var buf = new byte[s];
            int i = 0;
            while (n > 0 && i<s) {
                buf[i++] = ((byte)(n & 0xFF));
                n >>= 8;
            }
            return buf;
        }
        public static byte[] ToByteArray_BE(ulong n, byte s = 0) {
            if (s > 8) throw new ArgumentOutOfRangeException("s", "Size must be less than 9 bytes.");
            if (s == 0) s = getSize(n);
            var buf = new byte[s];
            int i = s-1;
            while (n > 0 && i > -1) {
                buf[i--] = ((byte)(n & 0xFF));
                n >>= 8;
            }
            return buf;
        }
        public static byte[] ToByteArray_LE(long n) => ToByteArray_LE((ulong)n);
        public static byte[] ToByteArray_LE(long n, byte s) => ToByteArray_LE((ulong)n,s);
        public static byte[] ToByteArray_BE(long n) => ToByteArray_BE((ulong)n);
        public static byte[] ToByteArray_BE(long n, byte s) => ToByteArray_BE((ulong)n, s);
    }
}
