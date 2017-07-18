using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;

// ReSharper disable InconsistentNaming

namespace FirstFloor.ModernUI.Win32 {
    // Taken from http://www.codeproject.com/Articles/707502/Version-Helper-API-for-NET
    // License: The Code Project Open License

    /// <summary>
    /// .NET wrapper for Version Helper functions.
    /// http://msdn.microsoft.com/library/windows/desktop/dn424972.aspx
    /// </summary>
    [Localizable(false)]
    public static class WindowsVersionHelper {
        #region Supplementary data types
        /// <summary>
        /// Operating systems, the information which is stored within
        /// the class <seealso cref="WindowsVersionHelper"/>.
        /// </summary>
        public enum KnownWindows {
            /// <summary>
            /// Windows XP.
            /// </summary>
            [Description("Windows XP")]
            WindowsXP,

            /// <summary>
            /// Windows XP SP1.
            /// </summary>
            [Description("Windows XP SP1")]
            WindowsXPSP1,

            /// <summary>
            /// Windows XP SP2.
            /// </summary>
            [Description("Windows XP SP2")]
            WindowsXPSP2,

            /// <summary>
            /// Windows XP SP3.
            /// </summary>
            [Description("Windows XP SP3")]
            WindowsXPSP3,

            /// <summary>
            /// Windows Vista.
            /// </summary>
            [Description("Windows Vista")]
            WindowsVista,

            /// <summary>
            /// Windows Vista SP1.
            /// </summary>
            [Description("Windows Vista SP1")]
            WindowsVistaSP1,

            /// <summary>
            /// Windows Vista SP2.
            /// </summary>
            [Description("Windows Vista SP2")]
            WindowsVistaSP2,

            /// <summary>
            /// Windows 7.
            /// </summary>
            [Description("Windows 7")]
            Windows7,

            /// <summary>
            /// Windows 7 SP1.
            /// </summary>
            [Description("Windows 7 SP1")]
            Windows7SP1,

            /// <summary>
            /// Windows 8.
            /// </summary>
            [Description("Windows 8")]
            Windows8,

            /// <summary>
            /// Windows 8.1.
            /// </summary>
            [Description("Windows 8.1")]
            Windows8Point1,

            /// <summary>
            /// Windows 10.
            /// </summary>
            [Description("Windows 10")]
            Windows10
        }

        /// <summary>
        /// Information about operating system.
        /// </summary>
        private sealed class WindowsVersionEntry {
            #region Properties
            /// <summary>
            /// The major version number of the operating system.
            /// </summary>
            public uint MajorVersion { get; }

            /// <summary>
            /// The minor version number of the operating system.
            /// </summary>
            public uint MinorVersion { get; }

            /// <summary>
            /// The major version number of the latest Service Pack installed
            /// on the system. For example, for Service Pack 3, the major
            /// version number is 3. If no Service Pack has been installed,
            /// the value is zero.
            /// </summary>
            public ushort ServicePackMajor { get; }

            /// <summary>
            /// Flag indicating if the running OS matches, or is greater
            /// than, the OS specified with this entry. Should be initialized
            /// with <see cref="VerifyVersionInfo"/> method.
            /// </summary>
            public bool? MatchesOrGreater { get; set; }
            #endregion 

            #region Constructor
            /// <summary>
            /// Creates a new entry of operating system.
            /// </summary>
            /// <param name="majorVersion">The major version number of the
            /// operating system.</param>
            /// <param name="minorVersion">The minor version number of the
            /// operating system.</param>
            /// <param name="servicePackMajor">The major version number of the
            /// latest Service Pack installed on the system. For example, for
            /// Service Pack 3, the major version number is 3. If no Service
            /// Pack has been installed, the value is zero.</param>
            public WindowsVersionEntry(uint majorVersion, uint minorVersion, ushort servicePackMajor) {
                MajorVersion = majorVersion;
                MinorVersion = minorVersion;
                ServicePackMajor = servicePackMajor;
            }
            #endregion // Constructor
        }
        #endregion // Supplementary data types

