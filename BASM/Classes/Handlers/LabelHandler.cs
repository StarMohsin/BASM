using BASM.Classes.DS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace BASM.Classes.Handlers {
    public class LabelHandler { 
        static Dictionary<string, long> labels = new Dictionary<string, long>();
        static Queue<DerefferedLabel> derefferedLabels = new Queue<DerefferedLabel>();
        static Stack<string> labelStack = new Stack<string>();
        static string cLabel = "", _clabel = "";
        public static void AddLabel(string label, long IP) {
            if (label.Length == 0) return;
            if (labels.TryGetValue(label, out var key)) return;

            if (label.StartsWith('.')) label = cLabel + label;
            else {
                cLabel = label;
                var cl = (labelStack.Count>0)? labelStack.Peek():"";
                if (cl.Length > 0) cl += ".";
                _clabel = label = cl + label;
            }

            Debugger.Info($"Saved '{label}' with {IP}");
            labels.Add(label, IP);
        } 
        public static void PushLabel() => labelStack.Push(_clabel);
        public static void PopLabel() => labelStack.Pop();
        public static void AddDereferredLabel(DerefferedLabel lbl) => derefferedLabels.Enqueue(lbl);
        public static bool isLabelValid(string label) {
            if(label.Length == 0) return false;
            char fc = label[0];
            if(fc >= '0' && fc <= '9') return false;
            char lc = label[label.Length - 1];
            if(lc == 'h') {
                if(fc >= 'A' && fc<='F') return false; 
                if(fc >= 'a' && fc<='f') return false; 
            }
            string symbols = "+-*/";
            foreach(var c in label) {
                foreach (var s in symbols) if (c == s) return false;
            }
            return true;
        }
        public static bool TryGetValue(string lbl, out DS.LABEL imm,byte keyw = 0) {
            imm = new DS.LABEL();
            if (!isLabelValid(lbl)) return false;
            var src = lbl;
            if (lbl.StartsWith('.')) src = cLabel + lbl;
            imm.keyw = keyw;
            imm.label = src;
            imm.unsolved = 1;
            Debugger.Log($"looking for {src}");
            if (!labels.TryGetValue(src, out var imme)) return false;
            ulong _size = (ulong)imme;
            if (_size <= 0xFF) _size = 1;
            else if (_size <= 0xFFFF) _size = 2;
            else if (_size <= 0xFFFFFFFF) _size = 4;
            else if (_size <= 0xFFFFFFFFFFFFFFFF) _size = 8;
            else _size = 8;
            imm.imme = imme;
            imm.unsolved = 0;
            Debugger.Info($"found {src}");
            return true; 
        }
        public static LABEL Parse(string src, byte keyw = 0) {
            if (TryGetValue(src, out var imm,keyw)) return imm;
            Debugger.Error($"Couldn't parse LABEL : {src}");
            return imm;
        }
        public static void ParseAllDereferredLabels(FileStream dst) {
            while (derefferedLabels.Count > 0) {
                var drlbl = derefferedLabels.Dequeue();
                if (!labels.TryGetValue(drlbl.label, out var IP)) {

                    //Debugger.Error($"Couldn't locate label {drlbl.label}");
                    //continue;
                    IP = EqParser.Parse(drlbl.label, (string lbl, out long v) => {
                        v = 0;
                        if (MemoryHandler.TryParse(lbl, out v)) return true;
                        if (LabelHandler.TryGetValue(lbl, out var imm)) {
                            v = imm.imme;
                            return true;
                        }
                        return false;
                    }).Solve();
                }
                dst.Seek(drlbl.IP + drlbl.Off, SeekOrigin.Begin);

                long v =  IP; 
                if (drlbl.opcode == 1) {
                    v = IP - (drlbl.ORG + drlbl.IP + drlbl.instSize);
                }
                Debugger.Log($"Parsed DRLABEL: {drlbl.label} RELIP:0x{v:X} IP:0x{IP:X} drIP:0x{drlbl.IP:X} drOFF:0x{drlbl.Off:X}");

                dst.WriteByte((byte)(v & 0xFF));
                if(drlbl.size > 1) dst.WriteByte((byte)((v>>8) & 0xFF));
            }
        }
    }
}
