using System;
using System.Collections.Generic;


namespace PktrScript
{
    public class Node
    {
        public Span span;
        public NodeKind kind;
        public List<Node> children = new List<Node>();
        public List<Token> tokens = new List<Token>();


        public Node(NodeKind kind)
        {
            this.kind = kind;
        }


        public void AddSpan(Span span)
        {
            if (this.span == null)
                this.span = span;
            else
                this.span += span;
        }


        public void AddChildrenSpansRecursively()
        {
            foreach (var child in this.children)
            {
                child.AddChildrenSpansRecursively();
                this.AddSpan(child.span);
            }
        }


        public void PrintDebug(int indent = 0)
        {
            Console.Write(new string(' ', indent * 3));
            Console.Write(Enum.GetName(typeof(NodeKind), this.kind));

            foreach (var token in this.tokens)
                Console.Write(" `" + token.excerpt + "`");

            Console.WriteLine();

            foreach (var c in this.children)
                c.PrintDebug(indent + 1);
        }
    }


    public enum NodeKind
    {
        TopLevel,
        Include,
        FunctionDef,
        FunctionDefAnon,
        Parenthesized,
        Block,
        Identifier,
        Number,
        BoolTrue,
        BoolFalse,
        String,
        Var,
        If,
        While,
        Loop,
        Continue,
        Break,
        Return,
        Call,
        LiteralObject,
        LiteralObjectField,
        FieldAccess,
        MethodCall,
        BinaryAssign,
        BinaryPlus,
        BinaryMinus,
        BinaryMultiply,
        BinaryDivide,
        BinaryEqual,
        BinaryNotEqual,
        BinaryLessThan,
        BinaryLessThanEqual,
        BinaryGreaterThan,
        BinaryGreaterThanEqual,
        BinaryAnd,
        BinaryOr,
        BinaryConditionalAnd,
        BinaryConditionalOr,
        UnaryNot,
        UnaryNegation
    }


    public class Parser
    {
        public static Node Parse(List<Token> tokens)
        {
            var parser = new Parser
            {
                tokens = tokens,
                index = 0,
                indexPrevious = 0
            };

            parser.SkipIgnorable();

            var node = parser.ParseTopLevel();
            node.AddChildrenSpansRecursively();
            return node;
        }


        private class ParseException : System.Exception
        {

        }


        private List<Token> tokens;
        private int index, indexPrevious;
        private bool parsedLineBreak = false;


        private Parser()
        {

        }


        private bool IsOver
        {
            get { return this.index >= this.tokens.Count; }
        }


        private void Advance()
        {
            this.parsedLineBreak = false;
            this.indexPrevious = this.index;
            this.index++;
            this.SkipIgnorable();
        }


        private void SkipIgnorable()
        {
            while (this.NextNthIs(0, TokenKind.Error) ||
                this.NextNthIs(0, TokenKind.Whitespace) ||
                this.NextNthIs(0, TokenKind.Comment) ||
                this.NextNthIs(0, TokenKind.LineBreak))
            {
                if (this.NextNthIs(0, TokenKind.LineBreak))
                    this.parsedLineBreak = true;

                this.index++;
            }
        }


        private Token Current
        {
            get { return this.tokens[this.index]; }
        }


        private Token Previous
        {
            get { return this.tokens[this.indexPrevious]; }
        }


        private Span SpanBeforeCurrent
        {
            get
            {
                if (this.index >= this.tokens.Count)
                    return this.tokens[this.tokens.Count - 1].span.JustAfter;
                else
                    return this.tokens[this.index].span.JustBefore;
            }
        }


        private void ErrorBeforeCurrent(string descr)
        {
            Runtime.RaiseCompileError(descr, this.SpanBeforeCurrent);
        }


        private bool NextNthIs(int n, TokenKind kind)
        {
            if (this.index + n >= this.tokens.Count)
                return false;

            return this.tokens[this.index + n].kind == kind;
        }


        private Token Expect(TokenKind kind)
        {
            if (this.NextNthIs(0, kind))
            {
                var token = this.tokens[this.index];
                this.Advance();
                return token;
            }
            else
            {
                this.ErrorBeforeCurrent("expected " + Token.PrintableKind(kind));
                throw new ParseException();
            }
        }


        private Token ExpectMaybe(TokenKind kind)
        {
            if (this.NextNthIs(0, kind))
            {
                var token = this.tokens[this.index];
                this.Advance();
                return token;
            }
            else
            {
                return null;
            }
        }


