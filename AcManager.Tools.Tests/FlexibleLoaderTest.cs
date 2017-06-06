using System;
using System.Diagnostics;
using System.Threading.Tasks;
using AcManager.Tools.Helpers.Loaders;
using NUnit.Framework;

namespace AcManager.Tools.Tests {
    [TestFixture]
    public class FlexibleLoaderTest {
        [Test]
        public async Task RdTest() {
            var direct = await FlexibleLoader.LoadAsync("http://www.racedepartment.com/downloads/lut-generator-for-ac.9740/");
            Console.WriteLine(direct);
            Assert.NotNull(direct);
        }
    }
}