using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BASM.Classes.DS {

    public class ARG {
        public byte size = 0;
        public byte mod = 0xFF;
        public byte type = 0;

        // reg
        public byte value = 0;
        public byte usec = 0;

        // imm | rm
        public long imme = 0;
        public long imme1 = 0;
        public byte keyw = 0;

        // sib 
        public byte sib = 0;
        public byte seg = 0;

        // label
        public byte unsolved = 0;
        public bool isSolved => (unsolved & 1)==0;
        public string label = "";
        public bool isMem => mod < 3;
        public bool isReg => mod == 3;
        public bool isImm => mod == 4;

        const int sizeKeyW = 5;
        public bool isSizeKeyW => keyw >= sizeKeyW && keyw <= sizeKeyW+4;
        public bool isByte  => keyw == sizeKeyW;
        public bool isWord  => keyw == sizeKeyW + 1;
        public bool isDWord => keyw == sizeKeyW + 2;
        public bool isQWord => keyw == sizeKeyW + 3;
    }
    public class REG : ARG {
        public new const int Type = 1;
        public REG() => type = Type;
    }
    public class IMM : REG {
        public new const int Type = 2;
        public IMM() => type = Type;
    }
    public class RM : IMM {
        public new const int Type = 3;
        public RM() => type = Type;
    }
    public class SIB : RM {
        public new const int Type = 5;
        public SIB() => type = Type;
    }
    public class LABEL : IMM {
        public new const int Type = 4;
        public LABEL() => type = Type;
        //relIP = TIP - (drlbl.IP + 2 + w);

        public static long relIP(long CIP, long TIP,byte size = 2) => TIP - (CIP + size);
    }

    public class OPCODE {
        public string name;
        public long flags;
        public long typeId;
        public long[] codes;

        // for alu inst with ax and imm
        public bool ALU => (flags & 0x10) != 0;

        // for alu inst with ax and imm
        public bool CU => (flags & 0x20) != 0;
        // for inst with zero-operand form
        public bool ZOP => (flags & 0x1) == 0;
        public bool PREF => (flags & 0x10) == 1 && ZOP;
        // for inst with one-operand form, no dw   
        public bool OOP => (flags & 0x1) != 0 && (flags & 0x2) == 0;
    #pragma warning disable format
        public const long DEF_ID    = 0;
        public const long JC_ID     = DEF_ID       +1;
        public const long INT_ID    = JC_ID        +1;
        public const long IO_ID     = INT_ID       +1;
        public const long ROL_ID    = IO_ID        +1;
        public const long INC_ID    = ROL_ID       +1;
        public const long JMP_ID    = INC_ID       +1;
        public const long CALL_ID   = JMP_ID       +1;
        public const long PUSH_ID   = CALL_ID      +1;
        public const long ADD_ID    = PUSH_ID      +1;
        public const long XCHG_ID   = ADD_ID       +1;
        public const long MOV_ID    = XCHG_ID      +1;
    #pragma warning restore format
    }

    public class ParseError : Exception {

    }
    public class DerefferedLabel { 
        public bool Rel = false; 
        public long ORG = 0;
        public long IP = 0;
        public byte Size = 0;
        public string line = "";
        public byte state = 0;
        public Queue<string> labels = new();

        public byte CalSize(long TIP) {
            var relIp = LABEL.relIP(IP, TIP);
            var s = Size;
            bool of = (relIp < -128 || relIp > 127);
            if (s == 2 && of) {

                s = 3;
                relIp = LABEL.relIP(IP, TIP, s);
            }else if (s == 3 && !of) {

                s = 2;
                relIp = LABEL.relIP(IP, TIP, s);
            }

            return s;
        }
    }
}
