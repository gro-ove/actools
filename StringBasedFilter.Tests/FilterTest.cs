using System;
using System.Diagnostics;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StringBasedFilter.Tests {
    internal class ListTester : IParentTester<string[]> {
        public static readonly ListTester Instance = new ListTester();

        public string ParameterFromKey(string key) {
            return null;
        }

        public bool Test(string[] obj, string key, ITestEntry value) {
            switch (key) {
                case null:
                    return value.Test(string.Join(", ", obj));

                case "len":
                    return value.Test(obj.Length);

                case "empty":
                    return value.Test(obj.Length == 0);

                case "0":
                    return value.Test(obj.ElementAtOrDefault(0));

                case "1":
                    return value.Test(obj.ElementAtOrDefault(1));
            }

            return false;
        }

        public bool TestChild(string[] obj, string key, IFilter filter) {
            switch (key) {
                case null:
                case "0":
                    return obj.Length > 0 && filter.Test(StringTester.Instance, obj[0]);

                case "1":
                    return obj.Length > 1 && filter.Test(StringTester.Instance, obj[1]);
            }

            return false;
        }
    }

    [TestClass]
    public class FilterTest {
        [TestMethod]
        public void ParsingTest() {
            var filter = Filter.Create(new StringTester(), "A&B");


            var s = filter.ToString();
            Console.WriteLine(s);


            Assert.AreEqual(@"{ ""=a"" && ""=b"" }", s);
            Assert.IsTrue(filter.Test("A B"));
            Assert.IsTrue(filter.Test("a b"));
            Assert.IsFalse(filter.Test("A"));
            Assert.IsFalse(filter.Test("B"));
            Assert.IsFalse(filter.Test("qwerty"));
            Assert.IsFalse(filter.Test("AB"));
            Assert.IsFalse(filter.Test("ab"));
        }

        [TestMethod]
        public void ParsingTestSpace() {
            var filter = Filter.Create(new StringTester(), "A B");


            var s = filter.ToString();
            Console.WriteLine(s);

            
            Assert.IsTrue(filter.Test("A B"));
            Assert.IsTrue(filter.Test("a b"));
            Assert.IsTrue(filter.Test("q A B"));
            Assert.IsFalse(filter.Test("qA B"));
        }

        [TestMethod]
        public void QuotesTest() {
            var em0 = Filter.Create(new StringTester(), "!");
            Assert.IsFalse(em0.Test("!"));
            Assert.IsFalse(em0.Test("a!"));
            Assert.IsFalse(em0.Test("!a"));

            var ema = Filter.Create(new StringTester(), "\\!");
            Assert.IsTrue(ema.Test("!"));
            Assert.IsFalse(ema.Test("a!"));
            Assert.IsTrue(ema.Test("!a"));

            var em1 = Filter.Create(new StringTester(), "`!`");
            Assert.IsTrue(em1.Test("!"));
            Assert.IsTrue(em1.Test("a!"));
            Assert.IsTrue(em1.Test("!a"));

            var em2 = Filter.Create(new StringTester(), "\"!\""); 
            Assert.IsTrue(em2.Test("!"));
            Assert.IsFalse(em2.Test("a!"));
            Assert.IsFalse(em2.Test("!a"));
        }

        [TestMethod]
        public void DevTest() {
            var filter = Filter.Create(new StringTester(), "A & B(Q)");


            var s = filter.ToString();
            Console.WriteLine(s);
        }

        [TestMethod]
        public void ChildTest() {
            var filter = Filter.Create(ListTester.Instance, "len=2 & 1(A & B)");


            var s = filter.ToString();
            Console.WriteLine(s);


            Assert.IsTrue(Filter.Create(ListTester.Instance, "len=2 & len<5").Test(new[] { "Q", "A B" }));

            Assert.IsTrue(filter.Test(new[] { "Q", "A B" }));
            Assert.IsTrue(filter.Test(new[] { "Q", "Aa BB" }));
            Assert.IsFalse(filter.Test(new[] { "Q", "Aa BB", "5" }));
            Assert.IsFalse(filter.Test(new[] { "Q", "AaBB" }));
        }

        [TestMethod]
        public void PerformanceTest() {
            var w0 = Stopwatch.StartNew();
            var filter = Filter.Create(ListTester.Instance, "len=2 & 1(A & B) | ((0:A* ^ empty+) & 0(len > 1)) | 0(len=3)");
            Console.WriteLine($"creation: {w0.ElapsedMilliseconds} ms");


            var s = filter.ToString();
            Console.WriteLine(s);

            var d = new[] {
                new[] { "Q", "A B" },
                new[] { "Q", "Aa BB" },
                new[] { "Q", "Aa BB", "5" },
                new[] { "Q", "AaBB" },
                new[] { "QWE", "AaBB" }
            };

            var w = Stopwatch.StartNew();
            var m = 100000;
            var n = Enumerable.Range(0, m).Select(x => d[x % d.Length]).Count(filter.Test);
            Console.WriteLine($"{m} items: {w.ElapsedMilliseconds} ms");
            Assert.AreEqual(n, m * 3 / 5);
        }
    }
}