        private List<Node> ParseList(TokenKind separator, TokenKind terminator, System.Func<Node> parseItemFunc)
        {
            var list = new List<Node>();

            while (!this.IsOver && this.Current.kind != terminator)
            {
                list.Add(parseItemFunc());
                if (this.Current.kind != terminator)
                    this.Expect(separator);
            }

            this.Expect(terminator);
            return list;
        }


        private class Op
        {
            public TokenKind tokenKind;
            public NodeKind nodeKind;


            public Op(TokenKind tokenKind, NodeKind nodeKind)
            {
                this.tokenKind = tokenKind;
                this.nodeKind = nodeKind;
            }
        }


        private Node ParseUnaryOp(System.Func<Node> parseInner, params Op[] unops)
        {
            var unopMatch = (Op)null;
            foreach (var unop in unops)
                if (this.ExpectMaybe(unop.tokenKind) != null)
                {
                    unopMatch = unop;
                    break;
                }

            var inner = parseInner();

            if (unopMatch != null)
            {
                var node = new Node(unopMatch.nodeKind);
                node.children.Add(inner);
                inner = node;
            }

            return inner;
        }


        private Node ParseBinaryOp(bool rightAssoc, System.Func<Node> parseInner, params Op[] binops)
        {
            var lhs = parseInner();

            while (true)
            {
                var binopMatch = (Op)null;
                foreach (var binop in binops)
                    if (this.ExpectMaybe(binop.tokenKind) != null)
                    {
                        binopMatch = binop;
                        break;
                    }

                if (binopMatch == null)
                    break;

                var node = new Node(binopMatch.nodeKind);
                node.children.Add(lhs);
                node.children.Add(parseInner());
                lhs = node;

                if (rightAssoc)
                    break;
            }

            return lhs;
        }


        private Node ParseTopLevel()
        {
            var node = new Node(NodeKind.TopLevel);
            
            while (!this.IsOver)
            {
                node.children.Add(this.ParseExpr());

                if (this.IsOver)
                    continue;

                if (this.ExpectMaybe(TokenKind.Semicolon) != null)
                    continue;

                if (this.parsedLineBreak)
                    continue;

                if (!this.NextNthIs(0, TokenKind.BraceClose))
                    this.ErrorBeforeCurrent("expected semicolon or line break");
            }

            return node;
        }


        private Node ParseExpr()
        {
            return this.ParseBinaryAssign();
        }


        private Node ParseBinaryAssign()
        {
            return this.ParseBinaryOp(true, ParseBinaryConditionalTerm,
                new Op(TokenKind.Equal, NodeKind.BinaryAssign));
        }


        private Node ParseBinaryConditionalTerm()
        {
            return this.ParseBinaryOp(false, ParseBinaryRelationalTerm,
                new Op(TokenKind.Ampersand, NodeKind.BinaryAnd),
                new Op(TokenKind.AmpersandAmpersand, NodeKind.BinaryConditionalAnd),
                new Op(TokenKind.VerticalBar, NodeKind.BinaryOr),
                new Op(TokenKind.VerticalBarVerticalBar, NodeKind.BinaryConditionalOr));
        }


        private Node ParseBinaryRelationalTerm()
        {
            return this.ParseBinaryOp(false, ParseBinaryAdditionTerm,
                new Op(TokenKind.EqualEqual, NodeKind.BinaryEqual),
                new Op(TokenKind.ExclamationMarkEqual, NodeKind.BinaryNotEqual),
                new Op(TokenKind.LessThan, NodeKind.BinaryLessThan),
                new Op(TokenKind.LessThanEqual, NodeKind.BinaryLessThanEqual),
                new Op(TokenKind.GreaterThan, NodeKind.BinaryGreaterThan),
                new Op(TokenKind.GreaterThanEqual, NodeKind.BinaryGreaterThanEqual));
        }


        private Node ParseBinaryAdditionTerm()
        {
            return this.ParseBinaryOp(false, ParseBinaryMultiplicationTerm,
                new Op(TokenKind.Plus, NodeKind.BinaryPlus),
                new Op(TokenKind.Minus, NodeKind.BinaryMinus));
        }


        private Node ParseBinaryMultiplicationTerm()
        {
            return this.ParseBinaryOp(false, ParseUnaryOp,
                new Op(TokenKind.Asterisk, NodeKind.BinaryMultiply),
                new Op(TokenKind.Slash, NodeKind.BinaryDivide));
        }


