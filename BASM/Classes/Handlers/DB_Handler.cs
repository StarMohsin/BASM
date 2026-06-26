using BASM.Classes.DS;
using BASM.Classes.Managers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace BASM.Classes.Handlers {
    public class DB_Handler { 
        public static byte[] parseDB(char db, string line,long IP) {
            var v = 0;
            switch (db) {
                case 'b': v += 1; break;
                case 'w': v += 2; break;
                case 'd': v += 4; break;
                case 'q': v += 8; break;
            }

            IP += v;
            char[] cs = ['\0', '\0', '\0'];
            int len = line.Length;
            string src = line;
            int i = 0;
            for (; i < len; i++) {
                cs[0] = line[i];
                if (cs[0] == ' ' && cs[1] == db && cs[2] == 'd') goto db_str;
                cs[2] = cs[1];
                cs[1] = cs[0];
            }
            return new byte[v];
        db_str:
            List<string> args = new();


            List<byte> ToBytes(long n) {
                List<byte> bytes = new();
                for (int j = v; j > 0; j--) {
                    bytes.Add((byte)(n & 0xFF));
                    n >>= 8;
                }
                return bytes;
            }

            List<byte> parseArg() {
                var sb = new StringBuilder();
                for (; i < len; i++) {
                    if (line[i] == ';') { i = len; goto done; }
                    if (line[i] != ' ') break;
                }
                bool str = false;
                for (; i < len; i++) {
                    if (line[i] == ';') { i = len; goto done; }
                    if (line[i] == ',') break;
                    if (line[i] == '\"') str ^= true;
                    if (line[i] == ' ' && !str) break;
                    sb.Append(line[i]);
                }
                i++;
            done:
                var arg = sb.ToString();

                List<byte> bytes = new List<byte>();
                if (arg.Length == 0) return bytes;
                if (arg.StartsWith('\"') && arg.EndsWith('\"')) {
                    int len = arg.Length-1;
                    for(int i=1;i<len;i++) {
                        bytes.AddRange(ToBytes(arg[i]));
                    }
                } else if (MemoryHandler.TryParseIMM(arg, out var imm)) {
                    bytes.AddRange(ToBytes(imm.imme));
                } else if(LabelHandler.TryGetValue(arg,out LABEL lbl)){
                    bytes.AddRange(ToBytes(lbl.imme));
                } else {
                    Console.WriteLine(EqParser.Parse(arg) +"");// bytes.AddRange(ToBytes());
                    Console.WriteLine(ParseEq(arg)+"");// bytes.AddRange(ToBytes());
                }
                return bytes;
            }

            List<byte> bytes = new List<byte>();
            while (i < len) {
                bytes.AddRange(parseArg());
            }
            return bytes.ToArray();
        }
        static DerefferedLabel drlbl => OpCodeManager.drlbl;
        public static Expression ParseEq(string eq) =>
            EqParser.Parse(eq, (string lbl, out long v) => {
                v = 0;
                if (MemoryHandler.TryParse(lbl, out v)) return true;
                if (RegistersHandler.TryParse(lbl, out var reg)) return false;
                if (LabelHandler.TryGetValue(lbl, out LABEL  imm)) {
                    v = imm.imme;
                    return true;
                }
                drlbl.state |= 1;
                return false;
            }).FoldConstants();
    }
}
