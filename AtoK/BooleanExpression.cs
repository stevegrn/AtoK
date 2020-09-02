using System;
using System.Security.Principal;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Globalization;

namespace ConvertToKicad
{
    public partial class ConvertPCBDoc
    {
        /// <summary>
        /// [CustomAuthorize(Roles = "A && (!B || C) ^ D")]
        /// </summary>
        public class Parser //CustomAuthorizeAttribute //: AuthorizeAttribute
        {
            /*
            *  Exp -> SubExp '&&' Exp // AND
            *  Exp -> SubExp '||' Exp // OR
            *  Exp -> SubExp '^' Exp  // XOR
            *  SubExp -> '(' Exp ')'
            *  SubExp -> '!' Exp         // NOT
            *  SubExp -> RoleName
            *  RoleName -> [a-z0-9]
            */

            public abstract class Node
            {
                public abstract bool Eval(PCBObject Obj);
                public virtual double EvalExpr(PCBObject Obj)
                {
                    return 0;
                }
            }

            public abstract class UnaryNode : Node
            {
                private readonly Node _expression;

                public Node Expression => _expression;

                protected UnaryNode(Node expression)
                {
                    _expression = expression;
                }
            }

            abstract class BinaryNode : Node
            {
                private readonly Node _leftExpression;
                private readonly Node _rightExpression;

                public Node LeftExpression  => _leftExpression;
                public Node RightExpression => _rightExpression;

                protected BinaryNode(Node leftExpression, Node rightExpression)
                {
                    _leftExpression  = leftExpression;
                    _rightExpression = rightExpression;
                }
            }

            class AndNode : BinaryNode
            {
                public AndNode(Node leftExpression, Node rightExpression)
                    : base(leftExpression, rightExpression)
                {
                }

                public override bool Eval(PCBObject Obj)
                {
                    if (LeftExpression.Eval(Obj) == false)
                        return false;
                    return RightExpression.Eval(Obj);
                }

                public override string ToString()
                {
                    var sb = new StringBuilder("");
                    sb.Append("(");
                    sb.Append(LeftExpression.ToString());
                    sb.Append(" && ");
                    sb.Append(RightExpression.ToString());
                    sb.Append(")");
                    return sb.ToString();
                }
            }

            class OrNode : BinaryNode
            {
                public OrNode(Node leftExpression, Node rightExpression)
                    : base(leftExpression, rightExpression)
                {
                }

                public override bool Eval(PCBObject Obj)
                {
                    if (LeftExpression.Eval(Obj) == true)
                        return true;
                    return RightExpression.Eval(Obj);
                }

                public override string ToString()
                {
                    var sb = new StringBuilder("");
                    sb.Append("(");
                    sb.Append(LeftExpression.ToString());
                    sb.Append(" || ");
                    sb.Append(RightExpression.ToString());
                    sb.Append(")");
                    return sb.ToString();
                }
            }

            class XorNode : BinaryNode
            {
                public XorNode(Node leftExpression, Node rightExpression)
                    : base(leftExpression, rightExpression)
                {
                }

                public override bool Eval(PCBObject Obj)
                {
                    return LeftExpression.Eval(Obj) ^ RightExpression.Eval(Obj);
                }

                public override string ToString()
                {
                    var sb = new StringBuilder("");
                    sb.Append("(");
                    sb.Append(LeftExpression.ToString());
                    sb.Append(" ^ ");
                    sb.Append(RightExpression.ToString());
                    sb.Append(")");
                    return sb.ToString();
                }
            }

            public class NotNode : UnaryNode
            {
                public NotNode(Node expression)
                    : base(expression)
                {
                }

                public override bool Eval(PCBObject Obj)
                {
                    return !Expression.Eval(Obj);
                }

                public override string ToString()
                {
                    var sb = new StringBuilder("");
                    sb.Append("(");
                    sb.Append(" ! ");
                    sb.Append(Expression.ToString());
                    sb.Append(")");
                    return sb.ToString();
                }
            }