        private Node ParseUnaryOp()
        {
            return this.ParseUnaryOp(ParseCallOrFieldAccess,
                new Op(TokenKind.ExclamationMark, NodeKind.UnaryNot),
                new Op(TokenKind.Minus, NodeKind.UnaryNegation));
        }


        private Node ParseCallOrFieldAccess()
        {
            var lhs = this.ParseExprLeaf();

            while (true)
            {
                if (this.ExpectMaybe(TokenKind.Dot) != null)
                {
                    var node = new Node(NodeKind.FieldAccess);
                    node.children.Add(lhs);
                    node.children.Add(this.ParseExprLeaf());
                    lhs = node;
                }

                else if (this.ExpectMaybe(TokenKind.ParenOpen) != null)
                {
                    if (lhs.kind == NodeKind.FieldAccess)
                    {
                        lhs.kind = NodeKind.MethodCall;
                        lhs.children.AddRange(this.ParseList(TokenKind.Comma, TokenKind.ParenClose, this.ParseExpr));
                    }
                    else
                    {
                        var node = new Node(NodeKind.Call);
                        node.children.Add(lhs);
                        node.children.AddRange(this.ParseList(TokenKind.Comma, TokenKind.ParenClose, this.ParseExpr));
                        lhs = node;
                    }
                }

                else
                    break;
            }

            return lhs;
        }


        private Node ParseBinaryFieldAccess()
        {
            return this.ParseBinaryOp(false, ParseExprLeaf,
                new Op(TokenKind.Dot, NodeKind.FieldAccess));
        }


        private Node ParseExprLeaf()
        {
            switch (this.Current.kind)
            {
                case TokenKind.KeywordInclude:
                    return this.ParseInclude();

                case TokenKind.KeywordFunction:
                    return this.ParseFunctionDef();

                case TokenKind.BraceOpen:
                    return this.ParseBlock();

                case TokenKind.ParenOpen:
                    return this.ParseParenthesized();

                case TokenKind.KeywordVar:
                    return this.ParseVar();

                case TokenKind.KeywordIf:
                    return this.ParseIf();

                case TokenKind.KeywordWhile:
                    return this.ParseWhile();

                case TokenKind.KeywordLoop:
                    return this.ParseLoop();

                case TokenKind.KeywordBreak:
                    return this.ParseBreak();

                case TokenKind.KeywordContinue:
                    return this.ParseContinue();

                case TokenKind.KeywordReturn:
                    return this.ParseReturn();

                case TokenKind.KeywordNew:
                    return this.ParseLiteralObject();

                case TokenKind.Identifier:
                    return this.ParseIdentifier();

                case TokenKind.Number:
                    return this.ParseNumber();

                case TokenKind.KeywordTrue:
                case TokenKind.KeywordFalse:
                    return this.ParseBool();

                case TokenKind.String:
                    return this.ParseString();

                default:
                    this.ErrorBeforeCurrent("expected expression");
                    throw new ParseException();
            }
        }


        private Node ParseIdentifier()
        {
            var node = new Node(NodeKind.Identifier);
            node.tokens.Add(this.Expect(TokenKind.Identifier));
            node.AddSpan(node.tokens[0].span);
            return node;
        }


        private Node ParseNumber()
        {
            var node = new Node(NodeKind.Number);
            node.tokens.Add(this.Expect(TokenKind.Number));
            node.AddSpan(node.tokens[0].span);
            return node;
        }


        private Node ParseBool()
        {
            var node = new Node(NodeKind.BoolTrue);
            if (this.ExpectMaybe(TokenKind.KeywordTrue) != null)
                node.AddSpan(this.Previous.span);
            else
            {
                node.AddSpan(this.Expect(TokenKind.KeywordFalse).span);
                node.kind = NodeKind.BoolFalse;
            }

            return node;
        }


        private Node ParseString()
        {
            var node = new Node(NodeKind.String);
            var token = this.Expect(TokenKind.String);

            while (true)
            {
                node.tokens.Add(token);
                node.AddSpan(token.span);

                token = this.ExpectMaybe(TokenKind.String);
                if (token == null)
                    break;
            }

            return node;
        }


        private Node ParseInclude()
        {
            var node = new Node(NodeKind.Include);
            node.AddSpan(this.Expect(TokenKind.KeywordInclude).span);
            node.tokens.Add(this.Expect(TokenKind.String));

            if (!this.parsedLineBreak)
                this.ErrorBeforeCurrent("expected line break");

            return node;
        }


