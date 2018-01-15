using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using JetBrains.Annotations;
using ComTypes = System.Runtime.InteropServices.ComTypes;
// ReSharper disable SuspiciousTypeConversion.Global

namespace FirstFloor.ModernUI.Windows {
    // Modified from http://smdn.jp/programming/tips/createlnk/
    // Originally from http://www.vbaccelerator.com/home/NET/Code/Libraries/Shell_Projects
    // /Creating_and_Modifying_Shortcuts/article.asp
    // Partly based on Sending toast notifications from desktop apps sample
    public class ShellLink : IDisposable {
        #region Win32 and COM

        // IShellLink Interface
        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("000214F9-0000-0000-C000-000000000046")]
        private interface IShellLinkW {
            uint GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, ref Win32_Find_Dataw pfd, uint fFlags);
            uint GetIDList(out IntPtr ppidl);
            uint SetIDList(IntPtr pidl);
            uint GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
            uint SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            uint GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
            uint SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
            uint GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
            uint SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
            uint GetHotKey(out ushort pwHotkey);
            uint SetHotKey(ushort wHotKey);
            uint GetShowCmd(out int piShowCmd);
            uint SetShowCmd(int iShowCmd);
            uint GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
            uint SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
            uint SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
            uint Resolve(IntPtr hwnd, uint fFlags);
            uint SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
        }

        // ShellLink CoClass (ShellLink object)
        [ComImport, ClassInterface(ClassInterfaceType.None), Guid("00021401-0000-0000-C000-000000000046")]
        private class CShellLink { }

        // WIN32_FIND_DATAW Structure
        [StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Unicode)]
        private struct Win32_Find_Dataw {
            public uint dwFileAttributes;
            public ComTypes.FILETIME ftCreationTime;
            public ComTypes.FILETIME ftLastAccessTime;
            public ComTypes.FILETIME ftLastWriteTime;
            public uint nFileSizeHigh;
            public uint nFileSizeLow;
            public uint dwReserved0;
            public uint dwReserved1;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MaxPath)]
            public string cFileName;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
            public string cAlternateFileName;
        }

        // IPropertyStore Interface
        [ComImport,
         InterfaceType(ComInterfaceType.InterfaceIsIUnknown),
         Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
        private interface IPropertyStore {
            uint GetCount([Out] out uint cProps);
            uint GetAt([In] uint iProp, out PropertyKey pkey);
            uint GetValue([In] ref PropertyKey key, [Out] PropVariant pv);
            uint SetValue([In] ref PropertyKey key, [In] PropVariant pv);
            uint Commit();
        }

        // PropertyKey Structure
        // Narrowed down from PropertyKey.cs of Windows API Code Pack 1.1
        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct PropertyKey {
            public Guid FormatId { get; }

            public Int32 PropertyId { get; }

            public PropertyKey(Guid formatId, Int32 propertyId) {
                FormatId = formatId;
                PropertyId = propertyId;
            }

            public PropertyKey(string formatId, Int32 propertyId) {
                FormatId = new Guid(formatId);
                PropertyId = propertyId;
            }
        }

        // PropVariant Class (only for string value)
        // Narrowed down from PropVariant.cs of Windows API Code Pack 1.1
        // Originally from http://blogs.msdn.com/b/adamroot/archive/2008/04/11
        // /interop-with-propvariants-in-net.aspx
        [StructLayout(LayoutKind.Explicit)]
        private sealed class PropVariant : IDisposable {
            [FieldOffset(0)]
            private ushort valueType; // Value type

            // [FieldOffset(2)]
            // ushort wReserved1; // Reserved field
            // [FieldOffset(4)]
            // ushort wReserved2; // Reserved field
            // [FieldOffset(6)]
            // ushort wReserved3; // Reserved field

            [FieldOffset(8)]
            private readonly IntPtr ptr; // Value

            // Value type (System.Runtime.InteropServices.VarEnum)
            public VarEnum VarType {
                get => (VarEnum)valueType;
                set => valueType = (ushort)value;
            }

            // Whether value is empty or null
            public bool IsNullOrEmpty => (valueType == (ushort)VarEnum.VT_EMPTY ||
                    valueType == (ushort)VarEnum.VT_NULL);

            // Value (only for string value)
            public string Value => Marshal.PtrToStringUni(ptr);
            #endregion

            public PropVariant() { }

            // Construct with string value
            public PropVariant(string value) {
                if (value == null)
                    throw new ArgumentException("Failed to set value.");

                valueType = (ushort)VarEnum.VT_LPWSTR;
                ptr = Marshal.StringToCoTaskMemUni(value);
            }

            ~PropVariant() {
                Dispose();
            }

            public void Dispose() {
                PropVariantClear(this);
                GC.SuppressFinalize(this);
            }
        }

        [DllImport("Ole32.dll", PreserveSig = false)]
        private static extern void PropVariantClear([In, Out] PropVariant pvar);

        private IShellLinkW _shellLinkW = null;

        // Name = System.AppUserModel.ID
        // ShellPKey = PKEY_AppUserModel_ID
        // FormatID = 9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3
        // PropID = 5
        // Type = String (VT_LPWSTR)
        private readonly PropertyKey _appUserModelIdKey =
                new PropertyKey("{9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3}", 5);

        private const int MaxPath = 260;
        private const int InfoTipSize = 1024;

        private const int StgmRead = 0x00000000; // STGM constants
        private const uint SlgpUncPriority = 0x0002; // SLGP flags

        private IPersistFile PersistFile {
            get {
                if (!(_shellLinkW is IPersistFile persistFile)) throw new COMException("Failed to create IPersistFile.");
                return persistFile;
            }
        }

        private IPropertyStore PropertyStore {
            get {
                if (!(_shellLinkW is IPropertyStore propertyStore)) throw new COMException("Failed to create IPropertyStore.");
                return propertyStore;
            }
        }

        #region Public Properties (Minimal)

        // Path of loaded shortcut file
        public string ShortcutFile {
            get {
                PersistFile.GetCurFile(out var shortcutFile);
                return shortcutFile;
            }
        }

        // Path of target file
        public string TargetPath {
            get {
                // No limitation to length of buffer string in the case of Unicode though.
                var targetPath = new StringBuilder(MaxPath);
                var data = new Win32_Find_Dataw();
                VerifySucceeded(_shellLinkW.GetPath(targetPath, targetPath.Capacity, ref data, SlgpUncPriority));
                return targetPath.ToString();
            }
            set => VerifySucceeded(_shellLinkW.SetPath(value));
        }

        public string Arguments {
            get {
                // No limitation to length of buffer string in the case of Unicode though.
                var arguments = new StringBuilder(InfoTipSize);
                VerifySucceeded(_shellLinkW.GetArguments(arguments, arguments.Capacity));
                return arguments.ToString();
            }
            set => VerifySucceeded(_shellLinkW.SetArguments(value));
        }

        // AppUserModelID to be used for Windows 7 or later.
        public string AppUserModelId {
            get {
                using (var pv = new PropVariant()) {
                    VerifySucceeded(PropertyStore.GetValue(_appUserModelIdKey, pv));
                    return pv.Value ?? "Null";
                }
            }
            set {
                using (var pv = new PropVariant(value)) {
                    VerifySucceeded(PropertyStore.SetValue(_appUserModelIdKey, pv));
                    VerifySucceeded(PropertyStore.Commit());
                }
            }
        }
        #endregion

        public ShellLink() : this(null) { }

        // Construct with loading shortcut file.
        public ShellLink(string file) {
            try {
                _shellLinkW = (IShellLinkW)new CShellLink();
            } catch {
                throw new COMException("Failed to create ShellLink object.");
            }

            if (file != null)
                Load(file);
        }

        ~ShellLink() {
            Dispose(false);
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing) {
            if (_shellLinkW != null) {
                // Release all references.
                Marshal.FinalReleaseComObject(_shellLinkW);
                _shellLinkW = null;
            }
        }

        // Save shortcut file.
        public void Save() {
            var file = ShortcutFile;

            if (file == null)
                throw new InvalidOperationException("File name is not given.");
            else
                Save(file);
        }

        public void Save([NotNull] string file) {
            PersistFile.Save(file, true);
        }

        // Load shortcut file.
        public void Load([NotNull] string file) {
            if (!File.Exists(file)) throw new FileNotFoundException("File is not found.", file);
            PersistFile.Load(file, StgmRead);
        }

        // Verify if operation succeeded.
        private static void VerifySucceeded(uint hresult) {
            if (hresult > 1) {
                throw new InvalidOperationException("Failed with HRESULT: " +
                        hresult.ToString("X"));
            }
        }
    }
}