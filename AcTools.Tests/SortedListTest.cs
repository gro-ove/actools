using System;
using System.Linq;
using AcTools.Utils.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AcTools.Tests {
    [TestClass]
    public class SortedListTest {
        [TestMethod]
        public void TestSmall() {
            const int size = 16;
            var random = new Random(Guid.NewGuid().GetHashCode());

            var list = new SortedList<int>(size);
            for (var i = 0; i < size; i++) {
                list.Add(random.Next(0, size));
            }

            Assert.IsTrue(list.SequenceEqual(list.OrderBy(x => x)), "not sorted properly");
        }

        [TestMethod]
        public void TestBig() {
            const int size = 10000;
            var random = new Random(Guid.NewGuid().GetHashCode());

            var list = new SortedList<int>(size);
            for (var i = 0; i < size; i++) {
                list.Add(random.Next(0, size));
            }

            Assert.IsTrue(list.SequenceEqual(list.OrderBy(x => x)), "not sorted properly");
        }
    }
}