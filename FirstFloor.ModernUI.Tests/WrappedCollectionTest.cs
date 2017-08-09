using System.Diagnostics;
using NUnit.Framework;

namespace FirstFloor.ModernUI.Tests {
    [TestFixture]
    public class WrappedCollectionTest {
        [Test]
        public void Test() {
            var array = new BetterObservableCollection<string> {
                "Cat", "Dog", "Rat"
            };

            var wrapped = WrappedCollection.Create(array, s => "Big " + s);
            var second = WrappedCollection.Create(wrapped, s => s.Replace("Big", "Small"));

            array.Add("Mouse");

            Debug.WriteLine(string.Join(", ", array));
            Debug.WriteLine(string.Join(", ", wrapped));
            Debug.WriteLine(string.Join(", ", second));

            Assert.AreEqual("Big Cat", wrapped[0]);
            Assert.AreEqual("Big Rat", wrapped[2]);
            Assert.AreEqual("Small Cat", second[0]);
            Assert.AreEqual("Small Rat", second[2]);
            Assert.AreEqual(4, wrapped.Count);

            array.Add("Moose");
            Debug.WriteLine(string.Join(", ", array));
            Debug.WriteLine(string.Join(", ", wrapped));
            Debug.WriteLine(string.Join(", ", second));

            Assert.AreEqual("Big Moose", wrapped[4]);
            Assert.AreEqual("Small Moose", second[4]);

            array.Insert(1, "Mole");
            Debug.WriteLine(string.Join(", ", array));
            Debug.WriteLine(string.Join(", ", wrapped));
            Debug.WriteLine(string.Join(", ", second));

            Assert.AreEqual("Big Mole", wrapped[1]);
            Assert.AreEqual("Big Dog", wrapped[2]);
            Assert.AreEqual("Small Mole", second[1]);
            Assert.AreEqual("Small Dog", second[2]);

            array.Remove("Mouse");
            Assert.AreEqual(5, wrapped.Count);
            Assert.AreEqual(5, second.Count);

            array.ReplaceEverythingBy(new[] {
                "Human", "Alien"
            });
            Assert.AreEqual("Big Human", wrapped[0]);
            Assert.AreEqual("Big Alien", wrapped[1]);
            Assert.AreEqual("Small Human", second[0]);
            Assert.AreEqual("Small Alien", second[1]);
        }
    }
}