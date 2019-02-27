using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace AcManager.CustomShowroom {
    /// <summary>
    /// Extensions for <see cref="System.Windows.Window"/>.
    /// </summary>
    public static class WindowExtensions {
        #region Win32
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        /// <summary>
        /// RECT struct for platform invokation.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT {
            /// <summary>
            /// Left.
            /// </summary>
            public int Left;

            /// <summary>
            /// Top.
            /// </summary>
            public int Top;

            /// <summary>
            /// Right.
            /// </summary>
            public int Right;

            /// <summary>
            /// Bottom.
            /// </summary>
            public int Bottom;
        }
        #endregion

        #region Current process main window
        /// <summary>
        /// Sets the owner window to the main window of the current process.
        /// </summary>
        /// <param name="window">Reference to the current window.</param>
        public static void SetOwnerWindow(this Window window) {
            // Set new owner window without forcing
            SetOwnerWindow(window, GetProcessMainWindowHandle(), false, true);
        }

        /// <summary>
        /// Sets the owner window to the main window of the current process, but
        /// also sets the focus on the first control.
        /// </summary>
        /// <param name="window">Reference to the current window.</param>
        public static void SetOwnerWindowAndFocus(this Window window) {
            // Set new owner window without forcing
            SetOwnerWindow(window, GetProcessMainWindowHandle(), false, true);
        }

        /// <summary>
        /// Sets the owner window to the main window of the current process.
        /// </summary>
        /// <param name="window">Reference to the current window.</param>
        /// <param name="forceNewOwner">If true, the new owner will be forced. Otherwise, if the
        /// window currently has an owner, that owner will be respected (and thus not changed).</param>
        public static void SetOwnerWindow(this Window window, bool forceNewOwner) {
            // Set owner window to process main window without forcing
            SetOwnerWindow(window, GetProcessMainWindowHandle(), false, false);
        }

        /// <summary>
        /// Sets the owner window to the main window of the current process, but
        /// also sets the focus on the first control.
        /// </summary>
        /// <param name="window">Reference to the current window.</param>
        /// <param name="forceNewOwner">If true, the new owner will be forced. Otherwise, if the
        /// window currently has an owner, that owner will be respected (and thus not changed).</param>
        public static void SetOwnerWindowAndFocus(this Window window, bool forceNewOwner) {
            // Set owner window to process main window without forcing
            SetOwnerWindow(window, GetProcessMainWindowHandle(), false, true);
        }
        #endregion

        #region Specific window - Window
        /// <summary>
        /// Sets the owner window of a specific window via the Window class.
        /// </summary>
        /// <param name="window">Reference to the current window.</param>
        /// <param name="owner">New owner window.</param>
        public static void SetOwnerWindow(this Window window, Window owner) {
            // Set owner without forcing
            SetOwnerWindow(window, owner, false, false);
        }

        /// <summary>
        /// Sets the owner window of a specific window via the Window class, but
        /// also sets the focus on the first control.
        /// </summary>
        /// <param name="window">Reference to the current window.</param>
        /// <param name="owner">New owner window.</param>
        public static void SetOwnerWindowAndFocus(this Window window, Window owner) {
            // Set owner without forcing
            SetOwnerWindow(window, owner, false, true);
        }

        /// <summary>
        /// Sets the owner window of a specific window via the Window class.
        /// </summary>
        /// <param name="window">Reference to the current window.</param>
        /// <param name="owner">New owner window.</param>
        /// <param name="forceNewOwner">If true, the new owner will be forced. Otherwise, if the
        /// window currently has an owner, that owner will be respected (and thus not changed).</param>
        public static void SetOwnerWindow(this Window window, Window owner, bool forceNewOwner) {
            // Set owner window but do not focus
            SetOwnerWindow(window, owner, forceNewOwner, false);
        }

        /// <summary>
        /// Sets the owner window of a specific window via the Window class, but
        /// also sets the focus on the first control.
        /// </summary>
        /// <param name="window">Reference to the current window.</param>
        /// <param name="owner">New owner window.</param>
        /// <param name="forceNewOwner">If true, the new owner will be forced. Otherwise, if the
        /// window currently has an owner, that owner will be respected (and thus not changed).</param>
        public static void SetOwnerWindowAndFocus(this Window window, Window owner, bool forceNewOwner) {
            // Set owner window and focus
            SetOwnerWindow(window, owner, forceNewOwner, true);
        }

        /// <summary>
        /// Sets the owner window of a specific window via the Window class.
        /// </summary>
        /// <param name="window">Reference to the current window.</param>
        /// <param name="owner">New owner window.</param>
        /// <param name="forceNewOwner">If true, the new owner will be forced. Otherwise, if the
        /// window currently has an owner, that owner will be respected (and thus not changed).</param>
        /// <param name="focusFirstControl">If true, the first control will automatically be focused.</param>
        private static void SetOwnerWindow(this Window window, Window owner, bool forceNewOwner,
                bool focusFirstControl) {
            // Focus if required
            if (focusFirstControl) window.FocusFirstControl();

            // Check if this window currently has an owner
            if (!forceNewOwner && HasOwner(window)) return;

            // Set owner
            window.Owner = owner;
        }
        #endregion

        #region Specific window - IntPtr
        /// <summary>
        /// Sets the owner window of a specific window via the window handle.
        /// </summary>
        /// <param name="window">Reference to the current window.</param>
        /// <param name="owner">New owner window.</param>
        public static void SetOwnerWindow(this Window window, IntPtr owner) {
            // Set owner without forcing
            SetOwnerWindow(window, owner, false, false);
        }

        /// <summary>
        /// Sets the owner window of a specific window via the window handle, but
        /// also sets the focus on the first control.
        /// </summary>
        /// <param name="window">Reference to the current window.</param>
        /// <param name="owner">New owner window.</param>
        public static void SetOwnerWindowAndFocus(this Window window, IntPtr owner) {
            // Set owner without forcing
            SetOwnerWindow(window, owner, false, true);
        }

        /// <summary>
        /// Sets the owner window of a specific window via the window handle.
        /// </summary>
        /// <param name="window">Reference to the current window.</param>
        /// <param name="owner">New owner window.</param>
        /// <param name="forceNewOwner">If true, the new owner will be forced. Otherwise, if the
        /// window currently has an owner, that owner will be respected (and thus not changed).</param>
        public static void SetOwnerWindow(this Window window, IntPtr owner, bool forceNewOwner) {
            SetOwnerWindow(window, owner, forceNewOwner, false);
        }

        /// <summary>
        /// Sets the owner window of a specific window via the window handle, but
        /// also sets the focus on the first control.
        /// </summary>
        /// <param name="window">Reference to the current window.</param>
        /// <param name="owner">New owner window.</param>
        /// <param name="forceNewOwner">If true, the new owner will be forced. Otherwise, if the
        /// window currently has an owner, that owner will be respected (and thus not changed).</param>
        public static void SetOwnerWindowAndFocus(this Window window, IntPtr owner, bool forceNewOwner) {
            SetOwnerWindow(window, owner, forceNewOwner, true);
        }

        /// <summary>
        /// Sets the owner window of a specific window via the window handle.
        /// </summary>
        /// <param name="window">Reference to the current window.</param>
        /// <param name="owner">New owner window.</param>
        /// <param name="forceNewOwner">If true, the new owner will be forced. Otherwise, if the
        /// window currently has an owner, that owner will be respected (and thus not changed).</param>
        /// <param name="focusFirstControl">If true, the first control will automatically be focused.</param>
        private static void SetOwnerWindow(Window window, IntPtr owner, bool forceNewOwner,
                bool focusFirstControl) {
            // Focus if required
            if (focusFirstControl) window.FocusFirstControl();

            // Check if this window currently has an owner
            if (!forceNewOwner && HasOwner(window)) return;

            // Set owner via interop helper
            WindowInteropHelper interopHelper = new WindowInteropHelper(window);
            interopHelper.Owner = owner;

            // Since this owner type doesn't support WindowStartupLocation.CenterOwner, do
            // it manually
            if (window.WindowStartupLocation == WindowStartupLocation.CenterOwner) {
                // Subscribe to the load event
                window.Loaded += delegate(object sender, RoutedEventArgs e) {
                    // Get the parent window rect
                    RECT ownerRect;
                    if (GetWindowRect(owner, out ownerRect)) {
                        // Get some additional information
                        int ownerWidth = ownerRect.Right - ownerRect.Left;
                        int ownerHeight = ownerRect.Bottom - ownerRect.Top;
                        int ownerHorizontalCenter = (ownerWidth / 2) + ownerRect.Left;
                        int ownerVerticalCenter = (ownerHeight / 2) + ownerRect.Top;

                        // Set the location to manual
                        window.WindowStartupLocation = WindowStartupLocation.Manual;

                        // Now we know the location of the parent, center the window
                        window.Left = ownerHorizontalCenter - (window.ActualWidth / 2);
                        window.Top = ownerVerticalCenter - (window.ActualHeight / 2);
                    }
                };
            }
        }
        #endregion

        /// <summary>
        /// Returns the main window handle of the current process.
        /// </summary>
        /// <returns>Handle of the main window of the current process.</returns>
        private static IntPtr GetProcessMainWindowHandle() {
            return System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
        }

        /// <summary>
        /// Returns whether the window currently has an owner.
        /// </summary>
        /// <param name="window">Window to check.</param>
        /// <returns>True if the window has an owner, otherwise false.</returns>
        private static bool HasOwner(Window window) {
            return ((window.Owner != null) || (new WindowInteropHelper(window).Owner != IntPtr.Zero));
        }
    }
}