using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using AcTools.Utils.Helpers;
using NUnit.Framework;

namespace AcTools.Tests {
    /// <summary>
    /// This is a weird test. If process doesn’t have an access to another process, it can’t even check if
    /// the another one is still running or wait for it to finish. And it might not have an access not just
    /// if another process is running with administrator privilegies, but also, sometimes, when platform is
    /// different (x32/x64). There are workarounds and they are tested here, on a notepad process running
    /// with administrator privilegies.
    /// </summary>

    [TestFixture]
    public class ProcessExtensionTest {
        private static Process _notepad;

        [OneTimeSetUp]
        public static void Initialize() {
            var startInfo = new ProcessStartInfo("notepad") { Verb = "runas" };
            _notepad = Process.Start(startInfo);
        }

        [OneTimeTearDown]
        public static void CleanUp() {
            try {
                _notepad.Kill();
            } catch (InvalidOperationException) { }
            _notepad.Dispose();
            _notepad = null;
        }

        [Test]
        public void EnsureAccessIsDenied() {
            // Even though notepad is an elevated process no exception will be thrown
            Assert.IsFalse(_notepad.HasExited);

            // Exception is thrown
            Assert.That(() => {
                var notepadById = Process.GetProcessById(_notepad.Id);
                Assert.IsFalse(notepadById.HasExited);
            }, Throws.TypeOf<Win32Exception>());
        }

        [Test]
        public void HasExitedWorkaround() {
            // Exception should not be thrown.
            var notepadById = Process.GetProcessById(_notepad.Id);
            Assert.IsFalse(notepadById.HasExitedSafe());
        }

        [Test]
        public async Task WaitForExitAsyncTest() {
            var notepadById = Process.GetProcessById(_notepad.Id);
            await notepadById.WaitForExitAsync();
        }
    }
}