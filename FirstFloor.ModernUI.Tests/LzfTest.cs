using System.Text;
using FirstFloor.ModernUI.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FirstFloor.ModernUI.Tests {
    [TestClass]
    public class LzfTest {
        [TestMethod]
        public void TestStream() {
            var data = "Loyal Guernseycow Improbable Cheerful Indochinesetiger Able Kindly Heron";
            var bytes = Encoding.UTF8.GetBytes(data);

            var c0 = Lzf.Compress(bytes);
            var d0 = Lzf.Decompress(c0, 0, c0.Length);
            Assert.AreEqual(data, Encoding.UTF8.GetString(d0));
        }
    }
}