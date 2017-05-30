using System;
using System.Linq;
using AcTools.Utils.Helpers;
using NUnit.Framework;

namespace AcTools.Tests {
    [TestFixture]
    public class SortedListTest {
        [Test]
        public void TestSmall() {
            const int size = 16;
            var random = new Random(Guid.NewGuid().GetHashCode());

            var list = new SortedList<int>(size);
            for (var i = 0; i < size; i++) {
                list.Add(random.Next(0, size));
            }

            Assert.IsTrue(list.SequenceEqual(list.OrderBy(x => x)), "not sorted properly");
        }

        [Test]
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