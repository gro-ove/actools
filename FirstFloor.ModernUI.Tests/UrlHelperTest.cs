using FirstFloor.ModernUI.Helpers;
using NUnit.Framework;

namespace FirstFloor.ModernUI.Tests {
    [TestFixture]
    public class UrlHelperTest {
        [Test]
        public void TestPort() {
            Assert.AreEqual(true, UrlHelper.IsWebUrl("ac.server.com:8081", 0, false, out var len));
            Assert.AreEqual("ac.server.com:8081".Length, len);
        }
    }
}