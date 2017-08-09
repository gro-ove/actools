using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Threading;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Lists;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using NUnit.Framework;

namespace AcManager.Tools.Tests {
    [TestFixture]
    public class AcEnabledOnlyCollectionTest {
        public class AcFakeObject : AcObjectNew {
            public AcFakeObject(string id, bool enabled) : base(null, id, enabled) { }
            public override void Load() {}
        }

        private class FakeLoader : IAcWrapperLoader {
            public void Load(string id) {
                throw new NotSupportedException();
            }

            public Task LoadAsync(string id) {
                throw new NotSupportedException();
            }
        }

        [Test, Apartment(ApartmentState.STA)]
        public void ListBoxTest() {
            Dispatcher.CurrentDispatcher.BeginInvoke((Action)(async () => {
                ListBox listBox;

                {
                    var b = new AcWrapperObservableCollection();
                    var e = new AcEnabledOnlyCollection<AcFakeObject>(b);
                    listBox = new ListBox { ItemsSource = new BetterListCollectionView(e) };
                    b.Add(new AcItemWrapper(new AcFakeObject("q", true)));
                }

                {
                    var b = new AcWrapperObservableCollection();
                    var e = new AcEnabledOnlyCollection<AcFakeObject>(b);

                    listBox.ItemsSource = new BetterListCollectionView(e);
                    b.Add(new AcItemWrapper(new AcFakeObject("a", true)));
                    b.Add(new AcItemWrapper(new AcFakeObject("q", true)));
                    await Task.Delay(10);
                    b.Add(new AcItemWrapper(new AcFakeObject("b", true)));
                    await Task.Delay(1);
                    b.Add(new AcItemWrapper(new AcFakeObject("c", false)));
                    Assert.AreEqual("a,q,b", listBox.Items.OfType<AcObjectNew>().Select(x => x.Id).JoinToString(','));

                    Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Normal);
                }
            }));

            Dispatcher.Run();
        }

        [Test]
        public async Task Test() {
            var b = new AcWrapperObservableCollection();
            var e = new AcEnabledOnlyCollection<AcFakeObject>(b);

            // First run
            b.Add(new AcItemWrapper(new AcFakeObject("a", true)));
            await Task.Delay(1);
            b.Add(new AcItemWrapper(new AcFakeObject("b", true)));
            b.Add(new AcItemWrapper(new AcFakeObject("c", false)));
            Assert.AreEqual("a,b", e.Select(x => x.Id).JoinToString(','));

            // Loading
            var w = new AcItemWrapper(new FakeLoader(), new AcPlaceholderNew("a", true));
            b.Add(w);
            Assert.AreEqual("a,b", e.Select(x => x.Id).JoinToString(','));
            w.Value = new AcFakeObject("L", true);
            Assert.AreEqual("a,b,L", e.Select(x => x.Id).JoinToString(','));

            // Removal
            b.RemoveAt(1);
            b.RemoveAt(2);
            await Task.Delay(1);
            b.Add(new AcItemWrapper(new AcFakeObject("q", true)));
            Assert.AreEqual("a,q", e.Select(x => x.Id).JoinToString(','));

            // Replacement
            b.Replace(b.First(x => x.Id == "a"), new AcItemWrapper(new AcFakeObject("a", false)));
            b.Replace(b.First(x => x.Id == "c"), new AcItemWrapper(new AcFakeObject("c", true)));
            Assert.AreEqual("c,q", e.Select(x => x.Id).JoinToString(','));

            // Insert
            b.Insert(0, new AcItemWrapper(new AcFakeObject("m", true)));
            Assert.AreEqual("m,c,q", e.Select(x => x.Id).JoinToString(','));
        }
    }
}