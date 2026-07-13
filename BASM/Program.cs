

using BASM.Classes;
using BASM.Classes.Managers;
using System.Security.Cryptography.X509Certificates;

Debugger.ShowConsole();

Debugger.Run(() => {

    string dir = "";
    string sourceFile = "source.asm";
    string outputFile = "output.bin";

    var file = Path.Combine(dir, sourceFile);
    if (!File.Exists(file)) {
        Debugger.Warn("Could not find File:\"{0}\"", file);
        dir = "../../../Resources";
        file = Path.Combine(dir, file);
        Debugger.Log("looking for File:\"{0}\"", file);
    }
    Assembler.Assemble(Path.Combine(dir,sourceFile), Path.Combine(dir,outputFile));  
});