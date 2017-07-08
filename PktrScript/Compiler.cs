using System;
using System.Collections.Generic;


namespace PktrScript
{
    public delegate string IncludeHandler(string filename);


    public class Compiler
    {
        public static Runtime.Function Compile(Node node, IncludeHandler includeHandler = null)
        {
            var compiler = new Compiler
            {
                f = new Runtime.Function(),
                globalScope = true,
                includeHandler = includeHandler
            };

            compiler.CompileExpr(node);

            return compiler.f;
        }


        private IncludeHandler includeHandler;
        private Runtime.Function f;
        private bool globalScope;
        private List<string> locals = new List<string>();
        private List<Instruction.Goto> continues;
        private List<Instruction.Goto> breaks;


        private Compiler()
        {

        }


        private void Emit(Instruction instr)
        {
            this.f.instructions.Add(instr);
        }


        private int CurrentIndex()
        {
            return this.f.instructions.Count;
        }


        private void NewVar(Span span, string name)
        {
            if (this.globalScope)
                this.Emit(new Instruction.NewGlobal { span = span, name = name });
            else
                this.Emit(new Instruction.NewLocal { span = span, name = name });
        }


        private void SetVar(Span span, string name)
        {
            if (this.globalScope)
                this.Emit(new Instruction.SetGlobal { span = span, name = name });
            else
                this.Emit(new Instruction.SetLocal { span = span, name = name });
        }


        private void DelVar(Span span, string name)
        {
            if (this.globalScope)
                this.Emit(new Instruction.DeleteGlobal { span = span, name = name });
            else
                this.Emit(new Instruction.DeleteLocal { span = span, name = name });
        }


        private void CompileBlock(Node node)
        {
            if (node.kind != NodeKind.TopLevel && node.kind != NodeKind.Block)
                throw new Exception("wrong node kind");

            var localsIndex = this.locals.Count;

            if (node.children.Count == 0)
                this.Emit(new Instruction.PushLiteral { span = node.span, literal = null });

            for (var c = 0; c < node.children.Count; c++)
            {
                CompileExpr(node.children[c]);

                if (c < node.children.Count - 1)
                    this.Emit(new Instruction.Discard { span = node.children[c].span });
            }

            if (node.kind != NodeKind.TopLevel)
            {
                for (var i = localsIndex; i < this.locals.Count; i++)
                    this.DelVar(node.span.JustAfter, this.locals[i]);

                this.locals.RemoveRange(localsIndex, this.locals.Count - localsIndex);
            }
        }


        private void CompileExpr(Node node)
        {
            switch (node.kind)
            {
                case NodeKind.Parenthesized: CompileExpr(node.children[0]); break;
                case NodeKind.TopLevel: CompileBlock(node); break;
                case NodeKind.Block: CompileBlock(node); break;
                case NodeKind.Include: CompileInclude(node); break;
                case NodeKind.FunctionDef: CompileFunctionDef(false, node); break;
                case NodeKind.FunctionDefAnon: CompileFunctionDef(true, node); break;
                case NodeKind.Identifier: CompileIdentifier(node); break;
                case NodeKind.Number: CompileNumber(node); break;
                case NodeKind.BoolTrue: CompileBool(node); break;
                case NodeKind.BoolFalse: CompileBool(node); break;
                case NodeKind.String: CompileString(node); break;
                case NodeKind.Var: CompileVar(node); break;
                case NodeKind.If: CompileIf(node); break;
                case NodeKind.While: CompileWhile(node); break;
                case NodeKind.Loop: CompileLoop(node); break;
                case NodeKind.Return: CompileReturn(node); break;
                case NodeKind.Continue: CompileContinue(node); break;
                case NodeKind.Break: CompileBreak(node); break;
                case NodeKind.FieldAccess: CompileFieldAccess(node); break;
                case NodeKind.Call: CompileCall(false, node); break;
                case NodeKind.MethodCall: CompileCall(true, node); break;
                case NodeKind.BinaryAssign: CompileBinaryAssign(node); break;
                case NodeKind.BinaryPlus:
                case NodeKind.BinaryMinus:
                case NodeKind.BinaryMultiply:
                case NodeKind.BinaryDivide:
                case NodeKind.BinaryEqual:
                case NodeKind.BinaryNotEqual:
                case NodeKind.BinaryLessThan:
                case NodeKind.BinaryLessThanEqual:
                case NodeKind.BinaryGreaterThan:
                case NodeKind.BinaryGreaterThanEqual:
                case NodeKind.BinaryAnd:
                case NodeKind.BinaryOr: CompileBinaryOp(node); break;
                case NodeKind.UnaryNot:
                case NodeKind.UnaryNegation: CompileUnaryOp(node); break;
                case NodeKind.LiteralObject: CompileLiteralObject(node); break;

                default:
                    Runtime.RaiseCompileError("unimplemented", node.span);
                    break;
            }
        }


