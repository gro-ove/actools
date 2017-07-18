using System.Linq;
using AcTools.Utils.Helpers;
using NUnit.Framework;

namespace AcTools.Tests {
    [TestFixture]
    public class SublistTest {
        [Test]
        public void Test() {
            var o = Enumerable.Range(0, 10).ToList();

            var sub = Sublist.Create(o, 2, 2);
            Assert.AreEqual(2, sub.Count);
            Assert.AreEqual(2, sub[0]);
            Assert.AreEqual(3, sub[1]);

            sub.Clear();
            Assert.AreEqual(8, o.Count);
            Assert.AreEqual(1, o[1]);
            Assert.AreEqual(4, o[2]);

            sub.Add(13);
            Assert.AreEqual(9, o.Count);
            Assert.AreEqual(13, o[2]);

            sub.Insert(0, 14);
            Assert.AreEqual(10, o.Count);
            Assert.AreEqual(14, o[2]);
            Assert.AreEqual(1, sub.IndexOf(13));

            sub.Remove(14);
            Assert.AreEqual(9, o.Count);
            Assert.AreEqual(13, o[2]);
            Assert.AreEqual(0, sub.IndexOf(13));

            sub.RemoveAt(0);
            Assert.AreEqual(8, o.Count);
            Assert.AreEqual(4, o[2]);
            Assert.AreEqual(-1, sub.IndexOf(13));
        }
    }
}