        private Node ParseFunctionDef()
        {
            var node = new Node(NodeKind.FunctionDefAnon);
            node.AddSpan(this.Expect(TokenKind.KeywordFunction).span);

            if (this.NextNthIs(0, TokenKind.Identifier))
            {
                node.kind = NodeKind.FunctionDef;
                node.children.Add(this.ParseIdentifier());
            }

            if (this.ExpectMaybe(TokenKind.Colon) == null)
            {
                this.Expect(TokenKind.ParenOpen);
                node.children.AddRange(this.ParseList(TokenKind.Comma, TokenKind.ParenClose, this.ParseIdentifier));
            }

            node.children.Add(this.ParseExpr());

            return node;
        }


        private Node ParseBlock()
        {
            var node = new Node(NodeKind.Block);
            node.AddSpan(this.Expect(TokenKind.BraceOpen).span);

            while (!this.IsOver && this.Current.kind != TokenKind.BraceClose)
            {
                node.children.Add(this.ParseExpr());

                if (this.ExpectMaybe(TokenKind.Semicolon) != null)
                    continue;

                if (this.parsedLineBreak)
                    continue;

                if (!this.NextNthIs(0, TokenKind.BraceClose))
                    this.ErrorBeforeCurrent("expected semicolon or line break");
            }

            node.AddSpan(this.Expect(TokenKind.BraceClose).span);
            return node;
        }


        private Node ParseParenthesized()
        {
            var node = new Node(NodeKind.Parenthesized);
            node.AddSpan(this.Expect(TokenKind.ParenOpen).span);
            node.children.Add(this.ParseExpr());
            node.AddSpan(this.Expect(TokenKind.ParenClose).span);
            return node;
        }


        private Node ParseLiteralObject()
        {
            var node = new Node(NodeKind.LiteralObject);
            node.AddSpan(this.Expect(TokenKind.KeywordNew).span);
            node.AddSpan(this.Expect(TokenKind.BraceOpen).span);
            node.children.AddRange(this.ParseList(TokenKind.Comma, TokenKind.BraceClose, this.ParseLiteralObjectField));
            node.AddSpan(this.Previous.span);
            return node;
        }


        private Node ParseLiteralObjectField()
        {
            var node = new Node(NodeKind.LiteralObjectField);
            node.children.Add(this.ParseIdentifier());
            this.Expect(TokenKind.Equal);
            node.children.Add(this.ParseExpr());
            return node;
        }


        private Node ParseVar()
        {
            var node = new Node(NodeKind.Var);
            node.AddSpan(this.Expect(TokenKind.KeywordVar).span);
            node.children.Add(this.ParseIdentifier());

            if (this.ExpectMaybe(TokenKind.Equal) != null)
                node.children.Add(this.ParseExpr());

            return node;
        }


        private Node ParseIf()
        {
            var node = new Node(NodeKind.If);
            node.AddSpan(this.Expect(TokenKind.KeywordIf).span);
            node.children.Add(this.ParseExpr());
            node.children.Add(this.ParseBlock());

            if (this.ExpectMaybe(TokenKind.KeywordElse) != null)
                node.children.Add(this.ParseBlock());

            return node;
        }


        private Node ParseWhile()
        {
            var node = new Node(NodeKind.While);
            node.AddSpan(this.Expect(TokenKind.KeywordWhile).span);
            node.children.Add(this.ParseExpr());
            node.children.Add(this.ParseBlock());
            return node;
        }


        private Node ParseLoop()
        {
            var node = new Node(NodeKind.Loop);
            node.AddSpan(this.Expect(TokenKind.KeywordLoop).span);
            node.children.Add(this.ParseBlock());
            return node;
        }


        private Node ParseBreak()
        {
            var node = new Node(NodeKind.Break);
            node.AddSpan(this.Expect(TokenKind.KeywordBreak).span);
            return node;
        }


        private Node ParseContinue()
        {
            var node = new Node(NodeKind.Continue);
            node.AddSpan(this.Expect(TokenKind.KeywordContinue).span);
            return node;
        }


        private Node ParseReturn()
        {
            var node = new Node(NodeKind.Return);
            node.AddSpan(this.Expect(TokenKind.KeywordReturn).span);

            if (this.Current.kind != TokenKind.BraceClose &&
                this.Current.kind != TokenKind.ParenClose &&
                this.Current.kind != TokenKind.Comma &&
                this.Current.kind != TokenKind.Semicolon)
                node.children.Add(this.ParseExpr());

            return node;
        }
    }
}
