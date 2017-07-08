using System;
using System.Collections.Generic;


namespace PktrScript
{
    public class Runtime
    {
        public Dictionary<string, object> globals = new Dictionary<string, object>();

        public Dictionary<string, object> doubleMethods = new Dictionary<string, object>();
        public Dictionary<string, object> boolMethods = new Dictionary<string, object>();
        public Dictionary<string, object> stringMethods = new Dictionary<string, object>();


        public Runtime()
        {
            this.doubleMethods["+"]  = (ExternalFunction)((args) => ((double)args[0] +  (double)args[1]));
            this.doubleMethods["-"]  = (ExternalFunction)((args) => ((double)args[0] -  (double)args[1]));
            this.doubleMethods["*"]  = (ExternalFunction)((args) => ((double)args[0] *  (double)args[1]));
            this.doubleMethods["/"]  = (ExternalFunction)((args) => ((double)args[0] /  (double)args[1]));
            this.doubleMethods["%"]  = (ExternalFunction)((args) => ((double)args[0] %  (double)args[1]));
            this.doubleMethods["=="] = (ExternalFunction)((args) => ((double)args[0] == (double)args[1]));
            this.doubleMethods["!="] = (ExternalFunction)((args) => ((double)args[0] != (double)args[1]));
            this.doubleMethods["<"]  = (ExternalFunction)((args) => ((double)args[0] <  (double)args[1]));
            this.doubleMethods["<="] = (ExternalFunction)((args) => ((double)args[0] <= (double)args[1]));
            this.doubleMethods[">"]  = (ExternalFunction)((args) => ((double)args[0] >  (double)args[1]));
            this.doubleMethods[">="] = (ExternalFunction)((args) => ((double)args[0] >= (double)args[1]));
            this.doubleMethods["-_"] = (ExternalFunction)((args) => (-(double)args[0]));

            this.boolMethods["=="] = (ExternalFunction)((args) => ((bool)args[0] == (bool)args[1]));
            this.boolMethods["!="] = (ExternalFunction)((args) => ((bool)args[0] != (bool)args[1]));
            this.boolMethods["&"]  = (ExternalFunction)((args) => ((bool)args[0] &  (bool)args[1]));
            this.boolMethods["|"]  = (ExternalFunction)((args) => ((bool)args[0] |  (bool)args[1]));
            this.boolMethods["!_"] = (ExternalFunction)((args) => (!(bool)args[0]));

            this.stringMethods["+"]  = (ExternalFunction)((args) => ((string)args[0] +  (string)args[1]));
            this.stringMethods["=="] = (ExternalFunction)((args) => ((string)args[0] == (string)args[1]));
            this.stringMethods["!="] = (ExternalFunction)((args) => ((string)args[0] != (string)args[1]));
        }


        public object GetGlobal(string name)
        {
            object value;
            if (this.GetVar(this.globals, name, out value))
                return value;
            else
                return null;
        }


        public string GetGlobalName(object value)
        {
            foreach (var g in globals)
            {
                if (g.Value == value)
                    return g.Key;
            }
            return null;
        }


        public void SetGlobal(string name, object value)
        {
            this.globals[name] = value;
        }


        public void SetExternalFunct(string name, ExternalFunction f)
        {
            this.globals[name] = f;
        }


        public object Execute(Function f, params object[] args)
        {
            var thread = this.CreateThread(f, args);
            while (!thread.IsOver())
                this.StepThread(thread);

            return thread.PopValue();
        }


        public object ExecuteGlobal(string name, params object[] args)
        {
            return this.Execute((Function)this.GetGlobal(name), args);
        }


        public object ExecuteSource(string filename, string src, IncludeHandler includeHandler = null)
        {
            var tokens = Tokenizer.Tokenize(filename, src);
            var ast = Parser.Parse(tokens);
            var compilation = Compiler.Compile(ast, includeHandler);
            return this.Execute(compilation);
        }


        public Thread CreateThread(Function f, params object[] args)
        {
            var thread = new Thread();
            if (f == null)
                throw new RuntimeError("function is null");

            thread.PushFrame(f);
            for (var i = 0; i < f.parameterNames.Count; i++)
                thread.CurrentFrame().locals[f.parameterNames[i]] = args[i];
            
            return thread;
        }


        public Thread CreateThreadFromGlobal(string name, object[] args)
        {
            return CreateThread((Function)this.GetGlobal(name), args);
        }


        public bool StepThread(Thread thread)
        {
            if (thread.stackFrames.Count == 0)
                return true;

            var frame = thread.CurrentFrame();
            if (frame.instructionIndex >= frame.func.instructions.Count)
            {
                thread.PopFrame();
                return false;
            }

            thread.currentInstr = frame.func.instructions[frame.instructionIndex];
            frame.instructionIndex++;
            thread.currentInstr.Execute(this, thread);
            return false;
        }


        public bool NewVar(Dictionary<string, object> dict, string name)
        {
            object value;
            if (dict.TryGetValue(name, out value))
                return false;

            dict.Add(name, null);
            return true;
        }


