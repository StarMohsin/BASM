# BASM
A custom ASM language with scoped labels, the syntax follows simple NASM

## NASM Syntax
```asm
L1:
  L1.L2:
  L1.L2.L3:
  jmp L1.L2.L3
```

## BASM Syntax
In BASM above code can simply be written as
```asm
L1: {
  L2:  {
    L3: {
      L4:
      jmp .L4 ;    jumps to L1.L2.L3,   '.'   represent current scope aka L3 scope
      jmp ..L3 ;   jumps to L1.L2.L3,   '..'  represent scope 1 level up aka L2 scope
      jmp ...L2 ;  jumps to L1.L2,      '...' represent scope 2 level up aka L3 scope
    }
  }
}
```
## Notice
That's just key idea, for any one who wants to create a custom Assembler, for any type of cpu, this also works as best blue print

So apparently I was writing a NTFS File Parser in NASM for my custom OS, and I took object oriented approach, but because NASM uses single scoped labels, I ran into many problems, the code became unreadable, I was able to able to write the parse code, but yet I also needed the disk driver code, thinking of the complexities I might face again, I just wrote an assembler in my free time which is also available on github as template for anyone who want's to understand how a assembler works, or if anyone wants to create their own custom assemblers for their custom CPU designs.

### Some key features
The code also has a flag to switch between NASM and BASM
This assembler is strict when parsing memory operations
```asm
mov ax, [es:bx+0x100]
```
can only be written this way, and not like this
```asm
mov ax, es:[bx+0x100] ; or
mov ax, es:[bx] +0x100 ; or this
```
because this assemblers considers anything inside '[' ']' a memory access operation, It just to keep it simple.

### How it works
The machine code generated is almost similar to nasm, the difference is, it compiles code in 2 passes only, in 1st pass, it write value '0' for every label, in 2nd pass it overwrites those values with correct values, but unlike nasm it doesn't resize that instruction. Suppose the assembler left 2 bytes space for a LABEL value, but in 2nd pass that LABEL's value turned out to be 1 byte long, at that point , nasm reassigns all the labels with new value, but my assembler doesn't do that, that is indeed in devlopment

### Future updates
Introducing resizeable LABELs' values in 2nd pass

This assembler is focused on creating Flat Bin files
I'm also thinking of add include a file feature like #include<File.asm> like c++, but with file inlined with currrent code
