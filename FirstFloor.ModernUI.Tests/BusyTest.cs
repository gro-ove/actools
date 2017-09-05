using System.Threading.Tasks;
using FirstFloor.ModernUI.Helpers;
using NUnit.Framework;
using Assert = Microsoft.VisualStudio.TestTools.UnitTesting.Assert;

namespace FirstFloor.ModernUI.Tests {
    [TestFixture]
    public class BusyTest {
        [Test]
        public async Task Test() {
            var busy = new Busy();
            var value = 0;
            busy.DoDelay(() => value++, 10);
            busy.Do(() => value++);
            Assert.AreEqual(0, value);
            await Task.Delay(100);
            Assert.AreEqual(1, value);
        }
    }
}