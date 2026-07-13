



jmp MAGIC
align 0x200, 0x90, {
	dw 0x55AA
}
MAGIC:
	.DAP:
jmp .DAP


#include<source1.asm>