        #region PInvoke data type declarations
        /// <summary>
        /// Wrapper for OSVERSIONINFOEX structure.
        /// http://msdn.microsoft.com/library/windows/desktop/ms724833.aspx
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct OsVersionInfoEx {
            /// <summary>
            /// The size of this data structure, in bytes.
            /// </summary>
            public uint OSVersionInfoSize;

            /// <summary>
            /// The major version number of the operating system.
            /// </summary>
            public uint MajorVersion;

            /// <summary>
            /// The minor version number of the operating system.
            /// </summary>
            public uint MinorVersion;

            /// <summary>
            /// The build number of the operating system.
            /// </summary>
            public uint BuildNumber;

            /// <summary>
            /// The operating system platform.
            /// </summary>
            public uint PlatformId;

            /// <summary>
            /// A null-terminated string, such as "Service Pack 3", that
            /// indicates the latest Service Pack installed on the system. If
            /// no Service Pack has been installed, the string is empty.
            /// </summary>
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string CSDVersion;

            /// <summary>
            /// The major version number of the latest Service Pack installed
            /// on the system. For example, for Service Pack 3, the major
            /// version number is 3. If no Service Pack has been installed,
            /// the value is zero.
            /// </summary>
            public ushort ServicePackMajor;

            /// <summary>
            /// The minor version number of the latest Service Pack installed
            /// on the system. For example, for Service Pack 3, the minor
            /// version number is 0.
            /// </summary>
            public ushort ServicePackMinor;

            /// <summary>
            /// A bit mask that identifies the product suites available on the
            /// system, e.g., flags indicating if the operating system is
            /// Datacenter Server or Windows XP Embedded.
            /// </summary>
            public ushort SuiteMask;

            /// <summary>
            /// Any additional information about the system, e.g., flags
            /// indicating if the operating system is a domain controller,
            /// a server or a workstation.
            /// </summary>
            public byte ProductType;

            /// <summary>
            /// Reserved for future use.
            /// </summary>
            public byte Reserved;
        }

        #endregion // PInvoke data type declarations

        #region PInvoke function declarations
        /// <summary>
        /// <para>Wrapper for VerSetConditionMask function (
        /// http://msdn.microsoft.com/library/windows/desktop/ms725493.aspx).
        /// </para>
        /// <para>
        /// Sets the bits of a 64-bit value to indicate the comparison
        /// operator to use for a specified operating system version
        /// attribute. This method is used to build the dwlConditionMask
        /// parameter of the <see cref="VerifyVersionInfo"/> method.
        /// </para>
        /// </summary>
        /// <param name="dwlConditionMask">
        /// <para>A value to be passed as the dwlConditionMask parameter of
        /// the <see cref="VerifyVersionInfo"/> method. The function stores
        /// the comparison information in the bits of this variable.
        /// </para>
        /// <para>
        /// Before the first call to VerSetConditionMask, initialize this
        /// variable to zero. For subsequent calls, pass in the variable used
        /// in the previous call.
        /// </para>
        /// </param>
        /// <param name="dwTypeBitMask">A mask that indicates the member of
        /// the <see cref="OsVersionInfoEx"/> structure whose comparison
        /// operator is being set.</param>
        /// <param name="dwConditionMask">The operator to be used for the
        /// comparison.</param>
        /// <returns>Condition mask value.</returns>
        [DllImport("kernel32.dll")]
        private static extern ulong VerSetConditionMask(ulong dwlConditionMask,
                uint dwTypeBitMask, byte dwConditionMask);

        /// <summary>
        /// <para>
        /// Wrapper for VerifyVersionInfo function (
        /// http://msdn.microsoft.com/library/windows/desktop/ms725492.aspx).
        /// </para>
        /// <para>
        /// Compares a set of operating system version requirements to the
        /// corresponding values for the currently running version of the
        /// system.
        /// </para>
        /// </summary>
        /// <param name="lpVersionInfo">A pointer to an
        /// <see cref="OsVersionInfoEx"/> structure containing the operating
        /// system version requirements to compare.</param>
        /// <param name="dwTypeMask">A mask that indicates the members of the
        /// <see cref="OsVersionInfoEx"/> structure to be tested.</param>
        /// <param name="dwlConditionMask">The type of comparison to be used
        /// for each lpVersionInfo member being compared. Can be constructed
        /// with <see cref="VerSetConditionMask"/> method.</param>
        /// <returns>True if the current Windows OS satisfies the specified
        /// requirements; otherwise, false.</returns>
        [DllImport("kernel32.dll")]
        private static extern bool VerifyVersionInfo([In] ref OsVersionInfoEx lpVersionInfo, uint dwTypeMask, ulong dwlConditionMask);
        #endregion // PInvoke declarations

        #region Local fields
        private static readonly Dictionary<KnownWindows, WindowsVersionEntry> WindowsEntries;
        private static bool? _isServer = null;

        private static ulong? _versionOrGreaterMask;
        private static uint? _versionOrGreaterTypeMask;
        #endregion // Local fields

