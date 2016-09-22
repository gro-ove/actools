using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using AcTools.Utils.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AcTools.Tests {
    [TestClass]
    public class ProcessExtensionTest {
        private static Process _notepad;

        [ClassInitialize]
        public static void Initialize(TestContext testContext) {
            var startInfo = new ProcessStartInfo("notepad") { Verb = "runas" };
            _notepad = Process.Start(startInfo);
        }

        [ClassCleanup]
        public static void CleanUp() {
            try {
                _notepad.Kill();
            } catch (InvalidOperationException) { }
            _notepad.Dispose();
        }
        
        [TestMethod, ExpectedException(typeof(Win32Exception))]
        public void EnsureAccessIsDenied() {
            // Even though notepad is an elevated process no exception will be thrown
            Assert.IsFalse(_notepad.HasExited);

            // Exception is thrown
            var notepadById = Process.GetProcessById(_notepad.Id);
            Assert.IsFalse(notepadById.HasExited);
        }

        [TestMethod]
        public void Workaround() {
            // Exception should not be thrown.
            var notepadById = Process.GetProcessById(_notepad.Id);
            Assert.IsFalse(notepadById.HasExitedSafe());
        }

        [TestMethod]
        public async Task WaitForExitAsyncTest() {
            var notepadById = Process.GetProcessById(_notepad.Id);
            await notepadById.WaitForExitAsync();
        }
    }
}