using System;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;

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

                case "f":
                    return value.Test(obj.ElementAtOrDefault(0));

                case "s":
                    return value.Test(obj.ElementAtOrDefault(1));

                case "c":
                    return obj.Any(value.Test);
            }

            return false;
        }

        public bool TestChild(string[] obj, string key, IFilter filter) {
            switch (key) {
                case null:
                case "0":
                case "f":
                    return obj.Length > 0 && filter.Test(StringTester.Instance, obj[0]);

                case "1":
                case "s":
                    return obj.Length > 1 && filter.Test(StringTester.Instance, obj[1]);
            }

            return false;
        }
    }

    [TestFixture]
    public class FilterTest {
        [Test]
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

        [Test]
        public void FilteringTestSpace() {
            var filter = Filter.Create(new StringTester(), "A B");


            var s = filter.ToString();
            Console.WriteLine(s);


            Assert.IsTrue(filter.Test("A B"));
            Assert.IsTrue(filter.Test("A Q B"));
            Assert.IsTrue(filter.Test("a b"));
            Assert.IsTrue(filter.Test("q A B"));
            Assert.IsFalse(filter.Test("qA B"));
        }

        [Test]
        public void ParsingTestSpace() {
            var filter = Filter.Create(new StringTester(), "A B | C");
            var s = filter.ToString();
            Console.WriteLine(s);
            Assert.IsTrue(filter.Test("A B"));
            Assert.IsTrue(filter.Test("A Q B"));
            Assert.IsTrue(filter.Test("a b"));
            Assert.IsTrue(filter.Test("q A B"));
            Assert.IsFalse(filter.Test("qA B"));


            filter = Filter.Create(new StringTester(), "\"A B\" | C");
            s = filter.ToString();
            Console.WriteLine(s);
            Assert.IsTrue(filter.Test("A B"));
            Assert.IsFalse(filter.Test("A Q B"));
            Assert.IsTrue(filter.Test("a b"));
            Assert.IsTrue(filter.Test("q A B"));
            Assert.IsFalse(filter.Test("qA B"));
        }

        [Test]
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

            var em2 = Filter.Create(new StringTester(), "'!'");
            Console.WriteLine(em2);
            Assert.IsTrue(em2.Test("!"));
            Assert.IsFalse(em2.Test("a!"));
            Assert.IsFalse(em2.Test("!a"));
        }

        [Test]
        public void DevTest() {
            var filter = Filter.Create(new StringTester(), "A & B(Q)");


            var s = filter.ToString();
            Console.WriteLine(s);
        }

        [Test]
        public void StrictMatchTest() {
            var filterAny = Filter.Create(ListTester.Instance, "f:test");
            Console.WriteLine(filterAny);

            Assert.IsTrue(filterAny.Test(new[] { "test" }));
            Assert.IsTrue(filterAny.Test(new[] { "testb" }));
            Assert.IsTrue(filterAny.Test(new[] { "a testb" }));

            var filterStrict = Filter.Create(ListTester.Instance, "f:'test'");
            Console.WriteLine(filterStrict);

            Assert.IsTrue(filterStrict.Test(new[] { "test" }));
            Assert.IsFalse(filterStrict.Test(new[] { "testb" }));
            Assert.IsFalse(filterStrict.Test(new[] { "atestb" }));

            var filterExclamationMarkAny = Filter.Create(ListTester.Instance, "f:\"!\"");
            Console.WriteLine(filterExclamationMarkAny);

            Assert.IsTrue(filterExclamationMarkAny.Test(new[] { "!" }));
            Assert.IsTrue(filterExclamationMarkAny.Test(new[] { "!b" }));
            Assert.IsTrue(filterExclamationMarkAny.Test(new[] { "a !b" }));

            var filterExclamationMarkStrict = Filter.Create(ListTester.Instance, "f:'!'");
            Console.WriteLine(filterExclamationMarkStrict);

            Assert.IsTrue(filterExclamationMarkStrict.Test(new[] { "!" }));
            Assert.IsFalse(filterExclamationMarkStrict.Test(new[] { "!b" }));
            Assert.IsFalse(filterExclamationMarkStrict.Test(new[] { "a!b" }));
        }

        [Test]
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

        [Test]
        public void PriorityTest() {
            var w0 = Stopwatch.StartNew();
            var filter0 = Filter.Create(ListTester.Instance, "len=2 & 1(A & B) | ((0:A* ^ empty+) & 0(len > 1)) | 0(len=3)");
            var filter1 = Filter.Create(ListTester.Instance, "len=2 & 1(A & B) | (0:A* ^ empty+) & 0(len > 1) | 0(len=3)");
            Assert.AreEqual(filter0.ToString(), filter1.ToString());
        }

        [Test]
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

        [Test]
        public void SubFilterDotTest() {
            var w0 = Stopwatch.StartNew();
            var filter = Filter.Create(ListTester.Instance, "len=2 & 1(A & B) | ((0:A* ^ empty+) & f.len > 1) | f.len=3");
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

        [Test]
        public void PropagateKeyTest_0() {
            var filter = Filter.Create(new ListTester(), "len<4 & !c:bmw lotus ferrari");
            Console.WriteLine(filter);
            Assert.AreEqual("{ { { \"len=<4\" && { ! \"c==bmw\" } } && { ! \"c==lotus\" } } && { ! \"c==ferrari\" } }", filter.ToString());

            Assert.IsTrue(filter.Test(new []{ "mini", "lada", "toyota" }));
            Assert.IsFalse(filter.Test(new []{ "audi", "bmw", "toyota" }));
            Assert.IsFalse(filter.Test(new []{ "lotus" }));
            Assert.IsFalse(filter.Test(new []{ "lotus", "ferrari" }));
            Assert.IsTrue(filter.Test(new []{ "mersedes", "aston martin", "maserati" }));
            Assert.IsFalse(filter.Test(new []{ "mersedes", "aston martin", "maserati", "vw" }));
        }

        [Test]
        public void PropagateKeyTest_1() {
            var filter = Filter.Create(new ListTester(), "len<4 !c:bmw lotus ferrari");
            Console.WriteLine(filter);

            Assert.IsTrue(filter.Test(new []{ "mini", "lada", "toyota" }));
            Assert.IsFalse(filter.Test(new []{ "audi", "bmw", "toyota" }));
            Assert.IsFalse(filter.Test(new []{ "lotus" }));
            Assert.IsFalse(filter.Test(new []{ "lotus", "ferrari" }));
            Assert.IsTrue(filter.Test(new []{ "mersedes", "aston martin", "maserati" }));
            Assert.IsFalse(filter.Test(new []{ "mersedes", "aston martin", "maserati", "vw" }));
        }

        [Test]
        public void QueryTest() {
            Assert.IsTrue(Filter.Create(new StringTester(), "ho*og").Test("hotdog"));
            Assert.IsTrue(Filter.Create(new StringTester(), "hot?og").Test("hotdog"));
            Assert.IsFalse(Filter.Create(new StringTester(), "hot.og").Test("hot,og"));
            Assert.IsFalse(Filter.Create(new StringTester(), @"hot\)\)\)").Test("hotdog"));
            Assert.IsFalse(Filter.Create(new StringTester(), @"hot\(\(\(").Test("hotdog"));
        }
    }
}
