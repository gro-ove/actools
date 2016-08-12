using AcTools.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AcTools.Tests {
    [TestClass]
    public class MathUtilsTest {
        [TestMethod]
        public void Round() {
            Assert.AreEqual(0.35, 0.352.Round(0.01), 0.0000001);
            Assert.AreEqual(0.36, 0.352.Round(0.02), 0.0000001);
            Assert.AreEqual(0.35, 0.352.Round(0.05), 0.0000001);
            Assert.AreEqual(10.35, 10.352.Round(0.05), 0.0000001);
            Assert.AreEqual(10.0, 10.352.Round(10), 0.0000001);

            Assert.AreEqual(330, 340.Round(33));
            Assert.AreEqual(350, 340.Round(25));
            Assert.AreEqual(350, 340.Round(50));
        }

        [TestMethod]
        public void RoughlyEquals() {
            Assert.IsTrue(15.342.RoughlyEquals(15.34));
            Assert.IsFalse(15.34.RoughlyEquals(15.342));
            Assert.IsFalse(5.34.RoughlyEquals(15.34));
            Assert.IsFalse(15.34.RoughlyEquals(-15.34));
            Assert.IsFalse(15.14.RoughlyEquals(-15.34));
        }
    }
}