            class LessThanNode : BinaryNode
            {
                    public LessThanNode(Node leftExpression, Node rightExpression)
                        : base(leftExpression, rightExpression)
                    {
                    }

                    public override bool Eval(PCBObject Obj)
                    {
                        return LeftExpression.EvalExpr(Obj) < RightExpression.EvalExpr(Obj);
                    }

                    public override string ToString()
                    {
                        var sb = new StringBuilder("");
                        sb.Append("(");
                        sb.Append(LeftExpression.ToString());
                        sb.Append(" < ");
                        sb.Append(RightExpression.ToString());
                        sb.Append(")");
                        return sb.ToString();
                    }
            }

            class LessThanOrEqualsNode : BinaryNode
            {
                public LessThanOrEqualsNode(Node leftExpression, Node rightExpression)
                    : base(leftExpression, rightExpression)
                {
                }

                public override bool Eval(PCBObject Obj)
                {
                    return LeftExpression.EvalExpr(Obj) <= RightExpression.EvalExpr(Obj);
                }

                public override string ToString()
                {
                    var sb = new StringBuilder("");
                    sb.Append("(");
                    sb.Append(LeftExpression.ToString());
                    sb.Append(" <= ");
                    sb.Append(RightExpression.ToString());
                    sb.Append(")");
                    return sb.ToString();
                }
            }

            class GreaterThanOrEqualsNode : BinaryNode
            {
                public GreaterThanOrEqualsNode(Node leftExpression, Node rightExpression)
                    : base(leftExpression, rightExpression)
                {
                }

                public override bool Eval(PCBObject Obj)
                {
                    return LeftExpression.EvalExpr(Obj) >= RightExpression.EvalExpr(Obj);
                }

                public override string ToString()
                {
                    var sb = new StringBuilder("");
                    sb.Append("(");
                    sb.Append(LeftExpression.ToString());
                    sb.Append(" >= ");
                    sb.Append(RightExpression.ToString());
                    sb.Append(")");
                    return sb.ToString();
                }
            }

            class GreaterThanNode : BinaryNode
            {
                public GreaterThanNode(Node leftExpression, Node rightExpression)
                    : base(leftExpression, rightExpression)
                {
                }

                public override bool Eval(PCBObject Obj)
                {
                    return LeftExpression.EvalExpr(Obj) > RightExpression.EvalExpr(Obj);
                }

                public override string ToString()
                {
                    var sb = new StringBuilder("");
                    sb.Append("(");
                    sb.Append(LeftExpression.ToString());
                    sb.Append(" > ");
                    sb.Append(RightExpression.ToString());
                    sb.Append(")");
                    return sb.ToString();
                }
            }

            class NotEqualsNode : BinaryNode
            {
                public NotEqualsNode(Node leftExpression, Node rightExpression)
                    : base(leftExpression, rightExpression)
                {
                }

                public override bool Eval(PCBObject Obj)
                {
                    return LeftExpression.EvalExpr(Obj) != RightExpression.EvalExpr(Obj);
                }

                public override double EvalExpr(PCBObject Obj)
                {
                    return 0; // LeftExpression.EvalExpr(Obj) != RightExpression.EvalExpr(Obj);
                }

                public override string ToString()
                {
                    var sb = new StringBuilder("");
                    sb.Append("(");
                    sb.Append(LeftExpression.ToString());
                    sb.Append(" <> ");
                    sb.Append(RightExpression.ToString());
                    sb.Append(")");
                    return sb.ToString();
                }
            }

            class EqualsNode : BinaryNode
            {
                public EqualsNode(Node leftExpression, Node rightExpression)
                    : base(leftExpression, rightExpression)
                {
                }

                public override bool Eval(PCBObject Obj)
                {
                    return LeftExpression.EvalExpr(Obj) == RightExpression.EvalExpr(Obj);
                }

                public override string ToString()
                {
                    var sb = new StringBuilder("");
                    sb.Append("(");
                    sb.Append(LeftExpression.ToString());
                    sb.Append(" = ");
                    sb.Append(RightExpression.ToString());
                    sb.Append(")");
                    return sb.ToString();
                }
            }

