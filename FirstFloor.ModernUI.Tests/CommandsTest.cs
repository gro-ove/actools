using System.Windows.Input;
using FirstFloor.ModernUI.Commands;
using NUnit.Framework;

namespace FirstFloor.ModernUI.Tests {
    [TestFixture]
    public class CommandsTest {
        [Test]
        public void TestTypes() {
            string s = null;

            {
                var cmd = (ICommand)new DelegateCommand<bool>(b => { s = b.ToString(); }) {
                    XamlCompatible = false
                };

                Assert.IsFalse(cmd.CanExecute(null));
                Assert.IsFalse(cmd.CanExecute("test"));
                Assert.IsFalse(cmd.CanExecute("true"));
                Assert.IsFalse(cmd.CanExecute("True"));
                Assert.IsTrue(cmd.CanExecute(true));

                cmd.Execute(true);
                Assert.AreEqual("True", s);

                cmd.Execute(null);
                Assert.AreEqual("True", s);
            }

            {
                var cmd = (ICommand)new DelegateCommand<bool>(b => { s = b.ToString(); }) {
                    XamlCompatible = true
                };

                Assert.IsFalse(cmd.CanExecute(null));
                Assert.IsFalse(cmd.CanExecute("test"));
                Assert.IsTrue(cmd.CanExecute("true"));
                Assert.IsTrue(cmd.CanExecute("True"));
                Assert.IsTrue(cmd.CanExecute(true));

                cmd.Execute(true);
                Assert.AreEqual("True", s);

                cmd.Execute(null);
                Assert.AreEqual("True", s);
            }

            {
                var cmd = (ICommand)new DelegateCommand<bool?>(b => { s = b.ToString(); }) {
                    XamlCompatible = true
                };

                Assert.IsTrue(cmd.CanExecute(null));
                Assert.IsFalse(cmd.CanExecute("test"));
                Assert.IsTrue(cmd.CanExecute("true"));
                Assert.IsTrue(cmd.CanExecute("True"));
                Assert.IsTrue(cmd.CanExecute(true));

                cmd.Execute(true);
                Assert.AreEqual("True", s);

                cmd.Execute(null);
                Assert.AreEqual("", s);
            }
        }
    }
}