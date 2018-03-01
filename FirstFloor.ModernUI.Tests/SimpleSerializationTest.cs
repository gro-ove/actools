using System;
using FirstFloor.ModernUI.Serialization;
using NUnit.Framework;

namespace FirstFloor.ModernUI.Tests {
    [TestFixture]
    public class SimpleSerializationTest {
        [Test]
        public void BoolTest() {
            Assert.AreEqual(true, "1".As<bool>());
            Assert.AreEqual(true, "1".As<bool?>());
            Assert.AreEqual(true, ((bool?)true).As<bool?>());
            Assert.AreEqual(true, "true".As<bool?>());
            Assert.AreEqual(false, "false".As<bool?>());
            Assert.AreEqual(false, "0".As<bool?>());
            Assert.AreEqual(null, ((object)null).As<bool?>());

            var date = DateTime.Parse("03/01/2018 03:37:18");
            Assert.AreEqual("03/01/2018 03:37:18", date.As<string>());
            Assert.IsTrue(date.As<string>().As<DateTime>() == date);
        }
    }
}