            class RoleNode : Node
            {
                private readonly string _roleName;
                private readonly string _roleParam;
                Func<bool> Func;

                public string RoleName
                {
                    get { return _roleName; }
                }

                public RoleNode(string roleName)
                {
                    // split rolename into function name and parameter
                    string[] words = roleName.Split('(');
                    _roleName = words[0];
                    if (words.Length == 2)
                    {
                        _roleParam = words[1].Substring(0, words[1].Length - 1);
                        _roleParam = _roleParam.Substring(1, _roleParam.Length - 2);
                    }
                    else
                        _roleParam = "";
                }

                public override bool Eval(PCBObject Obj)
                {
                    switch (_roleName)
                    {
                        case "All" :                return Obj.All();
                        case "IsArc" :              return Obj.IsArc();
                        case "IsClass":             return Obj.IsClass();
                        case "IsComponentArc":      return Obj.IsComponentArc();
                        case "IsComponentFillc":    return Obj.IsComponentFill();
                        case "IsComponentPad":      return Obj.IsComponentPad();
                        case "IsComponentTrack":    return Obj.IsComponentTrack();
                        case "IsConnection":        return Obj.IsConnection();
                        case "IsCoordinate":        return Obj.IsCoordinate();
                        case "IsCopperRegion":      return Obj.IsCopperRegion();
                        case "IsCutoutRegion":      return Obj.IsCutoutRegion();
                        case "IsDatumDimension":    return Obj.IsDatumDimension();
                        case "IsDesignator":        return Obj.IsDesignator();
                        case "IsComment":           return Obj.IsComment();
                        case "IsFill":              return Obj.IsFill();
                        case "IsPad":               return Obj.IsPad();
                        case "IsPoly":              return Obj.IsPoly();
                        case "IsPolygon":           return Obj.IsPolygon();
                        case "IsRegion":            return Obj.IsRegion();
                        case "IsSplitPlane":        return Obj.IsSplitPlane();
                        case "IsText":              return Obj.IsText();
                        case "IsTrack":             return Obj.IsTrack();
                        case "IsVia":               return Obj.IsVia();
                        case "InComponent":         return Obj.InComponent(_roleParam);
                    }
                    return false; // principal.IsInRole(RoleName);
                }

                public override string ToString()
                {
                    var sb = new StringBuilder("");
                    sb.Append("(");
                    sb.Append(RoleName);
                    sb.Append(")");
                    return sb.ToString();
                }
            }

            static string Reduce(string token)
            {
                if(string.Compare(token, "AND", StringComparison.OrdinalIgnoreCase)==0)
                    return "And";
                else
                if (string.Compare(token, "OR", StringComparison.OrdinalIgnoreCase) == 0)
                    return "Or";
                if (string.Compare(token, "NOT", StringComparison.OrdinalIgnoreCase) == 0)
                    return "Not";
                if (string.Compare(token, "XOR", StringComparison.OrdinalIgnoreCase) == 0)
                    return "Xor";
                return token;
            }

