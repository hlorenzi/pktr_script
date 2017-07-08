using System;
using System.Collections.Generic;


namespace PktrScript
{
    public class Token
    {
        public Span span;
        public TokenKind kind;
        public string excerpt;


        public void PrintDebug()
        {
            Console.Write(Enum.GetName(typeof(TokenKind), this.kind));

            if (this.kind != TokenKind.Whitespace && this.kind != TokenKind.LineBreak)
                Console.Write(" `" + this.excerpt + "`");

            Console.WriteLine();
        }


        public static string PrintableKind(TokenKind kind)
        {
            switch (kind)
            {
                case TokenKind.Error: return "error";
                case TokenKind.Whitespace: return "whitespace";
                case TokenKind.Comment: return "comment";
                case TokenKind.Identifier: return "identifier";
                case TokenKind.Number: return "number";
                case TokenKind.KeywordInclude: return "`include`";
                case TokenKind.KeywordFunction: return "`function`";
                case TokenKind.KeywordVar: return "`var`";
                case TokenKind.KeywordNew: return "`new`";
                case TokenKind.KeywordIf: return "`if`";
                case TokenKind.KeywordElse: return "`else`";
                case TokenKind.KeywordWhile: return "`while`";
                case TokenKind.KeywordLoop: return "`loop`";
                case TokenKind.KeywordBreak: return "`break`";
                case TokenKind.KeywordContinue: return "`continue`";
                case TokenKind.KeywordReturn: return "`return`";
                case TokenKind.KeywordTrue: return "`true`";
                case TokenKind.KeywordFalse: return "`false`";
                case TokenKind.BraceOpen: return "`{`";
                case TokenKind.BraceClose: return "`}`";
                case TokenKind.BracketOpen: return "`[`";
                case TokenKind.BracketClose: return "`]`";
                case TokenKind.ParenOpen: return "`(`";
                case TokenKind.ParenClose: return "`)`";
                case TokenKind.Comma: return "`,`";
                case TokenKind.Dot: return "`.`";
                case TokenKind.Colon: return "`:`";
                case TokenKind.Semicolon: return "`;`";
                case TokenKind.Arrow: return "`->`";
                case TokenKind.EqualEqual: return "`==`";
                case TokenKind.Equal: return "`=`";
                case TokenKind.LessThanEqual: return "`<=`";
                case TokenKind.LessThan: return "`<`";
                case TokenKind.GreaterThanEqual: return "`>=`";
                case TokenKind.GreaterThan: return "`>`";
                case TokenKind.Plus: return "`+`";
                case TokenKind.Minus: return "`-`";
                case TokenKind.Asterisk: return "`*`";
                case TokenKind.Slash: return "`/`";
                case TokenKind.ExclamationMarkEqual: return "`!=`";
                case TokenKind.ExclamationMark: return "`!`";
                case TokenKind.AmpersandAmpersand: return "`&&`";
                case TokenKind.Ampersand: return "`&`";
                case TokenKind.VerticalBarVerticalBar: return "`||`";
                case TokenKind.VerticalBar: return "`|`";
                default: return "unknown";
            }
        }
    }


    public enum TokenKind
    {
        Error,
        Whitespace,
        Comment,
        LineBreak,
        Identifier,
        Number,
        String,
        KeywordInclude,
        KeywordFunction,
        KeywordVar,
        KeywordNew,
        KeywordIf,
        KeywordElse,
        KeywordWhile,
        KeywordLoop,
        KeywordContinue,
        KeywordBreak,
        KeywordReturn,
        KeywordTrue,
        KeywordFalse,
        BraceOpen,
        BraceClose,
        BracketOpen,
        BracketClose,
        ParenOpen,
        ParenClose,
        Comma,
        Dot,
        Colon,
        Semicolon,
        Arrow,
        EqualEqual,
        Equal,
        LessThanEqual,
        LessThan,
        GreaterThanEqual,
        GreaterThan,
        Plus,
        Minus,
        Asterisk,
        Slash,
        ExclamationMarkEqual,
        ExclamationMark,
        Ampersand,
        AmpersandAmpersand,
        VerticalBar,
        VerticalBarVerticalBar,
    }