        private void CompileInclude(Node node)
        {
            if (this.includeHandler == null)
                Runtime.RaiseCompileError("cannot include files", node.span);

            var filename = node.tokens[0].excerpt.Substring(1, node.tokens[0].excerpt.Length - 2);
            var src = this.includeHandler(filename);
            if (src == null)
                Runtime.RaiseCompileError("included file not found: <" + filename + ">", node.span);

            var tokens = Tokenizer.Tokenize("included " + filename, src);
            var ast = Parser.Parse(tokens);
            var compilation = Compiler.Compile(ast, this.includeHandler);

            this.Emit(new Instruction.PushLiteral { span = node.span, literal = compilation });
            this.Emit(new Instruction.Call { span = node.span, argumentNum = 0 });
        }


        private void CompileFunctionDef(bool anon, Node node)
        {
            var newF = new Runtime.Function();

            if (!anon)
            {
                var name = node.children[0].tokens[0].excerpt;
                this.NewVar(node.children[0].span, name);
                this.Emit(new Instruction.PushLiteral { span = node.span, literal = newF });
                this.SetVar(node.children[0].span, name);
                this.Emit(new Instruction.PushLiteral { span = node.span.JustAfter, literal = null });
            }
            else
                this.Emit(new Instruction.PushLiteral { span = node.span, literal = newF });

            var prevFunc = this.f;
            var prevGlobalScope = this.globalScope;
            var prevLocals = this.locals;

            this.f = newF;
            this.globalScope = false;
            this.locals = new List<string>();

            for (var i = (anon ? 0 : 1); i < node.children.Count - 1; i++)
            {
                var paramName = node.children[i].tokens[0].excerpt;
                this.f.parameterNames.Add(paramName);
                this.locals.Add(paramName);
            }

            this.CompileExpr(node.children[node.children.Count - 1]);

            this.f = prevFunc;
            this.globalScope = prevGlobalScope;
            this.locals = prevLocals;
        }


        private void CompileIdentifier(Node node)
        {
            if (this.locals.Contains(node.tokens[0].excerpt) && !this.globalScope)
                this.Emit(new Instruction.PushLocal { span = node.span, name = node.tokens[0].excerpt });
            else
                this.Emit(new Instruction.PushGlobal { span = node.span, name = node.tokens[0].excerpt });
        }


        private void CompileNumber(Node node)
        {
            double value;
            if (double.TryParse(node.tokens[0].excerpt, out value))
                this.Emit(new Instruction.PushLiteral { span = node.span, literal = value });
            else
                Runtime.RaiseCompileError("invalid number `" + node.tokens[0].excerpt + "`", node.span);
        }


        private void CompileBool(Node node)
        {
            if (node.kind == NodeKind.BoolTrue)
                this.Emit(new Instruction.PushLiteral { span = node.span, literal = true });
            else
                this.Emit(new Instruction.PushLiteral { span = node.span, literal = false });
        }


        private void CompileString(Node node)
        {
            var str = "";
            foreach (var token in node.tokens)
                str += token.excerpt.Substring(1, token.excerpt.Length - 2);

            this.Emit(new Instruction.PushLiteral { span = node.span, literal = str });
        }


        private void CompileLiteralObject(Node node)
        {
            var fieldNames = new List<string>();

            foreach (var field in node.children)
            {
                fieldNames.Add(field.children[0].tokens[0].excerpt);
                this.CompileExpr(field.children[1]);
            }

            this.Emit(new Instruction.NewObject { span = node.span, fieldNames = fieldNames });
        }


