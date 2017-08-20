using System.Threading.Tasks;
using AcTools.Utils;
using NUnit.Framework;

namespace AcTools.Tests {
    [TestFixture]
    public class LazierTest {
        [Test]
        public void TestBasic() {
            var i = 0;
            var l = Lazier.Create(() => ++i);

            Assert.IsFalse(l.IsSet);
            Assert.AreEqual(1, l.Value);
            Assert.IsTrue(l.IsSet);
            Assert.AreEqual(1, l.Value);
            l.Reset();
            Assert.IsFalse(l.IsSet);
            Assert.AreEqual(2, l.Value);
            Assert.IsTrue(l.IsSet);
            Assert.AreEqual(2, l.Value);
            l.Reset();
            Assert.AreEqual(3, l.Value);
        }

        [Test]
        public async Task TestAsync() {
            var i = 0;
            var l = Lazier.CreateAsync(async () => {
                var v = ++i;
                await Task.Delay(25);
                return v;
            }, -1);

            Assert.IsFalse(l.IsSet);
            Assert.AreEqual(-1, l.Value);
            Assert.AreEqual(1, i);
            Assert.IsFalse(l.IsSet);
            await Task.Delay(1);
            Assert.AreEqual(1, i);
            Assert.AreEqual(-1, l.Value);
            Assert.IsFalse(l.IsSet);
            await Task.Delay(50);
            Assert.AreEqual(1, l.Value);
            Assert.IsTrue(l.IsSet);
            Assert.AreEqual(1, l.Value);
            l.Reset();



            Assert.IsFalse(l.IsSet);
            Assert.AreEqual(-1, l.Value);
            Assert.AreEqual(2, i);
            await Task.Delay(50);
            Assert.AreEqual(2, l.Value);
            Assert.IsTrue(l.IsSet);
            Assert.AreEqual(2, l.Value);
            l.Reset();


            Assert.AreEqual(-1, l.Value);
            Assert.AreEqual(3, i);
            await Task.Delay(15);
            l.Reset();
            Assert.AreEqual(-1, l.Value);
            Assert.AreEqual(4, i);
            await Task.Delay(50);
            Assert.AreEqual(4, l.Value);
        }
    }
}