    public static class Tokenizer
    {
        public static List<Token> Tokenize(string filename, string src)
        {
            var index = 0;
            var tokens = new List<Token>();
            var canAddLineBreak = false;
            var line = 0;
            var column = 0;

            while (index < src.Length)
            {
                var match =
                    TryMatchSingleLineComment(src, index) ??
                    TryMatchMultiLineComment(src, index) ??
                    TryMatchFixed(src, index) ??
                    TryMatchFilter(TokenKind.Whitespace, src, index, IsWhitespace, IsWhitespace) ??
                    TryMatchFilter(TokenKind.Identifier, src, index, IsIdentifierPrefix, IsIdentifier) ??
                    TryMatchFilter(TokenKind.Number, src, index, IsNumberPrefix, IsNumber) ??
                    TryMatchString(src, index) ??
                    new Match(src[index].ToString(), TokenKind.Error);

                var span = new Span(filename, index, index + match.excerpt.Length, line, column);

                // Advance line/column counter.
                for (var i = 0; i < span.Length; i++)
                {
                    if (src[index + i] == '\n')
                    {
                        line++;
                        column = 0;
                    }
                    else
                        column++;
                }

                index = span.end;

                if (match.kind == TokenKind.Error)
                    Runtime.RaiseCompileError("unexpected character", span);

                var token = new Token
                {
                    span = span,
                    kind = match.kind,
                    excerpt = match.excerpt
                };

                if (match.kind == TokenKind.LineBreak)
                {
                    if (!canAddLineBreak)
                        token.kind = TokenKind.Whitespace;

                    canAddLineBreak = false;
                }

                if (CanAcceptNewLineBreaksAfter(match.kind))
                    canAddLineBreak = true;

                tokens.Add(token);

            }

            return tokens;
        }


        struct Match
        {
            public string excerpt;
            public TokenKind kind;


            public Match(string excerpt, TokenKind kind)
            {
                this.excerpt = excerpt;
                this.kind = kind;
            }
        }


        static Match? TryMatchFilter(
            TokenKind kind,
            string src, int index,
            Func<char, bool> filterPrefix,
            Func<char, bool> filterRest)
        {
            if (!filterPrefix(src[index]))
                return null;

            var length = 1;
            while (index + length < src.Length && filterRest(src[index + length]))
                length++;

            return new Match(src.Substring(index, length), kind);
        }


