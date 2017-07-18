using AcTools.Render.Base;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AcTools.Render.Tests {
    [TestClass]
    public class RendererClockTest {
        [TestMethod]
        public void Test() {
            var clock = new RendererClock(5);

            clock.RegisterFrame(1f);
            clock.RegisterFrame(2f);

            Assert.AreEqual(2d, clock.GetTotalTime(1));
            Assert.AreEqual(3d, clock.GetTotalTime(2));
            Assert.AreEqual(6d, clock.GetTotalTime(4));
            Assert.AreEqual(60d, clock.GetTotalTime(40));

            clock.RegisterFrame(2f);
            clock.RegisterFrame(2f);
            clock.RegisterFrame(2f);
            clock.RegisterFrame(2f);

            Assert.AreEqual(2d, clock.GetTotalTime(1));
            Assert.AreEqual(8d, clock.GetTotalTime(4));
            Assert.AreEqual(10d, clock.GetTotalTime(5));
            Assert.AreEqual(200d, clock.GetTotalTime(100));
        }
    }
}