using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace BASM.Classes.Managers {
    public class FileManager {

        static FileInfo sfile,ofile;
        public static FileStream src, dst;

        public static void Open(string source,string dest) {
            sfile = new FileInfo(source);
            ofile = new FileInfo(dest);

            src = sfile.OpenRead();
            if (!ofile.Exists) ofile.Create();
            dst = ofile.Open(FileMode.Truncate,FileAccess.Write);
        }
        public static void Close() {
            if (src != null) src.Close();
            if (dst != null) {
                dst.Flush();
                dst.Close();
            }
        }
        public static bool EOF() {
            if (sfile == null) throw new InvalidOperationException("Source file not opened.");
            return src.ReadByte() == -1;
        }
        public static string ReadLine(out bool eof) {
            if (sfile == null) throw new InvalidOperationException("Source file not opened.");
            var sb = new StringBuilder();

            eof = false; 
            while (true) {
                int b = src.ReadByte();
                if (eof = b == -1) return sb.ToString(); // EOF
                char c = (char)b;
                if (c == '\t') c = ' ';
                if (c == '\r') continue; // Skip carriage return
                if (c == '\n') break; // End of line
                sb.Append(c);
            }
            return sb.ToString();
        }
        public static void Write(byte[] bytes) {
            if (ofile == null) throw new InvalidOperationException("Output file not opened."); 
            dst.Write(bytes, 0, bytes.Length);
        }
    }
}
