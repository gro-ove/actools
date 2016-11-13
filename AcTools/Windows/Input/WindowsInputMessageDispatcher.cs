using System;
using System.Runtime.InteropServices;
using AcTools.Windows.Input.Native;

namespace AcTools.Windows.Input {
    internal class WindowsInputMessageDispatcher : IInputMessageDispatcher {
        public void DispatchInput(InputEntry[] inputs) {
            if (inputs == null) throw new ArgumentNullException(nameof(inputs));
            if (inputs.Length == 0) throw new ArgumentException(@"The input array was empty", nameof(inputs));
            var successful = NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(InputEntry)));
            if (successful != inputs.Length) {
                throw new Exception(
                        "Some simulated input commands were not sent successfully. The most common reason for this happening are the security features of Windows including User Interface Privacy Isolation (UIPI). Your application can only send commands to applications of the same or lower elevation. Similarly certain commands are restricted to Accessibility/UIAutomation applications. Refer to the project home page and the code samples for more information.");
            }
        }
    }
}