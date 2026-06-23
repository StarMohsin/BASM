# BASM
A custom ASM language with scoped labels, the syntax follows simple NASM

## NASM Syntax
```
L1:
  L1.L2:
  L1.L2.L3:
  jmp L1.L2.L3
```

## BASM Syntax
In BASM above code can simply be written as
```
L1: {
  L2:  {
    L3: {
      jmp ..L3 ; jumps to L1.L2.L3, '..' represent scope 1 level up aka L2 scope, '.' represents current scope
    }
  }
}
```
## Notice
That's just key idea, for any one who wants to create a custom Assembler, for any type of cpu, this also works as best blue print

### Some key features
The code also has a flag to switch between NASM and BASM
This assembler is strict when parsing memory operations
```
mov ax,[es:bx+0x100]
```
can only be written this way, and not like this
```
mov ax, es:[bx+0x100] ; or
mov ax, es:[bx] +0x100 ; or this
```
because this assemblers considers anything inside '[' ']' a memory access operation, It just to keep it simple.
