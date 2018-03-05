using System.Reflection;
using System.Windows;
using System.Windows.Forms.Integration;
using JetBrains.Annotations;

namespace AcManager.Controls.UserControls.Cef {
    internal static class WindowsFormsExtension {
        [CanBeNull]
        public static Window FindParentWindow(this System.Windows.Forms.Control control) {
            var host = FindWpfHost(control);
            return host == null ? null : Window.GetWindow(host);
        }

        [CanBeNull]
        public static WindowsFormsHost FindWpfHost(this System.Windows.Forms.Control control) {
            while (control.Parent != null) {
                control = control.Parent;
            }

            const string typeName = "System.Windows.Forms.Integration.WinFormsAdapter";
            if (control.GetType().FullName != typeName) return null;

            var adapterAssembly = control.GetType().Assembly;
            var winFormsAdapterType = adapterAssembly.GetType(typeName);
            return (WindowsFormsHost)winFormsAdapterType.InvokeMember("_host",
                    BindingFlags.NonPublic | BindingFlags.GetField | BindingFlags.Instance, null, control, new object[0], null);
        }
    }
}