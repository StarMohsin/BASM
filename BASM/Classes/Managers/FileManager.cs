using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace BASM.Classes.Managers {
    public class FileManager {
          
        public static string ReadLine(FileStream fs, out bool eof) {
            if (fs == null) throw new ArgumentNullException("fs", "File stream cannot be null.");
            var sb = new StringBuilder();

            eof = false;
            while (true) {
                int b = fs.ReadByte();
                if (eof = b == -1) return sb.ToString(); // EOF
                char c = (char)b;
                if (c == '\t') c = ' ';
                if (c == '\r') continue; // Skip carriage return
                if (c == '\n') break; // End of line
                sb.Append(c);
            }
            return sb.ToString();
        } 
        public static void Write(FileStream fs,byte[] bytes) {
            if (fs == null) throw new ArgumentNullException("fs", "File stream cannot be null.");
            fs.Write(bytes, 0, bytes.Length);
        }
    }
}
