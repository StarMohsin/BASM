using BASM.Classes.Handlers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BASM.Classes.Managers {
    public class Assembler { 
        public static void Assemble(string sourcePath, string outputPath) {
            FileManager.Open(sourcePath, outputPath);
            OpCodeManager.Load();

            bool eof = false;
            int lineI = 0;
            while (!eof) {
                string line = FileManager.ReadLine(out eof);
                Console.WriteLine($"[{lineI++}] {line}");

                if (line.Length == 0) continue; // Skip empty lines
                var ip = OpCodeManager.IP;
                var opcode = OpCodeManager.Parse(line);

                if (opcode.Length == 0) continue;

                var sb = new StringBuilder();
                foreach (var part in opcode) sb.Append($"{part:X2} ");
                Debugger.info("[{1}] Parsed opcode: '{0}'",sb.ToString(),ip.ToString("X")); // Debug output

                FileManager.Write(opcode);
            }
            LabelHandler.ParseAllDereferredLabels(FileManager.dst);
            FileManager.Close();
        }
    }
}
