using System;
using PktrScript;


namespace Example
{
    class Program
    {
        static void Main(string[] args)
        {
            var src = "var z = 1; function get_z() { z }";

            var tokens = Tokenizer.Tokenize("test", src);
            foreach (var t in tokens) t.PrintDebug();
            Console.WriteLine();

            var ast = Parser.Parse(tokens);
            ast.PrintDebug();
            Console.WriteLine();

            var compiled = Compiler.Compile(ast);
            compiled.PrintDebug();
            Console.WriteLine();

            var runtime = new Runtime();

            try
            {
                runtime.Execute(compiled);
                var answer = runtime.ExecuteGlobal("get_z");
                Console.WriteLine("get_z returned: " + Runtime.Printable(answer));
            }
            catch (Runtime.RuntimeError e)
            {
                Console.WriteLine(e.Message);
            }

            Console.ReadKey();
        }
    }
}
