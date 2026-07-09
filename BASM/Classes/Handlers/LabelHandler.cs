using BASM.Classes.DS;
using BASM.Classes.Managers;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace BASM.Classes.Handlers {
    public class LabelHandler { 
        class LABEL {
            public string label;
            public long IP = 0; 

            public LABEL(string label, long iP) {
                this.label = label;
                IP = iP; 
            }
        }

        static Queue<DerefferedLabel> derefferedLabels = new();
        static Dictionary<string, LABEL> labels = new();
        static Dictionary<string, int> labelInd = new();
        static List<string> labelSeq = new();
        //static Queue<DerefferedLabel> derefferedLabels = new Queue<DerefferedLabel>();
        static Stack<string> labelStack = new Stack<string>();
        static string cLabel = "", _clabel = "";

        static byte NASM => OpCodeManager.NASM;
        public static byte parseStage = 1;
        public static void AddLabel(string label, long IP) {
            if (label.Length == 0) return;
            if (labels.TryGetValue(label, out _)) return;

            if (label.StartsWith('.')) label = cLabel + label;
            else {
                cLabel = label;
                var cl = (labelStack.Count>0)? labelStack.Peek():"";
                if (cl.Length > 0) cl += ".";
                _clabel = label = cl + label;
            }

            Debugger.Info($"Saved '{label}' with {IP}");
            LABEL _lbl = new(label, IP);
              
            labels.Add(label, _lbl);
            labelSeq.Add(label);

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
            HashSet<char> InvalidSymbols = ['+', '-', '*', '/'];
            foreach (var c in label) {
                if(InvalidSymbols.Contains(c)) return false; 
            }
            return true;
        }
        public static bool TryGetValue(string lbl, out DS.LABEL imm,byte keyw = 0) {
            imm = new DS.LABEL();
            if (!isLabelValid(lbl)) return false;
            var src = lbl;
            if (lbl.StartsWith('.')) {
                if ((NASM & 1) > 0) src = cLabel + lbl;
                else {
                    int len = lbl.Length;
                    int i = 0;
                    for (; i < len; i++) if (lbl[i] != '.') break;

                    var _stack = labelStack.ToArray();
                    len = _stack.Length;
                    if (len - i -1>= 0) {
                        src = _stack[len - i - 1] + "." + lbl.Substring(i);
                    }
                }
            }
            imm.keyw = keyw;
            imm.label = src;
            imm.unsolved = 1;
            imm.size = (byte)2;
            Debugger.Log($"looking for {src}");
            if (!labels.TryGetValue(src, out var _lbl)) return false;
            var imme = _lbl.IP;
            imm.imme = imme - LabelOff;
            if(parseStage == 1) {
                imm.size = (byte)2;
                imm.unsolved = 1; // forcing as unsolved drlabel,
            } else {
                imm.unsolved = 0; 
                imm.size = MemoryHandler.getSize(imme);
            }

            Debugger.Info($"found {src}:{imm.imme} = {imme} - {LabelOff}");
            return true; 
        }
        public static DS.LABEL Parse(string src, byte keyw = 0) {
            if (TryGetValue(src, out var imm,keyw)) return imm;
            Debugger.Error($"Couldn't parse LABEL : {src}");
            return imm;
        }

        static long LabelOff = 0; // current label offset
        // 2nd pass
        public static void ParseAllDereferredLabels(FileStream dst) {
            Debugger.Log();
            Debugger.Log("2nd Parse for labels");
            Debugger.Log();
            Debugger.Log();

            parseStage = 2;
            long LIP = 0; 
              
            long IPoff = 0;
            long diffI = 0;
            long diffJ = 0;
            long diffK = 0;
            long _diffK = 0;

            var _instCache = new List<byte>();
            int _instCacheLen = 16;

            byte write(DerefferedLabel drlbl) {  
                var IP = drlbl.IP;

                if (_diffK != 0) {
                    int bufLen = (int)(IP - LIP);
                    if (bufLen > 0) {
                        byte[] buf = new byte[bufLen];

                        var dsPos = dst.Position;
                        dst.Position = LIP;
                        dst.Read(buf, 0, bufLen);
                        dst.Position = LIP - (_diffK);
                        if(_diffK < 0) {
                            var difAbs = Math.Abs(_diffK);
                            var __inst = _instCache.ToArray();
                            for(int i=0;i<difAbs;i++) buf[i] = __inst[__inst.Length+_diffK +i];
                        }
                        dst.Write(buf, 0, bufLen);
                        dst.Position = dsPos;
                        _diffK = 0;
                        _instCache.Clear();
                    }
                }

                IP -= (diffI+diffJ+diffK); 
                LIP = drlbl.IP + drlbl.Size;

                OpCodeManager.IP = IP;
                var inst = OpCodeManager.Parse(drlbl.line);
                var _size = inst.Length;
                var dif = drlbl.Size - _size;
                //diff += dif;
                diffK += dif;
                _diffK += dif;
                if (dif < 0) {
                    dst.Position = IP;
                    _instCacheLen = _size;
                    var _buf = new byte[_instCacheLen];
                    dst.Read(_buf, 0, _instCacheLen);
                    _instCache.AddRange(_buf);
                    dst.Position = IP;
                } 
                dst.Position = IP;
                dst.Write(inst, 0, inst.Length);
                var sb = new StringBuilder();
                foreach (var part in inst) sb.Append($"{part:X2} ");
                Debugger.Info("[{1}] Parsed opcode: '{0}'", sb.ToString(), IP.ToString("X")); // Debug output
                return 0;
            }

            //
            var dstLen = dst.Length;


            /// I
            diffI = 0;

            int lblInd = 0;
            var _labelSeq = labelSeq.ToArray();
            Dictionary<string, int> Done = new(); 
            var _drLabels = derefferedLabels.ToArray();

            int i = 0, j = 0;
            void writeAll() {
                diffJ = 0;
                for (; j < i; j++) {
                    diffK = 0;
                    write(_drLabels[j]);
                    diffJ += diffK;
                    //if (s != diffK) LabelOff += diffK - s;
                }
                diffI += diffJ;
            }
            for (; i<_drLabels.Length; i++) {
                var drlbl = _drLabels[i];


                if (lblInd >= _labelSeq.Length || 
                    drlbl.IP >= labels[_labelSeq[lblInd]].IP) {
                    var _cl = _labelSeq[lblInd];
                    Debugger.Log($"Label done {_cl}");
                    Done.Add(_cl, lblInd);
                    lblInd++;
                    writeAll();
                }

                int s = 0; 
                foreach (var _drlbl_i in drlbl.labels) {
                    if (!Done.TryGetValue(_drlbl_i,out _)) {
                        //s = drlbl.Size - drlbl.CalSize(labels[_drlbl_i].IP);
                        long IP =drlbl.IP - s; 

                        OpCodeManager.IP = IP;
                        var inst = OpCodeManager.Parse(drlbl.line); 

                        s += drlbl.Size - inst.Length;
                        Debugger.Log($"Size diff {s} for {_drlbl_i}");
                         
                        break;
                    }
                }
                LabelOff += s; 
            }
            writeAll();

            dstLen -= diffI;
            if(dst.Length != dstLen) dst.SetLength(dstLen);
        }
    }
}
