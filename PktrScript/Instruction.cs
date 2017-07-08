using System.Collections.Generic;


namespace PktrScript
{
    public abstract class Instruction
    {
        public Span span;


        public abstract string Printable();
        public abstract void Execute(Runtime runtime, Runtime.Thread thread);


        public class Discard : Instruction
        {
            public override string Printable()
            {
                return "pop";
            }


            public override void Execute(Runtime runtime, Runtime.Thread thread)
            {
                thread.PopValue();
            }
        }


        public class PushLiteral : Instruction
        {
            public object literal;


            public override string Printable()
            {
                return "push " + Runtime.Printable(this.literal);
            }


            public override void Execute(Runtime runtime, Runtime.Thread thread)
            {
                thread.PushValue(this.literal);
            }
        }


        public class PushLocal : Instruction
        {
            public string name;


            public override string Printable()
            {
                return "pushlocal `" + this.name + "`";
            }


            public override void Execute(Runtime runtime, Runtime.Thread thread)
            {
                object value;
                if (!runtime.GetVar(thread.CurrentFrame().locals, this.name, out value))
                    Runtime.RaiseRuntimeError(thread, this.span, "unknown local `" + this.name + "`");

                thread.PushValue(value);
            }
        }


        public class PushGlobal : Instruction
        {
            public string name;


            public override string Printable()
            {
                return "pushglobal `" + this.name + "`";
            }


            public override void Execute(Runtime runtime, Runtime.Thread thread)
            {
                object value;
                if (!runtime.GetVar(runtime.globals, this.name, out value))
                    Runtime.RaiseRuntimeError(thread, this.span, "unknown global `" + this.name + "`");

                thread.PushValue(value);
            }
        }


        public class NewLocal : Instruction
        {
            public string name;


            public override string Printable()
            {
                return "newlocal `" + this.name + "`";
            }


            public override void Execute(Runtime runtime, Runtime.Thread thread)
            {
                if (!runtime.NewVar(thread.CurrentFrame().locals, this.name))
                    Runtime.RaiseRuntimeError(thread, this.span, "duplicate local `" + this.name + "`");
            }
        }


        public class NewGlobal : Instruction
        {
            public string name;


            public override string Printable()
            {
                return "newglobal `" + this.name + "`";
            }


            public override void Execute(Runtime runtime, Runtime.Thread thread)
            {
                if (!runtime.NewVar(runtime.globals, this.name))
                    Runtime.RaiseRuntimeError(thread, this.span, "duplicate global `" + this.name + "`");
            }
        }


        public class DeleteLocal : Instruction
        {
            public string name;


            public override string Printable()
            {
                return "dellocal `" + this.name + "`";
            }


            public override void Execute(Runtime runtime, Runtime.Thread thread)
            {
                if (!runtime.DelVar(thread.CurrentFrame().locals, this.name))
                    Runtime.RaiseRuntimeError(thread, this.span, "unknown local `" + this.name + "`");
            }
        }


        public class DeleteGlobal : Instruction
        {
            public string name;


            public override string Printable()
            {
                return "delglobal `" + this.name + "`";
            }


            public override void Execute(Runtime runtime, Runtime.Thread thread)
            {
                if (!runtime.DelVar(runtime.globals, this.name))
                    Runtime.RaiseRuntimeError(thread, this.span, "unknown global `" + this.name + "`");
            }
        }


        public class SetLocal : Instruction
        {
            public string name;


            public override string Printable()
            {
                return "setlocal `" + this.name + "`";
            }


            public override void Execute(Runtime runtime, Runtime.Thread thread)
            {
                if (!runtime.SetVar(thread.CurrentFrame().locals, this.name, thread.PopValue()))
                    Runtime.RaiseRuntimeError(thread, this.span, "unknown local `" + this.name + "`");
            }
        }


        public class SetGlobal : Instruction
        {
            public string name;


            public override string Printable()
            {
                return "setglobal `" + this.name + "`";
            }


            public override void Execute(Runtime runtime, Runtime.Thread thread)
            {
                if (!runtime.SetVar(runtime.globals, this.name, thread.PopValue()))
                    Runtime.RaiseRuntimeError(thread, this.span, "unknown global `" + this.name + "`");
            }
        }


        public class NewObject : Instruction
        {
            public List<string> fieldNames = new List<string>();


            public override string Printable()
            {
                var str = "new { ";
                for (var i = 0; i < this.fieldNames.Count; i++)
                {
                    if (i > 0)
                        str += ", ";

                    str += this.fieldNames[i];
                }

                return str + " }";
            }


            public override void Execute(Runtime runtime, Runtime.Thread thread)
            {
                var obj = new Runtime.Object();

                for (var i = 0; i < fieldNames.Count; i++)
                    obj.fields[fieldNames[fieldNames.Count - 1 - i]] = thread.PopValue();

                thread.PushValue(obj);
            }
        }


        public class Call : Instruction
        {
            public int argumentNum;


            public override string Printable()
            {
                return "call " + this.argumentNum.ToString();
            }


