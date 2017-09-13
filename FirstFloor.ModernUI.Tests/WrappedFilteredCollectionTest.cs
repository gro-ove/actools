using System.Diagnostics;
using System.Globalization;
using NUnit.Framework;

namespace FirstFloor.ModernUI.Tests {
    [TestFixture]
    public class WrappedFilteredCollectionTest {
        public class Obj {
            public int Value;

            public Obj(int value) {
                Value = value;
            }

            public override string ToString() {
                return Value.ToString(CultureInfo.InvariantCulture);
            }
        }

        [Test]
        public void RefreshTest() {
            var array = new BetterObservableCollection<Obj> {
                new Obj(1), new Obj(2), new Obj(3), new Obj(4)
            };

            var wrapped = WrappedFilteredCollection.Create(array, s => new Obj(s.Value * 10), s => s.Value % 2 == 0);
            var second = WrappedFilteredCollection.Create(wrapped, s => new Obj(-s.Value), s => s.Value > 20);

            Assert.AreEqual("-40", string.Join(",", second));

            array[0].Value = 6;
            wrapped.Refresh(array[0]);

            Assert.AreEqual("-60,-40", string.Join(",", second));

            array[1].Value = 0;
            array[3].Value = 8;
            wrapped.Refresh(array[1]);
            wrapped.Refresh(array[3]);

            Assert.AreEqual("-60,-80", string.Join(",", second));

            array[3].Value = -1;
            wrapped.Refresh(array[3]);

            Assert.AreEqual("-60", string.Join(",", second));
        }

        [Test]
        public void Test() {
            var array = new BetterObservableCollection<string> {
                "Cat", "Dog", "Rat"
            };

            var wrapped = WrappedFilteredCollection.Create(array, s => "Big " + s, s => s.EndsWith("t") || s.EndsWith("e"));
            var second = WrappedFilteredCollection.Create(wrapped, s => s.Replace("Big", "Small"), s => s.EndsWith("t"));

            array.Add("Mouse");

            Debug.WriteLine(string.Join(", ", array));
            Debug.WriteLine(string.Join(", ", wrapped));
            Debug.WriteLine(string.Join(", ", second));

            Assert.AreEqual("Big Cat", wrapped[0]);
            Assert.AreEqual("Big Rat", wrapped[1]);
            Assert.AreEqual("Big Mouse", wrapped[2]);
            Assert.AreEqual("Small Cat", second[0]);
            Assert.AreEqual("Small Rat", second[1]);
            Assert.AreEqual(3, wrapped.Count);
            Assert.AreEqual(2, second.Count);

            array.Add("Moose");
            Debug.WriteLine(string.Join(", ", array));
            Debug.WriteLine(string.Join(", ", wrapped));
            Debug.WriteLine(string.Join(", ", second));

            Assert.AreEqual("Big Moose", wrapped[3]);
            Assert.AreEqual(2, second.Count);

            /*array.Insert(1, "Mole");
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
            Assert.AreEqual("Small Alien", second[1]);*/
        }
    }
}