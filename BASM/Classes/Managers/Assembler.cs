using BASM.Classes.DS;
using BASM.Classes.Handlers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static BASM.Classes.Managers.OpCodeManager.Token;

namespace BASM.Classes.Managers {
    public class Assembler {

        // leaving it here, As it has some core logic for the assembler,
        // but it's not used in the current implementation.
        // current implementation uses the Pass1st and Assemble methods instead.
        // with Token parsing and label handling separated into InstructionManager and LabelHandler classes.

        //public static void _Assemble(string sourcePath, string outputPath) {
        //    FileManager.Open(sourcePath, outputPath);
        //    OpCodeManager.Load();

        //    bool eof = false;
        //    int lineI = 0;
        //    while (!eof) {
        //        string line = FileManager.ReadLine(out eof);
        //        Console.WriteLine($"[{lineI++}] {line}");

        //        if (line.Length == 0) continue; // Skip empty lines
        //        var ip = OpCodeManager.IP;
        //        var opcode = OpCodeManager.Parse(line);

        //        if (opcode.Length == 0) continue;

        //        var sb = new StringBuilder();
        //        foreach (var part in opcode) sb.Append($"{part:X2} ");
        //        Debugger.info("[{1}] Parsed opcode: '{0}'",sb.ToString(),ip.ToString("X")); // Debug output

        //        FileManager.Write(opcode);
        //    }
        //    LabelHandler.ParseAllDereferredLabels(FileManager.dst);
        //    FileManager.Close();
        //}


        public static void Pass1st(string sourcePath) {
            var fs = File.OpenRead(sourcePath);

            bool eof = false;
            int lineI = 0;
            while (!eof) {
                string line = FileManager.ReadLine(fs,out eof).TrimStart();
                Console.WriteLine($"[{lineI++}] {line}");

                if (line.Length == 0) continue; // Skip empty lines
                if (PreProcessor.IsIncludeDir(line)) {
                    var iFile = PreProcessor.getIncludeFile(line);

                    // Suspend current file processing, drop down into the include file
                    var includePath = iFile.file;
                    if (iFile.IsCustom) {
                        var dir = Path.GetDirectoryName(sourcePath) ?? ""; 
                        includePath = Path.Combine(dir , includePath);
                    }
                    Debugger.Info("-----------------Processing include file: '{0}'", includePath); 
                    Pass1st(includePath);
                    Debugger.Info("-----------------Processed include file: '{0}'", includePath); 
                    continue;
                }

                var ip = OpCodeManager.IP;
                var opcode = InstructionManager.Parse(line);

                if (opcode.Length == 0) continue;

                var sb = new StringBuilder();
                foreach (var part in opcode) sb.Append($"{part:X2} ");
                Debugger.Info("[{1}] Parsed opcode: '{0}'", sb.ToString(), ip.ToString("X")); // Debug output

                //if (opcode.Type == TOKENS.LABEL) labels.Enqueue(_ti);
                //else if (opcode.drlbl != null) drlabels.Enqueue(_ti);
                //tokens.Enqueue(opcode);
            }
            fs.Close();
        }
        public static void Assemble(string sourcePath, string outputPath) {
            OpCodeManager.Load();
             
            /// 1st pass
            Pass1st(sourcePath);

            var _tokens = InstructionManager.Parse2nd();

            var dst = File.OpenWrite(outputPath);

            for (int i = 0; i < _tokens.Length; i++) {
                var T = _tokens[i];
                if (T.Type == TOKENS.RES) FileManager.Write(dst, new byte[T.Size]);
                if (T.Type == TOKENS.ALIGN) {
                    var ip = dst.Position;
                    var ab = (byte)T.Regs[1];
                    var align = T.Regs[0];
                    var relIP = ((ip + align - 1) & ~(align - 1)) - ip; 
                    var b = new byte[relIP];
                    for (int j = 0; j < b.Length; j++) b[j] = ab;

                    if (T.Data[0] == 1) {
                        var j = T.Data[1];
                        for (; j > i; j--) {
                            var _T = _tokens[j];
                            int k = b.Length - _T.Bytes.Length;
                            for (int l = 0; l < _T.Bytes.Length; l++, k++) b[k] = _T.Bytes[l];
                        }
                        i = (int)(T.Data[1]+1);
                    } else { 
                        int k = b.Length - T.Bytes.Length;
                        for (int j = 0; j < T.Bytes.Length; j++, k++) b[k] = T.Bytes[j];
                    }
                    dst.Write(b);
                } else FileManager.Write(dst,T.Bytes);
            }
            dst.SetLength(dst.Position);
            dst.Close();
        }
    }
}