            static Queue<string> Tokenise(string text)
            {
                var Tokens = new Queue<string>();

                var token = new StringBuilder("");
                bool InToken = false;
                int bc = 0;
                for (var i  = 0; i < text.Length; i++)
                {
                    char c = text[i];
                    switch (c)
                    {
                        case '<':
                            if(InToken)
                            {
                                Tokens.Enqueue(token.ToString());
                                token.Clear();
                                InToken = false;
                            }
                            if(text[i+1]=='>')
                            {
                                i++;
                                Tokens.Enqueue("<>");
                            }
                            else
                            if(text[i+1] == '=')
                            {
                                i++;
                                Tokens.Enqueue("<=");
                            }
                            else
                            {
                                Tokens.Enqueue("<");
                            }
                            break;
                        case '>':
                            if (InToken)
                            {
                                Tokens.Enqueue(token.ToString());
                                token.Clear();
                                InToken = false;
                            }
                            if (text[i + 1] == '=')
                            {
                                i++;
                                Tokens.Enqueue(">=");
                            }
                            else
                                Tokens.Enqueue(">");
                            break;
                        case '=':
                            if (InToken)
                            {
                                Tokens.Enqueue(token.ToString());
                                token.Clear();
                                InToken = false;
                            }
                            Tokens.Enqueue("=");
                            break;
                        case ' ':
                            if (InToken)
                            {
                                Tokens.Enqueue(Reduce(token.ToString()));
                                token.Clear();
                                InToken = false;
                            }
                            break;

                        case '(':
                            if (!InToken)
                                Tokens.Enqueue("(");
                            else
                            {
                                token.Append('(');
                                bc++;
                            }
                            break;

                        case ')':
                            if (!InToken)
                                Tokens.Enqueue(")");
                            else
                            {
                                if (bc == 0)
                                {
                                    Tokens.Enqueue(Reduce(token.ToString()));
                                    Tokens.Enqueue(")");
                                    InToken = false;
                                    token.Clear();
                                }
                                else
                                {
                                    token.Append(')');
                                    bc--;
                                }
                            }
                            break;

                        default:
                            token.Append(c);
                            InToken = true;
                            break;
                    }
                }

                if (token.ToString() != "")
                    Tokens.Enqueue(Reduce(token.ToString()));

                return Tokens;
            }

            static Node Parse(string text)
            {
                Queue<string> tokens = Tokenise(text);

                return ParseExp(ref tokens);
            }

            static Node ParseExp(ref Queue<string> tokens)
            {
                Node leftExp = ParseSubExp(ref tokens);
                if (tokens.Count == 0)
                    return leftExp;
                if (tokens.Peek() == ")")
                    return leftExp;

                string token = tokens.Dequeue();

                if (token == "And")
                {
                    Node rightExp = ParseExp(ref tokens);
                    return new AndNode(leftExp, rightExp);
                }
                else if (token == "Or")
                {
                    Node rightExp = ParseExp(ref tokens);
                    return new OrNode(leftExp, rightExp);
                }
                else if (token == "Xor")
                {
                    Node rightExp = ParseExp(ref tokens);
                    return new XorNode(leftExp, rightExp);
                }
                else if (token == "<")
                {
                    Node rightExp = ParseExp(ref tokens);
                    return new LessThanNode(leftExp, rightExp);
                }
                else if (token == "<=")
                {
                    Node rightExp = ParseExp(ref tokens);
                    return new LessThanOrEqualsNode(leftExp, rightExp);
                }
                else if (token == "<>")
                {
                    Node rightExp = ParseExp(ref tokens);
                    return new NotEqualsNode(leftExp, rightExp);
                }
                else if (token == ">")
                {
                    Node rightExp = ParseExp(ref tokens);
                    return new GreaterThanNode(leftExp, rightExp);
                }
                else if (token == ">=")
                {
                    Node rightExp = ParseExp(ref tokens);
                    return new GreaterThanOrEqualsNode(leftExp, rightExp);
                }
                else if (token == "=")
                {
                    Node rightExp = ParseExp(ref tokens);
                    return new EqualsNode(leftExp, rightExp);
                }
                else
                {
                    //OutputString($"Got {token} Expected 'And' or 'Or' or 'XOR' or EOF");
                    return null;
                }
            }

            static Node ParseSubExp(ref Queue<string> tokens)
            {
                string token = tokens.Dequeue();

                if (token == "(")
                {
                    Node node = ParseExp(ref tokens);

                    token = tokens.Dequeue();
                    //if (token != ")")
                    //    OutputString("Expected ')'");

                    return node;
                }
                else if (token == "Not")
                {
                    Node node = ParseExp(ref tokens);
                    return new NotNode(node);
                }
                else
                {
                    return new RoleNode(token);
                }
            }

            public Node ParseAltiumExpr(string Expression)
            {
                Node _expression = Parse(Expression);

                return _expression;
            }

            public bool Eval(PCBObject Obj, Node Expr)
            {
                return Expr.Eval(Obj);
            }
        }
    }
}