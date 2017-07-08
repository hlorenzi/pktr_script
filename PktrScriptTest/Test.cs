using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PktrScript;


namespace PktrScriptTests
{
    [TestClass]
    public class UnitTest1
    {
        public static Runtime.Function Compile(string src)
        {
            var tokens = Tokenizer.Tokenize("test", src);
            var ast = Parser.Parse(tokens);
            var compiled = Compiler.Compile(ast);
            return compiled;
        }


        public static void ExpectResult(string src, object result)
        {
            try
            {
                var runtime = new Runtime();
                Assert.IsTrue(result == runtime.Execute(Compile(src)), "wrong result");

                runtime = new Runtime();
                runtime.Execute(Compile("function _test() {\n" + src + "\n}"));
                Assert.IsTrue(result == runtime.ExecuteGlobal("_test"), "wrong result");
            }
            catch (Runtime.CompileError e)
            {
                Assert.Fail("compile error: " + e.Message);
            }
            catch (Runtime.RuntimeError e)
            {
                Assert.Fail("runtime error: " + e.Message);
            }
        }

        [TestMethod]
        public void Test()
        {
            ExpectResult("", null);
            ExpectResult("{}", null);
        }
    }
}