            public override void Execute(Runtime runtime, Runtime.Thread thread)
            {
                var args = new object[this.argumentNum];
                for (var i = 0; i < this.argumentNum; i++)
                    args[this.argumentNum - 1 - i] = thread.PopValue();

                var target = thread.PopValue();

                var targetFunc = target as Runtime.Function;
                if (targetFunc != null)
                {
                    if (targetFunc.parameterNames.Count != this.argumentNum)
                        Runtime.RaiseRuntimeError(thread, this.span, "expected " + targetFunc.parameterNames.Count + " argument(s)");

                    thread.PushFrame(targetFunc);
                    for (var i = 0; i < this.argumentNum; i++)
                        thread.CurrentFrame().locals.Add(targetFunc.parameterNames[i], args[i]);

                    return;
                }

                var targetExternal = target as Runtime.ExternalFunction;
                if (targetExternal != null)
                {
                    try
                    {
                        thread.PushValue(targetExternal.Invoke(args));
                    }
                    catch (System.Exception e)
                    {
                        Runtime.RaiseRuntimeError(thread, this.span, "external function error:\n\n" + e.Message + "\n\n" + e.StackTrace);
                    }

                    return;
                }

                Runtime.RaiseRuntimeError(thread, this.span, "object cannot be called as function");
            }
        }


        public class PushField : Instruction
        {
            public bool duplicateTargetAfter;
            public string fieldName;


            public override string Printable()
            {
                if (this.duplicateTargetAfter)
                    return "pushmethod `" + this.fieldName + "`";
                else
                    return "pushfield `" + this.fieldName + "`";
            }


            public override void Execute(Runtime runtime, Runtime.Thread thread)
            {
                var target = thread.PopValue();
                if (target == null)
                    Runtime.RaiseRuntimeError(thread, this.span, "field access on null value");

                var obj = target;
                while (obj != null)
                {
                    Dictionary<string, object> fields;

                    if (obj is double)
                        fields = runtime.doubleMethods;

                    else if (obj is bool)
                        fields = runtime.boolMethods;

                    else if (obj is string)
                        fields = runtime.stringMethods;

                    else if (obj is Runtime.Object)
                        fields = ((Runtime.Object)obj).fields;

                    else
                        break;

                    object getFunc;
                    if (!this.duplicateTargetAfter && fields.TryGetValue("get:" + this.fieldName, out getFunc))
                    {
                        if (getFunc is Runtime.Function)
                        {
                            var f = (Runtime.Function)getFunc;
                            thread.PushFrame(f);
                            thread.CurrentFrame().locals.Add(f.parameterNames[0], target);
                        }
                        else
                            thread.PushValue(((Runtime.ExternalFunction)getFunc).Invoke(new object[] { target }));

                        return;
                    }

                    object value;
                    if (fields.TryGetValue(this.fieldName, out value))
                    {
                        thread.PushValue(value);
                        if (this.duplicateTargetAfter)
                            thread.PushValue(target);

                        return;
                    }

                    if (obj is Runtime.Object)
                        obj = ((Runtime.Object)obj).proto;
                    else
                        break;
                }

                Runtime.RaiseRuntimeError(thread, this.span, "unknown field `" + this.fieldName + "`");
            }
        }


        public class SetField : Instruction
        {
            public string fieldName;


            public override string Printable()
            {
                return "setfield `" + this.fieldName + "`";
            }


            public override void Execute(Runtime runtime, Runtime.Thread thread)
            {
                var value = thread.PopValue();
                var target = thread.PopValue();
                if (target == null)
                    Runtime.RaiseRuntimeError(thread, this.span, "field access on null value");

                var obj = target;
                while (obj != null)
                {
                    Dictionary<string, object> fields;

                    if (obj is Runtime.Object)
                        fields = ((Runtime.Object)obj).fields;

                    else
                        break;

                    object setFunc;
                    if (fields.TryGetValue("set:" + this.fieldName, out setFunc))
                    {
                        if (setFunc is Runtime.Function)
                        {
                            var f = (Runtime.Function)setFunc;
                            thread.PushFrame(f);
                            thread.CurrentFrame().locals.Add(f.parameterNames[0], target);
                            thread.CurrentFrame().locals.Add(f.parameterNames[1], value);
                        }
                        else
                            thread.PushValue(((Runtime.ExternalFunction)setFunc).Invoke(new object[] { target, value }));

                        return;
                    }

                    if (fields.ContainsKey(this.fieldName))
                    {
                        fields[this.fieldName] = value;
                        return;
                    }

                    if (obj is Runtime.Object)
                        obj = ((Runtime.Object)obj).proto;
                    else
                        break;
                }

                Runtime.RaiseRuntimeError(thread, this.span, "unknown field `" + this.fieldName + "`");
            }
        }


        public class Return : Instruction
        {
            public override string Printable()
            {
                return "return";
            }


            public override void Execute(Runtime runtime, Runtime.Thread thread)
            {
                thread.PopFrame();
            }
        }


        public class Goto : Instruction
        {
            public int destination;


            public override string Printable()
            {
                return "goto " + this.destination;
            }


            public override void Execute(Runtime runtime, Runtime.Thread thread)
            {
                thread.CurrentFrame().Goto(destination);
            }
        }


        public class Branch : Instruction
        {
            public int destinationIfTrue;
            public int destinationIfFalse;


            public override string Printable()
            {
                return "branch ? " + this.destinationIfTrue + " : " + this.destinationIfFalse;
            }


            public override void Execute(Runtime runtime, Runtime.Thread thread)
            {
                var cond = thread.stackData[thread.stackData.Count - 1];
                thread.stackData.RemoveAt(thread.stackData.Count - 1);

                if (cond is bool && (bool)cond)
                    thread.CurrentFrame().Goto(destinationIfTrue);
                else
                    thread.CurrentFrame().Goto(destinationIfFalse);
            }
        }
    }
}
