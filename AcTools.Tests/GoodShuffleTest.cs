using System;
using System.Linq;
using AcTools.Utils.Helpers;
using NUnit.Framework;

namespace AcTools.Tests {
    [TestFixture]
    public class GoodShuffleTest {
        [Test]
        public void GoodShuffleTest_TestUniformity() {
            var array = new[] { 1, 2, 3, 4, 5 };
            var g1 = GoodShuffle.Get(array);
            var g2 = GoodShuffle.Get(array);


            var n1Of10 = Enumerable.Range(0, 10).Select(x => g1.Next).Count(x => x == 1);
            var n2Of100 = Enumerable.Range(0, 100).Select(x => g2.Next).Count(x => x == 2);


            Assert.AreEqual(n1Of10, 2, 0, "n1Of10: isn’t uniform enough");
            Assert.AreEqual(n2Of100, 20, 0, "n2Of100: isn’t uniform enough");
        }

        [Test]
        public void GoodShuffleTest_TestUniformityWithIgnored() {
            var array = new[] { 1, 2, 3, 4, 5 };
            var g1 = GoodShuffle.Get(array);
            g1.IgnoreOnce(3);
            var g2 = GoodShuffle.Get(array);
            g2.IgnoreOnce(4);


            var listWithSkipped3 = Enumerable.Range(0, 9).Select(x => g1.Next).ToList();
            var n3Of9 = listWithSkipped3.Count(x => x == 3);
            var n4Of109 = Enumerable.Range(0, 109).Select(x => g2.Next).Count(x => x == 4);


            Assert.AreEqual(1, n3Of9, "isn’t uniform enough");
            Assert.AreEqual(21, n4Of109, "isn’t uniform enough");
        }

        [Test]
        public void GoodShuffleTest_TestRandomity() {
            // arrange
            var array = new[] { 1, 2, 3, 4, 5 };

            // act
            var v0 = CountAverage(() => GoodShuffle.Get(array).Next, x => x == 5);
            var v1 = CountAverage(() => GoodShuffle.Get(array).Next, x => x == 1);

            // assert
            Assert.AreEqual(0.2, v0, 0.15, "v0: isn’t random enough");
            Assert.AreEqual(0.2, v1, 0.15, "v1: isn’t random enough");
        }

        private double CountAverage<T>(Func<T> input, Func<T, bool> count, int iterations = 10000) {
            return (double)Enumerable.Range(0, iterations).Select(x => input()).Count(count) / iterations;
        }

        [Test]
        public void LimitedShuffleTest() {
            var array = Enumerable.Range(1, 10).ToArray();

            Console.WriteLine(string.Join(", ", LimitedShuffle.Get(array, 0.1).Take(array.Length)));
            Console.WriteLine(string.Join(", ", LimitedShuffle.Get(array, 0.2).Take(array.Length)));
            Console.WriteLine(string.Join(", ", LimitedShuffle.Get(array, 0.3).Take(array.Length)));

            foreach (var i in Enumerable.Range(1, 4)) {
                var v = Enumerable.Range(4 - i, 1 + 2 * i).Select(x => CountAverage(() => LimitedShuffle.Get(array, 0.1 * i).IndexOf(5), y => y == x)).ToList();
                var t = 1d / (1 + 2 * i);
                Console.WriteLine($"{v.Select(x => $"{x * 100:F1}%").JoinToString(", ")} (target: {t * 100:F1}%)");
                foreach (var d in v) {
                    Assert.AreEqual(t, d, 0.06, "something wrong at " + i);
                }
            }
        }

        [Test]
        public void BigLimitedShuffleTest() {
            var array = Enumerable.Range(1, 100).ToArray();

            {
                var v0 = CountAverage(() => LimitedShuffle.Get(array, 0.01).IndexOf(4), x => x < 2 || x > 4);
                var v1 = CountAverage(() => LimitedShuffle.Get(array, 0.01).IndexOf(4), x => x == 2);
                var v2 = CountAverage(() => LimitedShuffle.Get(array, 0.01).IndexOf(4), x => x == 3);
                var v3 = CountAverage(() => LimitedShuffle.Get(array, 0.01).IndexOf(4), x => x == 4);

                Assert.AreEqual(0, v0, "0.01: out of bounds");
                Assert.AreEqual(0.33, v1, 0.03, "0.01: something wrong");
                Assert.AreEqual(0.33, v2, 0.03, "0.01: something wrong");
                Assert.AreEqual(0.33, v3, 0.03, "0.01: something wrong");
            }

            {
                var v0 = CountAverage(() => LimitedShuffle.Get(array, 0.02).IndexOf(4), x => x < 1 || x > 5);
                var v1 = CountAverage(() => LimitedShuffle.Get(array, 0.02).IndexOf(4), x => x == 1);

                Assert.AreEqual(0, v0, "0.02: out of bounds");
                Assert.AreEqual(0.2, v1, 0.03, "0.02: something wrong");
            }
        }
    }
}
