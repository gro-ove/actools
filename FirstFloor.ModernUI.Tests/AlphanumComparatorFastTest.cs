using FirstFloor.ModernUI.Helpers;
using NUnit.Framework;

namespace FirstFloor.ModernUI.Tests {
    [TestFixture]
    public class AlphanumComparatorFastTest {
        private static void EnsureAboveOf(string a, string b) {
            Assert.AreEqual(true, AlphanumComparatorFast.Compare(a, b) < 0);
            Assert.AreEqual(true, AlphanumComparatorFast.Compare(b, a) > 0);
        }

        private static void EnsureBelowOf(string a, string b) {
            Assert.AreEqual(true, AlphanumComparatorFast.Compare(a, b) > 0);
            Assert.AreEqual(true, AlphanumComparatorFast.Compare(b, a) < 0);
        }

        private static void EnsureSame(string a, string b) {
            Assert.AreEqual(true, AlphanumComparatorFast.Compare(a, b) == 0);
            Assert.AreEqual(true, AlphanumComparatorFast.Compare(b, a) == 0);
        }

        [Test]
        public void ServerNamesTest() {
            EnsureAboveOf("abc", "def");
            EnsureBelowOf("abcc", "abc");
            EnsureBelowOf("17-abcc", "17-abc");
            EnsureAboveOf("17-abcc", "17-abcd");
            EnsureAboveOf("ab99c", "abc0c");
            EnsureAboveOf("ab99c", "abc0000c");

            EnsureBelowOf("test-15-q", "test-5-q");
            EnsureBelowOf("test-15-q", "test-005-q");
            EnsureAboveOf("test-15-q", "test-25-q");
            EnsureSame("test-15-q", "test-15-q");
            EnsureAboveOf("test-15-q-7", "test-15-q-14");
            EnsureBelowOf("test-15-q-70", "test-15-q-14");

            {
                var name1 = "AC-01 (www.assetto-fr.tk) French Nordschleife Endurance (SOL) (KMR)x:xdn566";
                var name2 = "AC-02 (www.assetto-fr.tk) French GT2 (Track Rotation) (SOL) (KMR)x:VAJezh";
                var name3 = "AC-03 (www.assetto-fr.tk) French GT3 (Track Rotation) (SOL) (KMR)x:JdWnGb";
                EnsureBelowOf(name2, name1);
                EnsureBelowOf(name3, name1);
                EnsureBelowOf(name3, name2);
            }

            {
                var name1 = "AC-01 (www.assetto-fr.tk) French Nordschleife Endurance (SOL) (KMR)x:xdn566";
                var name2 = "AC-02 (www.assetto-fr.tk) French GT2 (Track Rotation) (SOL) (KMR)x:VAJezh";
                var name3 = "AC-03 (www.assetto-fr.tk) French GT3 (Track Rotation) (SOL) (KMR)x:JdWnGb";
                EnsureBelowOf(name2, name1);
                EnsureBelowOf(name3, name1);
                EnsureBelowOf(name3, name2);
            }
        }
    }
}
