using System.Collections.Generic;
using System.Linq;
using FirstFloor.ModernUI.Helpers;
using NUnit.Framework;

namespace FirstFloor.ModernUI.Tests {
    [TestFixture]
    public class ValuesStorageTest {
        [Test]
        public void TestEncryption() {
            ValuesStorage.Initialize(null, "Superior Binturong Sizzling Wry Drake Mediumblue Dizzy Merganser Grateful Flyingsquirrel");
            var str = "Wretched Angora Pretty Northernhairynosedwombat Offbeat Skeletal Turaco";
            var key = "Miniature Curassow Descriptive Terrapin Descriptive Terrapin Swift Brown Spreadwing";


            ValuesStorage.SetEncrypted(key, str);
            var result = ValuesStorage.GetEncrypted<string>(key);


            Assert.AreEqual(str, result, "Encryption test failed");
        }

        [Test]
        public void TestSubstorage() {
            var storage = new Storage();
            var sub = new Substorage(storage, "sub/");

            storage.Set("0", 10);
            storage.Set("1", 20);

            sub.Set("a", 18);
            Assert.AreEqual(18, storage.Get<int>("sub/a"), "Wrong value");

            storage.Set("2", 30);

            sub.Set("b", "qwerty");
            Assert.IsTrue(sub.Keys.SequenceEqual(new[] { "a", "b" }));

            using (var e = sub.GetEnumerator()) {
                Assert.AreEqual(e.MoveNext() ? e.Current : default(KeyValuePair<string, string>), new KeyValuePair<string, string>("a", "18"), "Wrong value");
                Assert.AreEqual(e.MoveNext() ? e.Current : default(KeyValuePair<string, string>), new KeyValuePair<string, string>("b", "qwerty"), "Wrong value");
                Assert.IsFalse(e.MoveNext());
            }

            Assert.IsTrue(storage.Keys.SequenceEqual(new[] { "0", "1", "sub/a", "2", "sub/b" }));
        }
    }
}
