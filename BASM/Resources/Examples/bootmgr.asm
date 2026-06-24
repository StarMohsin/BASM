; NASM

ORG 0x20000
bits 16
; bootmgr of NTFS 2nd stage bootloader
; cs = 0x2000
; ss = 0
; A20 = 1
jmp main@F
align 4
    dd Console@C.ISR@F
align 0x10
CONST:
    .DAP dd Drive@C.DAP@S
    dd NTFS@C.MSG.path 
    dd NTFS@C.File@C
Console@C:
    .SP  dd 0
    .Caret dd 0
    .key dd 0
    .ISR@F:
        cmp ah, 0x1
        je  .Write@F
        cmp ah, 0x10
        je  .WriteStr@F
        ret
    .clrscr:
        ; Clear screen (BIOS)
        mov ax, 0x0600
        mov bh, 0x07
        mov cx, 0x0000
        mov dx, 0x184F
        int 0x10
        ret 
    .ISR9:
        push ax
        in al, 0x60                     ; Read scancode from Keyboard Controller data port
        
        mov [.key], al
        ; 4. Acknowledge the keyboard controller (tell it we read the byte)
        in al, 0x61
        or al, 0x80
        out 0x61, al
        and al, 0x7F
        out 0x61, al
    
        ; 5. Send End of Interrupt (EOI) to the Primary PIC (0x20)
        mov al, 0x20
        out 0x20, al 
        pop ax
        iret
    .Init@F:
        cli
        mov dword[9*4], .ISR9
        sti
    .HLT@F:
        pushad
        mov [.SP], esp
        sti
        mov al, 'H' 
        .ReadKey@F.@L: 
            call .Write@F
            ; Wait for a key press (blocking)
            mov ah, 0x00        ; Subfunction 00h: Read character
            int 0x16            ; Call BIOS Keyboard Video Services 
            cmp al, 0x20
            jne .ReadKey@F.@L
        cli 
        popad
        ret
    .ReadKey@F:
        push ax  
        mov ah, 0x00        ; Subfunction 00h: Read character
        int 0x16            ; Call BIOS Keyboard Video Services  
        mov [.key], al
        pop ax
        ret
    .Write@F:
        push ax
        push edi
        mov edi, [Console@C.Caret] 
        
        mov ah, 0xf
        mov word [ss:edi+0xB8000], ax
        inc edi
        inc edi 
        mov [Console@C.Caret], edi 
        
        pop edi
        pop ax
        ret
    .WriteStr@F: 
        push ax
        push edi
        push esi 
        mov edi, [.Caret] 
        jmp .WriteStr@F.@B
    .WriteStr@F.@R:
        mov [.Caret], edi
        pop esi
        pop edi
        pop ax 
        ret 
    .WriteStr@F.@B: 
        mov ah, 0xf
        .WriteStr@F.@B.@L1:
            mov al, [ss:esi]
            test al, al
            jz .WriteStr@F.@R
            mov word [ss:edi+0xB8000], ax
            inc edi
            inc edi
            inc esi
            jmp .WriteStr@F.@B.@L1
        

Drive@C:
    align 4
    .DAP@S:
        db 0x10                 ; 0x000 Size of packet (always 16 bytes / 0x10)
        db 0x00                 ; 0x001 Reserved (always 0)
        .DAP@S.Count dw 1       ; 0x002 Number of sectors to read (1 sector = 512 bytes)
        .DAP@S.Off dw 0         ; 0x004 Target Offset (16-bit pointer)
        .DAP@S.Seg dw 0x3000    ; 0x006 Target Segment (16-bit pointer)
        .DAP@S.LBA dq 0         ; 0x008 64-bit absolute LBA address (Sector 0)  
    .DAP_PTR dd .DAP@S
        .DAP@S.SetAddr@F:
            push eax
            push ebx
            mov ebx, [.DAP_PTR]
            mov [ss:ebx+ 0x4], ax
            shr eax, 0x10
            shl eax, 0xc
            mov [ss:ebx+ 0x6], ax
            pop ebx
            pop eax
            ret
        .DAP@S.GetAddr@F:  
            push edx
            push ebx
            xor eax, eax
            mov edx, [.DAP_PTR]
            mov ax, [ss:edx+ 0x6]
            shl eax, 0x4 
            xor ebx, ebx
            mov bx, [ss:edx+ 0x4]
            add eax, ebx
            pop ebx 
            pop edx
            ret
    .Malloc@F:
        push eax
        push ebx
        push edx
         
        call .DAP@S.GetAddr@F
        xor ebx, ebx
        mov edx, [.DAP_PTR]
        mov bx, [ss:edx+ 0x2]
        shl ebx, 9
        add eax, ebx
        call .DAP@S.SetAddr@F
        
        pop edx
        pop ebx
        pop eax
        ret 
    .MFT dq 0    
    .MFT_ROOT dq 0
    .ReadLBA@F:
        push ax
        push dx
        push si
        ; 2. Execute the Extended LBA Read
        mov ah, 0x42                ; Extended Read Sectors function
        mov dl, 0x80;[BOOT_DRIVE]        ; Your saved boot drive ID (e.g., 0x80)
        mov si, [.DAP_PTR] ; Pointer to the DAP structure below 
        
        clc
        int 0x13                    ; Call BIOS Storage Services
        jc .read_failed             ; If Carry Flag (CF) is set, an error occurred!
           
        ; Success! Sector 0 data is now sitting at 0x2000:0000
        pop si
        pop dx
        pop ax
        ret
    .read_sector:   
        push ebx
        mov ebx, [.DAP_PTR]
        mov dword [ss:ebx+ 0x8], eax
        call .ReadLBA@F
        pop ebx
        ret
    .read_sector_0:   
        push ebx
        mov ebx, [.DAP_PTR]
        mov dword [ss:ebx+ 0x8], 0
        call .ReadLBA@F
        pop ebx
        ret
    
    .read_failed:
        ; AH contains the error code status. 
        ; Often a retry or reset (AH=0, INT 0x13) is called here if it fails on real hardware.
        mov esi, MSG.read_error
        call Console@C.WriteStr@F
        cli
        hlt 
