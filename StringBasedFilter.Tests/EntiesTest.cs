using Microsoft.VisualStudio.TestTools.UnitTesting;
using StringBasedFilter.TestEntries;

namespace StringBasedFilter.Tests {
    [TestClass]
    public class EntiesTest {
        [TestMethod]
        public void StringEntryTest() {
            var a = new StringTestEntry("aBc", false);
            var b = new StringTestEntry("aBc", true);

            Assert.IsTrue(a.Test("abc"));
            Assert.IsTrue(a.Test("qw abc"));
            Assert.IsTrue(a.Test("qw ABCdef"));
            Assert.IsFalse(a.Test("qwABC"));

            Assert.IsTrue(b.Test("abc"));
            Assert.IsFalse(b.Test("qw abc"));
            Assert.IsFalse(b.Test("qw ABCdef"));
            Assert.IsFalse(b.Test("qwABC"));
        }
    }
}