        public bool SetVar(Dictionary<string, object> dict, string name, object value)
        {
            object oldValue;
            if (!dict.TryGetValue(name, out oldValue))
                return false;

            dict[name] = value;
            return true;
        }


        public bool GetVar(Dictionary<string, object> dict, string name, out object value)
        {
            return dict.TryGetValue(name, out value);
        }


        public bool DelVar(Dictionary<string, object> dict, string name)
        {
            object value;
            if (!dict.TryGetValue(name, out value))
                return false;

            dict.Remove(name);
            return true;
        }


        public static string Printable(object obj)
        {
            if (obj == null)
                return "<null>";

            if (obj is double)
                return ((double)obj).ToString();

            if (obj is bool)
                return ((bool)obj).ToString();

            if (obj is string)
                return "\"" + ((string)obj) + "\"";

            if (obj is Function)
                return "<function>";

            if (obj is ExternalFunction)
                return "<external function>";

            if (obj is Object)
            {
                var objObject = (Object)obj;
                var str = "{ ";

                while (objObject != null)
                {
                    foreach (var pair in objObject.fields)
                        str += pair.Key + " = " + Printable(pair.Value) + ", ";

                    objObject = objObject.proto;
                }

                return str + "}";
            }

            return "<unknown>";
        }


        public static void RaiseCompileError(string descr, Span span)
        {
            throw new RuntimeError("error in <" + span.filename + "> at line " + (span.startLine + 1) + ", column " + (span.startColumn + 1) + ": " + descr);
        }


        public static void RaiseRuntimeError(Thread thread, Span span, string descr)
        {
            throw new RuntimeError("error in <" + span.filename + "> at line " + (span.startLine + 1) + ", column " + (span.startColumn + 1) + ": " + descr);
        }


        public void PrintThread(Thread thread)
        {
            Console.WriteLine("globals");
            foreach (var g in this.globals)
            {
                Console.Write("  ");
                Console.WriteLine(g.Key + " = " + Printable(g.Value));
            }

            for (var i = 0; i < thread.stackFrames.Count; i++)
            {
                Console.WriteLine("frame #" + i + ": <" + this.GetGlobalName(thread.stackFrames[i].func) + ">, " + thread.stackFrames[i].instructionIndex);
                foreach (var local in thread.stackFrames[i].locals)
                {
                    Console.Write("  ");
                    Console.WriteLine(local.Key + " = " + Printable(local.Value));
                }
            }

            Console.WriteLine("stack");
            for (var i = 0; i < thread.stackData.Count; i++)
            {
                Console.Write("  ");
                Console.WriteLine("[" + i.ToString().PadLeft(3) + "] = " + Printable(thread.stackData[i]));
            }
        }


        public class Object
        {
            public Object proto;
            public Dictionary<string, object> fields = new Dictionary<string, object>();
        }


        public class Function
        {
            public List<string> parameterNames = new List<string>();
            public List<Instruction> instructions = new List<Instruction>();


            public void PrintDebug()
            {
                Console.WriteLine("parameters: " + this.parameterNames.Count);

                foreach (var param in this.parameterNames)
                    Console.WriteLine("   `" + param + "`");

                Console.WriteLine("instructions: " + this.instructions.Count);

                for (var i = 0; i < this.instructions.Count; i++)
                    Console.WriteLine("   " + i + ": " + this.instructions[i].Printable());
            }
        }


        public delegate object ExternalFunction(object[] args);


        public class CompileError : System.Exception
        {
            public CompileError(string descr) : base(descr)
            {

            }
        }


        public class RuntimeError : System.Exception
        {
            public RuntimeError(string descr) : base(descr)
            {

            }
        }


        public class Thread
        {
            public class StackFrame
            {
                public Function func;
                public int instructionIndex;
                public Dictionary<string, object> locals = new Dictionary<string, object>();


                public void Goto(int index)
                {
                    this.instructionIndex = index;
                }
            }

            public List<StackFrame> stackFrames = new List<StackFrame>();
            public List<object> stackData = new List<object>();
            public Instruction currentInstr;


            public StackFrame CurrentFrame()
            {
                return this.stackFrames[this.stackFrames.Count - 1];
            }


            public Instruction CurrentInstruction()
            {
                return this.currentInstr;
            }


            public bool IsOver()
            {
                return this.stackFrames.Count == 0;
            }


            public void Stop()
            {
                this.stackFrames.Clear();
            }


            public void PushValue(object value)
            {
                this.stackData.Add(value);
            }


            public object PopValue()
            {
                var value = this.stackData[this.stackData.Count - 1];
                this.stackData.RemoveAt(this.stackData.Count - 1);
                return value;
            }


            public void PopFrame()
            {
                this.stackFrames.RemoveAt(this.stackFrames.Count - 1);
            }


            public void PushFrame(Function f)
            {
                this.stackFrames.Add(new StackFrame { func = f });
            }
        }
    }
}