NTFS@C: 
    .MFT_LBA dq 0
    .MFT_MEM dd 0  
    .DAP@S:
        db 0x10                 ; 0x000 Size of packet (always 16 bytes / 0x10)
        db 0x00                 ; 0x001 Reserved (always 0)
        .DAP@S.Count dw 1       ; 0x002 Number of sectors to read (1 sector = 512 bytes)
        .DAP@S.Off dw 0         ; 0x004 Target Offset (16-bit pointer)
        .DAP@S.Seg dw 0x3000    ; 0x006 Target Segment (16-bit pointer)
        .DAP@S.LBA dq 0         ; 0x008 64-bit absolute LBA address (Sector 0)
    .MSG: 
        .MSG.INDX db "INDX found", 0
        .MSG.FILE db "FILE found", 0
        .MSG.path db "build16/diskmgr.bin",0
    .loadEAX:
        xor eax, eax
        .loadEAX.L1:
            shl eax, 8
            mov al, [ss:edi+ecx]
            loop .loadEAX.L1
        ret
    .ReadSec@F:
        ;args 
        ; cx = count
        ; eax = lba 
        push ebx
        mov ebx, .DAP@S
        mov [Drive@C.DAP_PTR], ebx
        mov [.DAP@S.Count], cx
        mov [.DAP@S.LBA], eax 
        call Drive@C.ReadLBA@F
        pop ebx
        ret
        
    .Init@F:  
        xor ecx, ecx 
        mov cl,  [ss:0x7C0D] ; sectors per cluster
        mov eax, [ss:0x7C30] ; MFT cluster
        mul ecx
        add eax, [ss:0x7C1C] ; Hidden Sectors
        
        mov [.MFT_LBA], eax 
        add eax, 10          ; 5*2 = 5th record of MFT = root record
        
        mov cx, 2
        call .ReadSec@F
        
        mov esi, MSG.loadedMFT0 
        call Console@C.WriteStr@F
        
        call Drive@C.DAP@S.GetAddr@F
        mov edi, eax 
        call Drive@C.Malloc@F
        
        call Console@C.HLT@F
        mov esi, .MSG.path
        call .LoadPathMFT@F
        
        mov dword[.File@C.DAP@S.Off], eof
        call NTFS@C.File@C.Read@F
        
        mov al, 'j'
        call Console@C.Write@F
        mov eax, [NTFS@C.File@C.BASE] 
        push 0x3000
        push 0x548 
        retf
        cli
        hlt
    
    .LoadPathMFT@F:
        push ebp
        mov ebp, esp 
        push eax
        push ebx
        
        mov eax, [ss:edi]
        cmp eax, 0x454C4946
        je  .parseFILE
        
        jmp .parseINDX
    .LoadPathMFT@F.@@R: 
        mov al, 'r'
        call Console@C.Write@F
        pop ebx
        pop eax
        mov esp, ebp
        pop ebp
            
        ret    
    .parseINDX:
        mov al, 'I'
        call Console@C.Write@F
        xor ebx, ebx 
        movzx ebx, word[ss:edi+0x18]; load 1st entry offs 
        add bx, 0x18
             
        call Console@C.HLT@F
         
        call .parse@F
        call Console@C.HLT@F
        jmp .LoadPathMFT@F.@@R
    .parseFILE: 
        mov al, 'F'
        call Console@C.Write@F
        xor bx, bx
        mov bx, [ss:edi+0x14] ; load first entry offset to bx
        
        .@@FILE.L1:
            mov ax, [ss:edi+ebx] ; load type to ax
            cmp ax, 0x90
            je .@@FILE.INDX_ROOT
            cmp ax, 0xA0
            je .@@FILE.INDX_ALLOC
            cmp ax, 0xFFFF
            je .@@FILE.ERROR1
        .@@FILE.L1.M:
            add bx, [ss:edi+ebx+0x4] ; add length to entry offset to get next entry 
            jmp .@@FILE.L1
        .@@FILE.INDX_ROOT: 
            push bx
            add bx, [ss:edi+ebx+0x14]
            add bx, [ss:edi+ebx+0x10] 
            add bx, 0x10 
            call .parse@F
            mov al, 'i'
            call Console@C.Write@F 
            pop bx
            jmp .@@FILE.L1.M
        
        .@@FILE.INDX_ALLOC: 
            mov al, 'a'
            call Console@C.Write@F
            
            call .parseDATA_RUN 
             
            call .ReadSec@F
            
            call Drive@C.DAP@S.GetAddr@F
            mov edi, eax   
            call Console@C.HLT@F
            
            jmp .parseINDX
        
        
        .parseDATA_RUN:
            push edi
            add edi, ebx
            push ebx
            movzx ebx, word[ss:edi+0x20]
            add edi, ebx
            
            xor ecx, ecx 
            mov cl, [ss:edi] 
            mov ch, cl
            and cl, 0xF
            and ch, 0xF0
            shr ch, 4 
            
            push ebp
            push cx
            mov ebp, esp
        
        
            movzx ebx, byte[ss:0x7C0D]
            
            movzx ecx, byte[ss:ebp]
            call .loadEAX
            mul ebx
            ;mov [DRIVE.DAP.Count], ax
            push ax
            
            movzx ecx, byte[ss:ebp]  
            add edi, ecx
             
            movzx ecx, byte[ss:ebp+1]  
            call .loadEAX 
            mul ebx 
            add eax, [ss:0x7C1C]
           
            pop cx
            pop dx
            pop ebp
            pop ebx
            pop edi
            ret
        .@@FILE.L1_L:
        jmp .parse@F
        
        .@@FILE.ERROR1:
        mov al, 'E'
        call Console@C.Write@F  
        ret
        
    .parse@F: 
        ; parses INDX ENTRIES of INDX_ROOT or INDX_ALLOC
        ; args
        ; ebx = offset to 1st entry
        ; esi = ptr to path
        ; edi = ptr to MFT
        push ebp
        mov ebp, esp
        push eax
        push ebx
        push ecx
        push edx
        push esi
        push edi 
        jmp .parse.@@B
        .parse.@@R:
        mov al, 'r'
        call Console@C.Write@F 
        pop edi 
        pop esi
        pop edx
        pop ecx
        pop ebx 
        pop eax
        mov esp, ebp
        pop ebp
        ret
        .parse.@@B:
            mov al, 'P'
            call Console@C.Write@F
            add edi, ebx
        .@@L1:
            mov cx, [ss:edi+0x8]; load length of entry
            jcxz .parse.@@R
            mov dx, cx 
            
            mov bl, [ss:edi+0xC]; load flags
            test bl, 0x1 ; last entry
            jnz .parse.@@R
            
            ;test bl, 0x2
            ;jnz .parse.@@R
            
            ;mov cx, [es:si+0x10] ; move to filename part of Filename attr  
            movzx cx, byte[ss:edi+0x50]
             
            .@@L2_E: 
            xor ebx, ebx 
            .@@L2:
                mov ax, word[ss:edi+0x52+ebx*2]
                mov dl, byte[ss:esi+ebx]
                 
                call Console@C.Write@F 
                cmp al, dl
                jne .@@3 ; break
                 
                inc ebx  
                 
                loop .@@L2
            .@@L2_L: 
                call Console@C.HLT@F
            cmp byte[ss:esi + ebx], 0 
            je   .parse.@@C1
            cmp byte[ss:esi + ebx], '/' 
            jne .@@3
            .parse.@@C1:
            add esi, ebx
            inc esi 
            
            jmp .@@4
            
            .@@3:  
            add di, [ss:edi+0x8]
            jmp .@@L1
        .@@4:
        mov al, '/'
        call Console@C.Write@F 
        
        mov eax, [ss:edi]
        shl eax, 1
        
        add eax, [.MFT_LBA] 
        mov cx, 2
        call .ReadSec@F
         
        cmp byte[ss:esi-1], 0 
        je .parse.@@R
        call Console@C.HLT@F
         
        mov edi, [ss:esp]   
        call .LoadPathMFT@F 
        jmp .parse.@@R
    .File@C:
        .File@C.DESC  dd 0 ; 0x000
        .File@C.BASE  dq 0 ; 0x004
        .File@C.PTR   dq 0 ; 0x00C
        .File@C.SIZE  dd 0 ; 0x014
        .File@C.LEN   dd 0 ; 0x018
        .File@C.FLAGS dd 0 ; 0x01C
        .File@C.MFT   dd 0 ; 0x020
        .File@C.LBA   dq 0 ; 0x024
    .FILE_PTR dd .File@C
        .File@C.DAP@S:
	    db 0x10                 ; 0x000 Size of packet (always 16 bytes / 0x10)
	    db 0x00                 ; 0x001 Reserved (always 0)
            .File@C.DAP@S.Count dw 1       	; 0x002 Number of sectors to read (1 sector = 512 bytes)
            .File@C.DAP@S.Off dw 0         	; 0x004 Target Offset (16-bit pointer)
            .File@C.DAP@S.Seg dw 0x3000    	; 0x006 Target Segment (16-bit pointer)
            .File@C.DAP@S.LBA dq 0         	; 0x008 64-bit absolute LBA address (Sector 0)
    .File@C.ReadSec@F:
        push ebx,
        mov ebx, .File@C.DAP@S 
        mov [Drive@C.DAP_PTR], ebx
        mov [.File@C.DAP@S.Count], cx
        mov [.File@C.DAP@S.LBA], eax
        call Drive@C.ReadLBA@F
        pop ebx
        ret
        
    .File@C.Read@F:
        ;args
        ; edi = MFT location
        push eax
        push ebx
        push ecx
        mov al, 'f'
        call Console@C.Write@F
        mov eax, [ss:edi]
        cmp eax, 0x454C4946 
        je .File@C.Read@F.@@B
        .File@C.Read@F.@@R:
        
        mov al, 'f'
        call Console@C.Write@F
        pop ecx
        pop ebx
        pop eax
        ret
        .File@C.Read@F.@@B: 
            movzx ebx, word[ss:edi+0x14] 
        .File@C.Read@F.@@L1:
            mov ax, [ss:edi+ebx]
            call Console@C.HLT@F
            cmp ax, 0xFFFF
            je 		.File@C.Read@F.@@R
            cmp ax, 0x80
            je 		.File@C.Read@F.@@DATA
            mov ecx, [ss:edi+ebx+0x4]
            jcxz 	.File@C.Read@F.@@R
            add ebx, ecx  
            jmp 	.File@C.Read@F.@@L1
         
        .File@C.Read@F.@@DATA:
            mov al, 'F'
            call Console@C.Write@F
            mov al,  [ss:edi+ebx+0x8 ]
            test al, 0xFF
            jnz .File@C.Read@F.@@DATA.NR
            
            mov eax, [ss:edi+ebx+0x14]
            mov ecx, [ss:edi+ebx+0x10]
            add eax, ebx
            lea eax, [ss:edi+eax]
            mov ebx, [.FILE_PTR]
            mov [ss:ebx+ 0x4], eax
            mov [ss:ebx+ 0x18], ecx
            
            jmp .File@C.Read@F.@@R 
        .File@C.Read@F.@@DATA.NR:
            call .parseDATA_RUN 
            mov ebx, [.FILE_PTR]
            mov [ss:ebx+ 0x24], eax 
            mov [ss:ebx+ 0x14], ecx
             
            call .ReadSec@F
            
            mov ecx, .File@C
            mov al, 'N'
            call Console@C.Write@F
            
            jmp .File@C.Read@F.@@R
    
MSG:
    .read_error db "Failed to read Sector 0 via LBA!", 0x0D, 0x0A, 0 
    .hello db "Hello World", 0
    .loadedMFT0 db "MFT sector 0 Loaded" ,0
Interrupts@C:
    .ISR21@F:
        mov al, 'I' 
        call Console@C.Write@F
        iret
    .Init@F:
        mov word[ss:0x41*4], .ISR21@F
        mov word[ss:0x41*4+4], 0x2000
        cli
        hlt
        int 0x41
        ret
main@F: 
    mov ax, 0x2000
    mov ds, ax  
    
    call Console@C.clrscr
    
    mov al, 'M' 
    call Console@C.Write@F 
    
    call NTFS@C.Init@F
    cli
    hlt

eof:


