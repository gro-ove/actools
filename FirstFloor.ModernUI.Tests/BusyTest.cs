using NUnit.Framework;

#if DEBUG
using System.Threading.Tasks;
using FirstFloor.ModernUI.Helpers;
#endif

namespace FirstFloor.ModernUI.Tests {
    [TestFixture]
    public class BusyTest {
#if DEBUG
        [Test]
        public async Task Test() {
            var busy = new Busy();
            var value = 0;
            busy.DoDelay(() => value++, 10).Ignore();
            busy.Do(() => value++);
            Assert.AreEqual(0, value);
            await Task.Delay(100);
            Assert.AreEqual(1, value);
        }
#endif
    }
}