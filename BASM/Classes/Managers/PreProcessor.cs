using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BASM.Classes.Managers {
    internal class PreProcessor {

        public static bool IsIncludeDir(string dir) => dir.StartsWith("#include", StringComparison.OrdinalIgnoreCase);

        public struct IncludeFile {
            public string file;
            public bool IsCustom;
        }
        public static IncludeFile getIncludeFile(string line) {
            int i = 8;// "#include".Length;
            int len = line.Length;
            if (i >= len) throw new Exception("Invalid include directive: " + line);

            char bchar = '\0';
            for (; i < len; i++) {
                if(char.IsWhiteSpace(line[i])) continue;
                if (line[i] == '\"' || line[i] == '<') {
                    bchar = line[i];
                    break;
                }
            }
            i++;
            if (i >= len) throw new Exception("Invalid include directive: " + line);
            var sb = new StringBuilder();
            for (; i < len; i++) {
                if (line[i] == '\"' || line[i] == '>') {
                    bchar = line[i];
                    break;
                }
                sb.Append(line[i]);
            }
            if (i >= len) throw new Exception("Invalid include directive: " + line);

            return new() { file = sb.ToString(), IsCustom = (bchar == '\"') };
        }
    }
}
