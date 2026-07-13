using BASM.Classes.Handlers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;
using static BASM.Classes.Managers.OpCodeManager.Token;

namespace BASM.Classes.Managers {
    public class InstructionManager {

        // yeah, tokens are static here, as LabelHandler refrences them, to get the IP of a label,
        // and the IP is stored in the token's field "Regs[0]"
        public static OpCodeManager.Token[] Tokens = [];

        static LinkedList<OpCodeManager.Token> tokens = new();
        //Queue<OpCodeManager.Token> tokens = [];
        static LinkedList<int> drTokens = []; 
        public static byte[] Parse(string line) {
            var _ti = tokens.Count;
            var opcode = OpCodeManager.ParseAsToken(_ti,line); 
            
            if (opcode.Type == TOKENS.DB && opcode.Str.Length != 0) {
                var _oplabel = opcode.Clone();

                _oplabel.Type = TOKENS.LABEL;
                _oplabel.Bytes = [];
                drTokens.AddLast(_ti);
                tokens.AddLast(_oplabel);

                
                _ti = tokens.Count;
                drTokens.AddLast(_ti);
                tokens.AddLast(opcode);
            }
            else if (opcode.Type == TOKENS.LABEL ||
                opcode.Type == TOKENS.ALIGN || 
                opcode.Type == TOKENS.CB
                ) drTokens.AddLast(_ti);
            else if (opcode.drlbl != null) drTokens.AddLast(_ti);
            tokens.AddLast(opcode);
            return opcode.Bytes;
        }

        public static OpCodeManager.Token[] Parse2nd() {
            LabelHandler.parseStage = 2;
            Debugger.Warn("--------------------------------------- 2nd Parse");
            // 2nd pass
            var _tokens = Tokens = tokens.ToArray(); 
            var _drTokens = drTokens.ToArray();

            Dictionary<string, int> _done = new();
            Dictionary<string, Queue<int>> _undone = new();

            long diff = 0; 
            int j = -1;
            for(int i = 0; i < _drTokens.Length; i++) {
                var _drTI = _drTokens[i]; 
                var _drT = _tokens[_drTI]; 
                 
                if(_drT.Type == TOKENS.LABEL) {
                    _drT.Regs[0] += diff;
                    if(!_drT.Str.StartsWith('.')) LabelHandler.cLabel = _drT.Str;
                    Debugger.Warn($"--------------------------------------- 3rd Parse for label \"{_drT.Str}\"");
                    Debugger.Info($"Diff: {diff}");
                    if (_undone.TryGetValue(_drT.Str, out var _ud)) { 
                        foreach(var _udi in _ud) {
                            var __drT = _tokens[_udi];

                            var drlbl = __drT.drlbl;
                            OpCodeManager.IP = drlbl.IP;
                            var _t = OpCodeManager.ParseAsToken(_udi, drlbl.line);
                            _t.Data = _tokens[_udi].Data;
                            _tokens[_udi] = _t;
                        }
                    } 
                    if(!_done.ContainsKey(_drT.Str)) _done.Add(_drT.Str, _drTI);

                    Debugger.Warn($"--------------------------------------- 3rd Parse - done \"{_drT.Str}\"");
                    continue;
                } 

                if (_drT.drlbl != null) {
                    var drlbl = _drT.drlbl;
                    OpCodeManager.IP = drlbl.IP + diff;
                    var _t = OpCodeManager.ParseAsToken(_drTI, drlbl.line);
                    _t.Data = _tokens[_drTI].Data;
                    _t.drlbl = _tokens[_drTI].drlbl;
                    _tokens[_drTI] = _t;

                    var dif = (_t.Size - _drT.Size);
                    diff += dif;
                    if (_drT.Type == TOKENS.ALIGN) {
                        if (_t.Data[0] == 1) {
                            var _cbti = _drT.Data[1];

                            long _cbSize = 0;
                            long eDiff = 0;
                            for (int k=i+1;k< _cbti; k++) {  
                                var _cbdrT = _tokens[k];
                                var _cbdrlbl = _cbdrT.drlbl;

                                if (_cbdrT.Type == TOKENS.LABEL) continue;


                                if (_cbdrlbl != null) {
                                        OpCodeManager.IP = _cbdrlbl.IP + eDiff;
                                    var _t2 = OpCodeManager.ParseAsToken(k, _cbdrlbl.line); 
                                    var dif2 = (_t2.Size - _cbdrT.Size);
                                    _cbdrT = _t2;
                                    eDiff += dif2;
                                }
                                _cbSize += _cbdrT.Size;
                            }

                            var align = _t.Regs[0];
                            var cip = drlbl.IP + diff;
                            var ip = (cip + align - 1) & ~(align - 1);
                            var relIP = ip - cip;

                            if (relIP == 0) relIP += align;

                            var eip = relIP - _cbSize;

                            diff += eip - _drT.Size;
                            _t.Size = relIP;
                        } 
                    }
                    else { 
                        long maxIP = -1;
                        string _l = "";
                        foreach (var l in drlbl.labels) {
                            if (!_done.TryGetValue(l, out _)) {

                                if (LabelHandler.TryGetValue(l, out var _ip) && _ip.imme > maxIP) {
                                    maxIP = _ip.imme;
                                    _l = l;
                                } 
                            }
                        }
                        if (maxIP != -1 && _l.Length > 0) {
                            if (!_undone.TryGetValue(_l, out var _ud)) _undone.Add(_l, _ud = new());
                            drlbl.IP += diff;
                            _ud.Enqueue(_drTI);
                        }
                    }
                }
            }

            return _tokens;
        }
    }
}
