using System.Linq;
using AcTools.Utils.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AcTools.Tests {
    [TestClass]
    public class GoodShuffleTest {
        [TestMethod]
        public void GoodShuffleTest_TestUniformity() {
            var array = new[] { 1, 2, 3, 4, 5 };
            var g1 = GoodShuffle.Get(array);
            var g2 = GoodShuffle.Get(array);
            

            var n1Of10 = Enumerable.Range(0, 10).Select(x => g1.Next).Count(x => x == 1);
            var n2Of100 = Enumerable.Range(0, 100).Select(x => g2.Next).Count(x => x == 2);
            

            Assert.AreEqual(n1Of10, 2, 0, "n1Of10: isn’t uniform enough");
            Assert.AreEqual(n2Of100, 20, 0, "n2Of100: isn’t uniform enough");
        }

        [TestMethod]
        public void GoodShuffleTest_TestUniformityWithIgnored() {
            var array = new[] { 1, 2, 3, 4, 5 };
            var g1 = GoodShuffle.Get(array);
            g1.IgnoreOnce(3);
            var g2 = GoodShuffle.Get(array);
            g2.IgnoreOnce(4);
            

            var listWithSkipped3 = Enumerable.Range(0, 9).Select(x => g1.Next).ToList();
            var n3Of9 = listWithSkipped3.Count(x => x == 3);
            var n4Of109 = Enumerable.Range(0, 109).Select(x => g2.Next).Count(x => x == 4);
            

            Assert.AreEqual(n3Of9, 1, 0, "isn’t uniform enough");
            Assert.AreEqual(n4Of109, 21, 0, "isn’t uniform enough");
        }
        
        [TestMethod]
        public void GoodShuffleTest_TestRandomity() {
            // arrange
            var array = new[] { 1, 2, 3, 4, 5 };

            // act
            var v0 = Enumerable.Range(0, 100).Select(x => GoodShuffle.Get(array).Next).Count(x => x == 5);
            var v1 = Enumerable.Range(0, 100).Select(x => GoodShuffle.Get(array).Next).Count(x => x == 1);

            // assert
            Assert.AreEqual(20, v0, 10, "v0: isn’t random enough");
            Assert.AreEqual(20, v1, 10, "v1: isn’t random enough");
        }
    }
}
