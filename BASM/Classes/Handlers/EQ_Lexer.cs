using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using BASM.Classes.DS;

namespace BASM.Classes.Handlers {
    public enum TokenType {
        Atom,       // Represents numbers (0-9) or variables (a-z)
        Operator,   // +, -, *, /, =
        Eof,        // End of file / stream
        LeftParen,  // (
        RightParen  // )
    }

    public class Token {
        public TokenType Type { get; }
        public string Value { get; }

        public Token(TokenType type, string value) {
            Type = type;
            Value = value;
        }
    }

    public class EqLexer {
        private readonly Stack<Token> _tokens = new();

        bool IsOperator(char c) =>
            (c == '+') ||
            (c == '-') ||
            (c == '*') ||
            (c == '/');

        public EqLexer(string source,SymbolParser parse = null) {
            // Reverse parsing implementation [00:08:38] 
            // Reversing allows O(1) popping off the end of a collection stack
            var tempTokens = new List<Token>();

            var sb = new StringBuilder();

            void addWord() {
                if (sb.Length == 0) return;
                if (parse!=null && parse(sb.ToString(), out var v)) sb = new(v+"");
                tempTokens.Add(new Token(TokenType.Atom, sb.ToString()));
                sb.Clear(); 
            }
            foreach (char c in source) {
                if (char.IsWhiteSpace(c)) continue;

                if (c == '(') {
                    addWord();
                    tempTokens.Add(new Token(TokenType.LeftParen, "("));
                } else if (c == ')') {
                    addWord();
                    tempTokens.Add(new Token(TokenType.RightParen, ")"));
                }else if (IsOperator(c)) {
                    addWord();
                    tempTokens.Add(new Token(TokenType.Operator, c.ToString())); 
                } else
                    sb.Append(c);
            }

            addWord();
            tempTokens.Reverse();
            foreach (var t in tempTokens) _tokens.Push(t);
        }

        public Token Next() => _tokens.Count > 0 ? _tokens.Pop() : new Token(TokenType.Eof, "");
        public Token Peek() => _tokens.Count > 0 ? _tokens.Peek() : new Token(TokenType.Eof, "");

    }
    public delegate bool SymbolParser(string identifier, out long value);
    public abstract class Expression {
        public virtual long Solve() { TrySolve(out var res); return res;  }
        public abstract bool TrySolve(out long res); 
        public abstract Expression FoldConstants(SymbolParser parse);
        public Expression FoldConstants() => FoldConstants((string id, out long v) => long.TryParse(id,out v));
        public virtual Expression foldConstants(SymbolParser parse) { return this; }

        public virtual void Enum(Action<string> callback) { }
    }

    public class AtomExpression : Expression {
        public string Value { get; }
        public AtomExpression(string value) => Value = value;
        public override string ToString() => Value;

        public override bool TrySolve(out long res) => long.TryParse(Value, out res);
        public override Expression FoldConstants(SymbolParser parse) => this;
        public override void Enum(Action<string> callback) { callback?.Invoke(Value); }
    }

    public class OperationExpression : Expression {
        public string Operator { get; }
        public Expression Left { get; }
        public Expression Right { get; }

        public bool Mul=> (Operator == "*" || Operator == "/");

        public OperationExpression(string op, Expression left, Expression right) {
            Operator = op;
            Left = left;
            Right = right;
        }
        public override string ToString() => Left + Operator + Right;

