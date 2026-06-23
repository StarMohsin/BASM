

using BASM.Classes;
using BASM.Classes.Managers;

Debugger.ShowConsole();

Debugger.Run(() => {
    Assembler.Assemble("source.asm", "output.bin");  
});