        #region Constructor
        /// <summary>
        /// Initializes dictionary of operating systems.
        /// </summary>
        static WindowsVersionHelper() {
            WindowsEntries = new Dictionary<KnownWindows, WindowsVersionEntry> {
                { KnownWindows.WindowsXP, new WindowsVersionEntry(5, 1, 0) },
                { KnownWindows.WindowsXPSP1, new WindowsVersionEntry(5, 1, 1) },
                { KnownWindows.WindowsXPSP2, new WindowsVersionEntry(5, 1, 2) },
                { KnownWindows.WindowsXPSP3, new WindowsVersionEntry(5, 1, 3) },
                { KnownWindows.WindowsVista, new WindowsVersionEntry(6, 0, 0) },
                { KnownWindows.WindowsVistaSP1, new WindowsVersionEntry(6, 0, 1) },
                { KnownWindows.WindowsVistaSP2, new WindowsVersionEntry(6, 0, 2) },
                { KnownWindows.Windows7, new WindowsVersionEntry(6, 1, 0) },
                { KnownWindows.Windows7SP1, new WindowsVersionEntry(6, 1, 1) },
                { KnownWindows.Windows8, new WindowsVersionEntry(6, 2, 0) },
                { KnownWindows.Windows8Point1, new WindowsVersionEntry(6, 3, 0) },
                { KnownWindows.Windows10, new WindowsVersionEntry(10, 0, 0) }
            };
        }
        #endregion

        #region Public methods
        /// <summary>
        /// Indicates if the current OS version matches, or is greater than,
        /// the provided version information. This method is useful in
        /// confirming a version of Windows Server that doesn’t share a
        /// version number with a client release.
        /// </summary>
        /// <param name="majorVersion">The major OS version number.</param>
        /// <param name="minorVersion">The minor OS version number.</param>
        /// <param name="servicePackMajor">The major Service Pack version
        /// number.</param>
        /// <returns>True if the the running OS matches, or is greater
        /// than, the specified version information; otherwise, false.
        /// </returns>
        private static bool IsWindowsVersionOrGreater(uint majorVersion, uint minorVersion, ushort servicePackMajor) {
            var osvi = new OsVersionInfoEx();
            osvi.OSVersionInfoSize = (uint)Marshal.SizeOf(osvi);
            osvi.MajorVersion = majorVersion;
            osvi.MinorVersion = minorVersion;
            osvi.ServicePackMajor = servicePackMajor;

            // These constants initialized with corresponding definitions in
            // winnt.h (part of Windows SDK)
            const uint verMinor = 0x0000001;
            const uint verMajor = 0x0000002;
            const uint verServicePack = 0x0000020;
            const byte verGreaterEqual = 3;

            if (!_versionOrGreaterMask.HasValue) {
                _versionOrGreaterMask = VerSetConditionMask(
                        VerSetConditionMask(
                                VerSetConditionMask(
                                        0, verMajor, verGreaterEqual),
                                verMinor, verGreaterEqual),
                        verServicePack, verGreaterEqual);
            }

            if (!_versionOrGreaterTypeMask.HasValue) {
                _versionOrGreaterTypeMask = verMajor |
                        verMinor | verServicePack;
            }

            return VerifyVersionInfo(ref osvi, _versionOrGreaterTypeMask.Value,
                    _versionOrGreaterMask.Value);
        }

        /// <summary>
        /// Indicates if the running OS version matches, or is greater than,
        /// the provided OS.
        /// </summary>
        /// <param name="windows">OS to compare running OS to.</param>
        /// <returns>True if the the running OS matches, or is greater
        /// than, the specified OS; otherwise, false.</returns>
        public static bool IsWindowsVersionOrGreater(KnownWindows windows) {
            try {
                var entry = WindowsEntries[windows];
                if (!entry.MatchesOrGreater.HasValue) {
                    entry.MatchesOrGreater = IsWindowsVersionOrGreater(
                            entry.MajorVersion, entry.MinorVersion,
                            entry.ServicePackMajor);
                }
                
                return entry.MatchesOrGreater.Value;
            } catch (KeyNotFoundException e) {
                throw new ArgumentException(UiStrings.UnknownOS, e);
            } catch (MissingMethodException) {
                if (MessageBox.Show("Looks like you don’t have .NET 4.5.2 installed. Would you like to install it?", "Error",
                        MessageBoxButton.YesNo, MessageBoxImage.Asterisk) == MessageBoxResult.Yes) {
                    Process.Start("http://www.microsoft.com/en-us/download/details.aspx?id=42642");
                }

                var app = Application.Current;
                if (app == null) {
                    Environment.Exit(10);
                } else {
                    app.Shutdown(10);
                }

                return false;
            }
        }
        #endregion

        #region Public properties
        /// <summary>
        /// Indicates if the current OS version matches, or is greater than,
        /// the Windows XP version.
        /// </summary>
        public static bool IsWindowsXPOrGreater => IsWindowsVersionOrGreater(KnownWindows.WindowsXP);