        static Match? TryMatchFixed(string src, int index)
        {
            var models = new Match[]
            {
                new Match("\n", TokenKind.LineBreak),
                new Match("{", TokenKind.BraceOpen),
                new Match("}", TokenKind.BraceClose),
                new Match("[", TokenKind.BracketOpen),
                new Match("]", TokenKind.BracketClose),
                new Match("(", TokenKind.ParenOpen),
                new Match(")", TokenKind.ParenClose),
                new Match(",", TokenKind.Comma),
                new Match(".", TokenKind.Dot),
                new Match(":", TokenKind.Colon),
                new Match(";", TokenKind.Semicolon),
                new Match("->", TokenKind.Arrow),
                new Match("==", TokenKind.EqualEqual),
                new Match("=", TokenKind.Equal),
                new Match("<=", TokenKind.LessThanEqual),
                new Match("<", TokenKind.LessThan),
                new Match(">=", TokenKind.GreaterThanEqual),
                new Match(">", TokenKind.GreaterThan),
                new Match("+", TokenKind.Plus),
                new Match("-", TokenKind.Minus),
                new Match("*", TokenKind.Asterisk),
                new Match("/", TokenKind.Slash),
                new Match("!=", TokenKind.ExclamationMarkEqual),
                new Match("!", TokenKind.ExclamationMark),
                new Match("&&", TokenKind.AmpersandAmpersand),
                new Match("&", TokenKind.Ampersand),
                new Match("||", TokenKind.VerticalBarVerticalBar),
                new Match("|", TokenKind.VerticalBar),
                new Match("include", TokenKind.KeywordInclude),
                new Match("fn", TokenKind.KeywordFunction),
                new Match("function", TokenKind.KeywordFunction),
                new Match("var", TokenKind.KeywordVar),
                new Match("new", TokenKind.KeywordNew),
                new Match("if", TokenKind.KeywordIf),
                new Match("else", TokenKind.KeywordElse),
                new Match("while", TokenKind.KeywordWhile),
                new Match("loop", TokenKind.KeywordLoop),
                new Match("break", TokenKind.KeywordBreak),
                new Match("continue", TokenKind.KeywordContinue),
                new Match("return", TokenKind.KeywordReturn),
                new Match("true", TokenKind.KeywordTrue),
                new Match("false", TokenKind.KeywordFalse)
            };

            // Check whether one of the models match.
            foreach (var model in models)
            {
                var match = true;

                for (int i = 0; i < model.excerpt.Length; i++)
                {
                    if (index + i >= src.Length ||
                        src[index + i] != model.excerpt[i])
                        match = false;
                }

                if (match)
                    return model;
            }

            return null;
        }


        static Match? TryMatchString(string src, int index)
        {
            if (src[index] != '\"')
                return null;

            var str = "\"";

            index++;
            while (index < src.Length && src[index] != '\"')
            {
                str += src[index];
                index++;
            }

            if (src[index] != '\"')
                return null;

            index++;
            str += "\"";
            return new Match(str, TokenKind.String);
        }


        static Match? TryMatchSingleLineComment(string src, int index)
        {
            if (src[index] != '/')
                return null;

            index++;

            if (index >= src.Length || src[index] != '/')
                return null;

            index++;

            var str = "//";
            while (index < src.Length && src[index] != '\n')
            {
                str += src[index];
                index++;
            }

            return new Match(str, TokenKind.Comment);
        }


        static Match? TryMatchMultiLineComment(string src, int index)
        {
            if (index + 1 >= src.Length || src[index] != '/' || src[index + 1] != '*')
                return null;

            index += 2;

            var nesting = 1;
            var str = "/*";
            while (true)
            {
                if (index + 1 >= src.Length)
                    return null;

                if (src[index] == '/' && src[index + 1] == '*')
                {
                    str += "/*";
                    index += 2;
                    nesting++;
                }

                else if (src[index] == '*' && src[index + 1] == '/')
                {
                    str += "*/";
                    index += 2;
                    nesting--;
                    if (nesting <= 0)
                        break;
                }

                else
                {
                    str += src[index];
                    index++;
                }
            }

            return new Match(str, TokenKind.Comment);
        }


        static bool IsWhitespace(char c)
        {
            return c == ' ' || c == '\t' || c == '\r';
        }


        static bool IsIdentifierPrefix(char c)
        {
            return (c >= 'A' && c <= 'Z') ||
                (c >= 'a' && c <= 'z') ||
                c == '_';
        }


        static bool IsIdentifier(char c)
        {
            return IsIdentifierPrefix(c) ||
                (c >= '0' && c <= '9');
        }


        static bool IsNumberPrefix(char c)
        {
            return (c >= '0' && c <= '9');
        }


        static bool IsNumber(char c)
        {
            return IsNumberPrefix(c) ||
                (c >= 'A' && c <= 'Z') ||
                (c >= 'a' && c <= 'z') ||
                c == '_' || c == '.';
        }


        static bool CanAcceptNewLineBreaksAfter(TokenKind kind)
        {
            return
                kind != TokenKind.LineBreak &&
                kind != TokenKind.Whitespace &&
                kind != TokenKind.Comment;
        }
    }
}
