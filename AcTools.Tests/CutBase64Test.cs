using System.Linq;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using NUnit.Framework;

namespace AcTools.Tests {
    [TestFixture]
    public class CutBase64Test {
        [Test]
        public void Test() {
            foreach (var v in Enumerable.Range(0, 1000).Select(x => StringExtension.RandomString(MathUtils.Random(5, 45)))) {
                Assert.AreEqual(v, v.ToCutBase64().FromCutBase64());
            }
        }
    }
}