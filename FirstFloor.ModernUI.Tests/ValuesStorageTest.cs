using System.Collections.Generic;
using System.Linq;
using FirstFloor.ModernUI.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FirstFloor.ModernUI.Tests {
    [TestClass]
    public class ValuesStorageTest {
        [TestMethod]
        public void TestEncryption() {
            ValuesStorage.Initialize(null, "Superior Binturong Sizzling Wry Drake Mediumblue Dizzy Merganser Grateful Flyingsquirrel");
            var str = "Wretched Angora Pretty Northernhairynosedwombat Offbeat Skeletal Turaco";
            var key = "Miniature Curassow Descriptive Terrapin Descriptive Terrapin Swift Brown Spreadwing";


            ValuesStorage.SetEncrypted(key, str);
            var result = ValuesStorage.GetEncryptedString(key);


            Assert.AreEqual(str, result, "Encryption test failed");
        }

        [TestMethod]
        public void TestSubstorage() {
            var storage = new Storage();
            var sub = new Substorage(storage, "sub/");

            StorageMethods.Set(storage, "0", 10);
            StorageMethods.Set(storage, "1", 20);

            StorageMethods.Set(sub, "a", 18);
            Assert.AreEqual(18, storage.GetInt("sub/a"), "Wrong value");

            StorageMethods.Set(storage, "2", 30);

            StorageMethods.Set(sub, "b", "qwerty");
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