        /// <summary>
        /// Indicates if the current OS version matches, or is greater than,
        /// the Windows XP with Service Pack 1 (SP1) version.
        /// </summary>
        public static bool IsWindowsXPSP1OrGreater => IsWindowsVersionOrGreater(KnownWindows.WindowsXPSP1);

        /// <summary>
        /// Indicates if the current OS version matches, or is greater than,
        /// the Windows XP with Service Pack 2 (SP2) version.
        /// </summary>
        public static bool IsWindowsXPSP2OrGreater => IsWindowsVersionOrGreater(KnownWindows.WindowsXPSP2);

        /// <summary>
        /// Indicates if the current OS version matches, or is greater than,
        /// the Windows XP with Service Pack 3 (SP3) version.
        /// </summary>
        public static bool IsWindowsXPSP3OrGreater => IsWindowsVersionOrGreater(KnownWindows.WindowsXPSP3);

        /// <summary>
        /// Indicates if the current OS version matches, or is greater than,
        /// the Windows Vista version.
        /// </summary>
        public static bool IsWindowsVistaOrGreater => IsWindowsVersionOrGreater(KnownWindows.WindowsVista);

        /// <summary>
        /// Indicates if the current OS version matches, or is greater than,
        /// the Windows Vista with Service Pack 1 (SP1) version.
        /// </summary>
        public static bool IsWindowsVistaSP1OrGreater => IsWindowsVersionOrGreater(KnownWindows.WindowsVistaSP1);

        /// <summary>
        /// Indicates if the current OS version matches, or is greater than,
        /// the Windows Vista with Service Pack 2 (SP2) version.
        /// </summary>
        public static bool IsWindowsVistaSP2OrGreater => IsWindowsVersionOrGreater(KnownWindows.WindowsVistaSP2);

        /// <summary>
        /// Indicates if the current OS version matches, or is greater than,
        /// the Windows 7 version.
        /// </summary>
        public static bool IsWindows7OrGreater => IsWindowsVersionOrGreater(KnownWindows.Windows7);

        /// <summary>
        /// Indicates if the current OS version matches, or is greater than,
        /// the Windows 7 with Service Pack 1 (SP1) version.
        /// </summary>
        public static bool IsWindows7SP1OrGreater => IsWindowsVersionOrGreater(KnownWindows.Windows7SP1);

        /// <summary>
        /// Indicates if the current OS version matches, or is greater than,
        /// the Windows 8 version.
        /// </summary>
        public static bool IsWindows8OrGreater => IsWindowsVersionOrGreater(KnownWindows.Windows8);

        /// <summary>
        /// Indicates if the current OS version matches, or is greater than,
        /// the Windows 8.1 version.
        /// </summary>
        public static bool IsWindows8Point1OrGreater => IsWindowsVersionOrGreater(KnownWindows.Windows8Point1);

        /// <summary>
        /// Indicates if the current OS version matches, or is greater than,
        /// the Windows 10 version.
        /// </summary>
        public static bool IsWindows10OrGreater => IsWindowsVersionOrGreater(KnownWindows.Windows10);

        private static string _version;

        public static string GetVersion() {
            if (_version != null) return _version;

            foreach (var k in WindowsEntries.Reverse().Select(x => x.Key)) {
                if (IsWindowsVersionOrGreater(k)) {
                    _version = GetDescription(k);
                    return _version;
                }
            }

            _version = "Unknown System";
            return _version;
        }

        private static string GetDescription(Enum value) {
            var type = value.GetType();
            var name = Enum.GetName(type, value);
            if (name == null) return null;
            var field = type.GetField(name);
            return field == null ? null :
                    (Attribute.GetCustomAttribute(field, typeof(DescriptionAttribute)) as DescriptionAttribute)?.Description;
        }

        /// <summary>
        /// Indicates if the current OS is a Windows Server release.
        /// </summary>
        public static bool IsWindowsServer {
            get {
                if (!_isServer.HasValue) {
                    // These constants initialized with corresponding
                    // definitions in winnt.h (part of Windows SDK)
                    const byte verNtWorkstation = 0x0000001;
                    const uint verProductType = 0x0000080;
                    const byte verEqual = 1;

                    var osvi = new OsVersionInfoEx();
                    osvi.OSVersionInfoSize = (uint)Marshal.SizeOf(osvi);
                    osvi.ProductType = verNtWorkstation;
                    var dwlConditionMask = VerSetConditionMask(
                            0, verProductType, verEqual);

                    return !VerifyVersionInfo(
                            ref osvi, verProductType, dwlConditionMask);
                }

                return _isServer.Value;
            }
        }
        #endregion
    }
}
