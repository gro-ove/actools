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
    }
}
