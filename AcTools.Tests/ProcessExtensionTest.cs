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

        [TestInitialize]
        public void Initialize() {
            var startInfo = new ProcessStartInfo("notepad") { Verb = "runas" };
            _notepad = Process.Start(startInfo);
        }

        [TestCleanup]
        public void CleanUp() {
            try {
                _notepad.Kill();
            } catch (InvalidOperationException) { }
            _notepad.Dispose();
        }

        [TestMethod]
        public async Task EnsureAccessIsDenied() {
            // Even though notepad is an elevated process no exception will be thrown
            Assert.IsFalse(_notepad.HasExited);

            var notepadById = Process.GetProcessById(_notepad.Id);

            bool thrown;
            try {
                // Exception is thrown.
                Assert.IsFalse(notepadById.HasExited);
                thrown = false;
            } catch (Win32Exception e) when(e.Message.Contains("Access is denied")) {
                thrown = true;
            }

            Assert.IsTrue(thrown);

            await notepadById.WaitForExitAsync();
        }

        [TestMethod]
        public async Task Workaround() {
            var notepadById = Process.GetProcessById(_notepad.Id);

            bool thrown;
            try {
                // Exception is thrown.
                Assert.IsFalse(notepadById.HasExitedSafe());
                thrown = false;
            } catch (Win32Exception e) when(e.Message.Contains("Access is denied")) {
                thrown = true;
            }

            Assert.IsFalse(thrown);

            await notepadById.WaitForExitAsync();
        }
    }
}