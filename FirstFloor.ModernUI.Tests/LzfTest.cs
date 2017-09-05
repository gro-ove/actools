using System.Text;
using FirstFloor.ModernUI.Helpers;
using NUnit.Framework;
using Assert = Microsoft.VisualStudio.TestTools.UnitTesting.Assert;

namespace FirstFloor.ModernUI.Tests {
    [TestFixture]
    public class LzfTest {
        [Test]
        public void TestStream() {
            var data = "Loyal Guernseycow Improbable Cheerful Indochinesetiger Able Kindly Heron";
            var bytes = Encoding.UTF8.GetBytes(data);

            var c0 = Lzf.Compress(bytes);
            var d0 = Lzf.Decompress(c0, 0, c0.Length);
            Assert.AreEqual(data, Encoding.UTF8.GetString(d0));
        }
    }
}