        private void CompileFieldAccess(Node node)
        {
            if (node.children[1].kind != NodeKind.Identifier)
                Runtime.RaiseCompileError("expected field name", node.children[1].span);

            this.CompileExpr(node.children[0]);
            this.Emit(new Instruction.PushField { span = node.span, fieldName = node.children[1].tokens[0].excerpt, duplicateTargetAfter = false });
        }


        private void CompileCall(bool isMethod, Node node)
        {
            if (isMethod && node.children[1].kind != NodeKind.Identifier)
                Runtime.RaiseCompileError("expected method name", node.children[1].span);

            for (var c = 0; c < node.children.Count; c++)
            {
                if (c == 1 && isMethod)
                    this.Emit(new Instruction.PushField { span = node.children[c].span, fieldName = node.children[c].tokens[0].excerpt, duplicateTargetAfter = true });
                else
                    this.CompileExpr(node.children[c]);
            }

            this.Emit(new Instruction.Call { span = node.span, argumentNum = node.children.Count - 1 });
        }


        private void CompileBinaryAssign(Node node)
        {
            if (node.children[0].kind == NodeKind.Identifier)
            {
                this.CompileExpr(node.children[1]);
                this.SetVar(node.span, node.children[0].tokens[0].excerpt);
                this.Emit(new Instruction.PushLiteral { span = node.span.JustAfter, literal = null });
            }

            else if (node.children[0].kind == NodeKind.FieldAccess && node.children[0].children[1].kind == NodeKind.Identifier)
            {
                this.CompileExpr(node.children[0].children[0]);
                this.CompileExpr(node.children[1]);
                this.Emit(new Instruction.SetField { span = node.span, fieldName = node.children[0].children[1].tokens[0].excerpt });
                this.Emit(new Instruction.PushLiteral { span = node.span.JustAfter, literal = null });
            }

            else
                Runtime.RaiseCompileError("invalid assignment target", node.children[0].span);
        }


        private void CompileUnaryOp(Node node)
        {
            var op = (string)null;
            switch (node.kind)
            {
                case NodeKind.UnaryNot: op = "!_"; break;
                case NodeKind.UnaryNegation: op = "-_"; break;
                default:
                    Runtime.RaiseCompileError("unimplemented", node.span);
                    break;
            }

            this.CompileExpr(node.children[0]);
            this.Emit(new Instruction.PushField { span = node.span, fieldName = op, duplicateTargetAfter = true });
            this.Emit(new Instruction.Call { span = node.span, argumentNum = 1 });
        }


        private void CompileBinaryOp(Node node)
        {
            var op = (string)null;
            switch (node.kind)
            {
                case NodeKind.BinaryPlus: op = "+"; break;
                case NodeKind.BinaryMinus: op = "-"; break;
                case NodeKind.BinaryMultiply: op = "*"; break;
                case NodeKind.BinaryDivide: op = "/"; break;
                case NodeKind.BinaryEqual: op = "=="; break;
                case NodeKind.BinaryNotEqual: op = "!="; break;
                case NodeKind.BinaryLessThan: op = "<"; break;
                case NodeKind.BinaryLessThanEqual: op = "<="; break;
                case NodeKind.BinaryGreaterThan: op = ">"; break;
                case NodeKind.BinaryGreaterThanEqual: op = ">="; break;
                case NodeKind.BinaryAnd: op = "&"; break;
                case NodeKind.BinaryOr: op = "|"; break;
                default:
                    Runtime.RaiseCompileError("unimplemented", node.span);
                    break;
            }

            this.CompileExpr(node.children[0]);
            this.Emit(new Instruction.PushField { span = node.span, fieldName = op, duplicateTargetAfter = true });
            this.CompileExpr(node.children[1]);
            this.Emit(new Instruction.Call { span = node.span, argumentNum = 2 });
        }


        private void CompileVar(Node node)
        {
            var name = node.children[0].tokens[0].excerpt;
            this.locals.Add(name);
            this.NewVar(node.span, name);

            if (node.children.Count == 2)
            {
                this.CompileExpr(node.children[1]);
                this.SetVar(node.span, name);
            }

            this.Emit(new Instruction.PushLiteral { span = node.span.JustAfter, literal = null });
        }


