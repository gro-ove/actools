using NUnit.Framework;
using StringBasedFilter.TestEntries;

namespace StringBasedFilter.Tests {
    [TestFixture]
    public class EntiesTest {
        [Test]
        public void StringEntryTest() {
            var a = new StringTestEntry("aBc", StringMatchMode.IncludedWithin);
            var b = new StringTestEntry("aBc", StringMatchMode.CompleteMatch);

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