        public long solve(long l,long r) => 
            Operator[0] switch {
                    '+' => l + r,
                    '-' => l - r,
                    '*' => l* r,
                    '/' => l / r,
                    _ => throw new InvalidOperationException($"Unknown operator: {Operator}")
                };
        public override bool TrySolve(out long res) {
            if(Left.TrySolve(out var l)&&
               Right.TrySolve(out var r)) {
                res = solve(l, r);
                return true;
            }
            res = 0;
            return false;
        }
        public override Expression foldConstants(SymbolParser parse) {
            // 1. Recursively fold the left and right children first
            Expression foldedLeft = Left.foldConstants(parse);
            Expression foldedRight = Right.foldConstants(parse);

            // 2. Standard direct evaluation (e.g., 4 + 4 -> 8)
            if (foldedLeft is AtomExpression leftAtom && MemoryHandler.TryParse(leftAtom.Value, out var l) &&
                foldedRight is AtomExpression rightAtom && MemoryHandler.TryParse(rightAtom.Value, out var r)) {
                var res = Operator[0] switch {
                    '+' => l + r,
                    '-' => l - r,
                    '*' => l * r,
                    '/' => l / r,
                    _ => throw new InvalidOperationException($"Unknown operator: {Operator}")
                };
                return new AtomExpression(res.ToString());
            }
            // 3. ADVANCED PASS: Re-associate math trees (e.g., (a * 9) * 10)
            // If the left child is ALSO a multiplication node, and its right child is a number:
            if (foldedLeft is OperationExpression leftOp) {
                bool mul = leftOp.Mul;// (leftOp.Operator == "*" || leftOp.Operator == "/");
                if (!mul || (Operator == leftOp.Operator)) {
                    if (leftOp.Right is AtomExpression leftSubRight && MemoryHandler.TryParse(leftSubRight.Value, out var subR) &&
                        foldedRight is AtomExpression rightConst && MemoryHandler.TryParse(rightConst.Value, out var mainR)) {
                        // We have: (LeftSubLeft * subR) * mainR
                        // We can pre-calculate: subR * mainR (9 * 10 = 90)
                        var combinedConstant = solve(subR, mainR);

                        //if (op == "+" && combinedConstant < 0) op = "-";
                        // Return a new cleanly collapsed tree: (LeftSubLeft * 90) -> (a * 90)
                        return new OperationExpression(leftOp.Operator, leftOp.Left, new AtomExpression(combinedConstant.ToString()));
                    }
                }
            }
            // 3. If either side contains a variable (e.g., "a"), we can't fold it.
            // Return a new operation node containing the partially optimized children.
            return new OperationExpression(Operator, foldedLeft, foldedRight);
        }
        private Expression NormalizeSigns(Expression exp) {
            // If it's an atom, we can't normalize its operators, so just return it
            if (exp is not OperationExpression opExp) {
                return exp;
            }

            // 1. Recursively clean both Left and Right branches first (Bottom-Up traversal)
            Expression cleanLeft = NormalizeSigns(opExp.Left);
            Expression cleanRight = NormalizeSigns(opExp.Right);

            string op = opExp.Operator;

            // 2. Look at the right child. If it's a negative numeric atom, adjust the current operator!
            if (op == "-" && cleanRight is AtomExpression rAtom && MemoryHandler.TryParse(rAtom.Value, out var rVal)) {
                
                // Flip the operator
                op = "+";
                // Make the right value positive
                rVal = -rVal;

                cleanRight = new AtomExpression(rVal.ToString());
            }

            // Reconstruct the node with its cleanly processed children
            return new OperationExpression(op, cleanLeft, cleanRight);
        }

        public override Expression FoldConstants(SymbolParser parse) {
            // Step 1: Collapse all constants mathematically
            Expression collapsedTree = foldConstants(parse);

            // Step 2: Clean up structural artifacts like "+ -2" -> "- 2"
            return NormalizeSigns(collapsedTree);
        }
        public override void Enum(Action<string> callback) {
            if (callback == null) return;
            Left.Enum(callback);
            Right.Enum(callback);
        }
    }
    public class EqParser {
        // Define left and right binding power tuples [00:08:55]
        private static (int Left, int Right) GetBindingPower(string op) {
            return op switch {
                "=" => (1, 2),  // Assignment is right-associative [00:15:55]
                "+" or "-" => (3, 4),
                "*" or "/" => (5, 6),
                _ => throw new Exception($"Unknown operator: {op}")
            };
        }

        public static Expression Parse(string source,SymbolParser parse = null) {
            var lexer = new EqLexer(source,parse);
            return ParseExpression(lexer, 0);
        }

        private static Expression ParseExpression(EqLexer lexer, int minBindingPower) {
            Token token = lexer.Next();
            Expression leftHandSide;

            // Handle standard prefix rules and parenthesis [00:14:14]
            if (token.Type == TokenType.LeftParen) {
                leftHandSide = ParseExpression(lexer, 0); // Explicitly pass 0 [00:14:23]
                Token next = lexer.Next();
                if (next.Type != TokenType.RightParen)
                    throw new Exception("Mismatched parenthesis."); // [00:15:06]
            } else if (token.Type == TokenType.Atom) {
                leftHandSide = new AtomExpression(token.Value);
            } else {
                throw new Exception($"Unexpected token: {token.Value}");
            }

            // The Core Pratt Loop [00:12:28]
            while (true) {
                Token peeked = lexer.Peek();
                if (peeked.Type == TokenType.Eof || peeked.Type == TokenType.RightParen)
                    break;

                if (peeked.Type != TokenType.Operator)
                    throw new Exception("Expected operator.");

                var (leftBp, rightBp) = GetBindingPower(peeked.Value);

                // If the next operator binds weaker than our current calculation, break out!
                if (leftBp < minBindingPower)
                    break;

                // Consume the operator
                lexer.Next();

                // Recursively parse the right-hand side using the operator's right binding power
                Expression rightHandSide = ParseExpression(lexer, rightBp);
                leftHandSide = new OperationExpression(peeked.Value, leftHandSide, rightHandSide);
            }

            return leftHandSide;
        }
    }
}