        private void CompileIf(Node node)
        {
            this.CompileExpr(node.children[0]);

            var branch = new Instruction.Branch { span = node.span };
            this.Emit(branch);

            var trueIndex = this.CurrentIndex();
            this.CompileExpr(node.children[1]);
            this.Emit(new Instruction.Discard { span = node.children[1].span.JustAfter });

            var gotoAfter = new Instruction.Goto { span = node.children[1].span.JustAfter };
            this.Emit(gotoAfter);

            var afterIndex = this.CurrentIndex();
            var falseIndex = afterIndex;

            if (node.children.Count == 3)
            {
                this.CompileExpr(node.children[2]);
                this.Emit(new Instruction.Discard { span = node.children[2].span.JustAfter });
                afterIndex = this.CurrentIndex();
            }

            branch.destinationIfTrue = trueIndex;
            branch.destinationIfFalse = falseIndex;
            gotoAfter.destination = afterIndex;

            this.Emit(new Instruction.PushLiteral { span = node.span.JustAfter, literal = null });
        }


        private void CompileWhile(Node node)
        {
            var conditionIndex = this.CurrentIndex();

            this.CompileExpr(node.children[0]);

            var branch = new Instruction.Branch { span = node.span };
            this.Emit(branch);

            var prevContinues = this.continues;
            var prevBreaks = this.breaks;
            this.continues = new List<Instruction.Goto>();
            this.breaks = new List<Instruction.Goto>();

            var trueIndex = this.CurrentIndex();
            this.CompileExpr(node.children[1]);
            this.Emit(new Instruction.Discard { span = node.children[1].span.JustAfter });
            this.Emit(new Instruction.Goto { span = node.children[1].span.JustAfter, destination = conditionIndex });

            var afterIndex = this.CurrentIndex();
            branch.destinationIfTrue = trueIndex;
            branch.destinationIfFalse = afterIndex;

            foreach (var cont in this.continues)
                cont.destination = conditionIndex;

            foreach (var br in this.breaks)
                br.destination = afterIndex;

            this.continues = prevContinues;
            this.breaks = prevBreaks;

            this.Emit(new Instruction.PushLiteral { span = node.span.JustAfter, literal = null });
        }


        private void CompileLoop(Node node)
        {
            var loopIndex = this.CurrentIndex();

            var prevContinues = this.continues;
            var prevBreaks = this.breaks;
            this.continues = new List<Instruction.Goto>();
            this.breaks = new List<Instruction.Goto>();

            this.CompileExpr(node.children[0]);
            this.Emit(new Instruction.Discard { span = node.children[0].span.JustAfter });
            this.Emit(new Instruction.Goto { span = node.children[0].span.JustAfter, destination = loopIndex });

            var afterIndex = this.CurrentIndex();

            foreach (var cont in this.continues)
                cont.destination = loopIndex;

            foreach (var br in this.breaks)
                br.destination = afterIndex;

            this.continues = prevContinues;
            this.breaks = prevBreaks;

            this.Emit(new Instruction.PushLiteral { span = node.span.JustAfter, literal = null });
        }


        private void CompileReturn(Node node)
        {
            if (node.children.Count == 1)
                this.CompileExpr(node.children[0]);
            else
                this.Emit(new Instruction.PushLiteral { span = node.span, literal = null });

            this.Emit(new Instruction.Return { span = node.span });
            this.Emit(new Instruction.PushLiteral { span = node.span.JustAfter, literal = null });
        }


        private void CompileContinue(Node node)
        {
            if (this.continues == null)
                Runtime.RaiseCompileError("no loop to continue from", node.span);

            var cont = new Instruction.Goto { span = node.span };
            this.continues.Add(cont);
            this.Emit(cont);
        }


        private void CompileBreak(Node node)
        {
            if (this.breaks == null)
                Runtime.RaiseCompileError("no loop to break from", node.span);

            var br = new Instruction.Goto { span = node.span };
            this.breaks.Add(br);
            this.Emit(br);
        }
    }
}
