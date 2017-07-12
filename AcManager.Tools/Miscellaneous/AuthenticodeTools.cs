using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace AcManager.Tools.Miscellaneous {
    public static class AuthenticodeTools {
        [DllImport("Wintrust.dll", PreserveSig = true, SetLastError = false)]
        private static extern uint WinVerifyTrust(IntPtr hWnd, IntPtr pgActionId, IntPtr pWinTrustData);

        private static uint WinVerifyTrust(string fileName) {
            var actionGenericVerifyV2 = new Guid("{00AAC56B-CD44-11d0-8CC2-00C04FC295EE}");
            using (var fileInfo = new WintrustFileInfo(fileName, Guid.Empty))
            using (var guidPtr = new UnmanagedPointer(Marshal.AllocHGlobal(Marshal.SizeOf(typeof(Guid))), AllocMethod.HGlobal))
            using (var dataPtr = new UnmanagedPointer(Marshal.AllocHGlobal(Marshal.SizeOf(typeof(WintrustData))), AllocMethod.HGlobal)) {
                var data = new WintrustData(fileInfo);
                IntPtr pGuid = guidPtr, pData = dataPtr;
                Marshal.StructureToPtr(actionGenericVerifyV2, pGuid, true);
                Marshal.StructureToPtr(data, pData, true);
                return WinVerifyTrust(IntPtr.Zero, pGuid, pData);
            }
        }

        public static WintrustResult IsTrusted(string fileName) {
            return (WintrustResult)WinVerifyTrust(fileName);
        }
    }

    public struct WintrustFileInfo : IDisposable {
        public WintrustFileInfo(string fileName, Guid subject) {
            CbStruct = (uint)Marshal.SizeOf(typeof(WintrustFileInfo));
            FilePath = fileName;

            if (subject != Guid.Empty) {
                KnownSubject = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(Guid)));
                Marshal.StructureToPtr(subject, KnownSubject, true);
            } else {
                KnownSubject = IntPtr.Zero;
            }

            File = IntPtr.Zero;
        }

        public uint CbStruct;

        [MarshalAs(UnmanagedType.LPTStr)]
        public string FilePath;
        public IntPtr File;
        public IntPtr KnownSubject;

        public void Dispose() {
            if (KnownSubject != IntPtr.Zero) {
                Marshal.DestroyStructure(KnownSubject, typeof(Guid));
                Marshal.FreeHGlobal(KnownSubject);
            }
        }
    }

    public enum AllocMethod {
        HGlobal,
        CoTaskMem
    };

    public enum UnionChoice {
        File = 1,
        Catalog,
        Blob,
        Signer,
        Cert
    };

    public enum UiChoice {
        All = 1,
        NoUi,
        NoBad,
        NoGood
    };

    public enum RevocationCheckFlags {
        None = 0,
        WholeChain
    };

    public enum StateAction {
        Ignore = 0,
        Verify,
        Close,
        AutoCache,
        AutoCacheFlush
    };

    public enum WintrustResult : uint {
        [Description("Everything is fine")]
        Success = 0,

        [Description("Trust provider is not recognized on this system")]
        ProviderUnknown = 0x800b0001,

        [Description("Trust provider does not support the specified action")]
        ActionUnknown = 0x800b0002,

        [Description("Trust provider does not support the form specified for the subject")]
        SubjectFormUnknown = 0x800b0003,

        [Description("Subject failed the specified verification action")]
        SubjectNotTrusted = 0x800b0004,

        [Description("File is not signed")]
        FileNotSigned = 0x800B0100,

        [Description("Signer’s certificate is in the Untrusted Publishers store")]
        SubjectExplicitlyDistrusted = 0x800B0111,

        [Description("File is probably corrupt")]
        SignatureOrFileCorrupt = 0x80096010,

        [Description("Signer’s certificate was expired")]
        SubjectCertificateExpired = 0x800B0101,

        [Description("Subject’s certificate was revoked")]
        SubjectCertificateRevoked = 0x800B010C,

        [Description("A certification chain processed correctly but terminated in a root certificate that is not trusted by the trust provider.")]
        UntrustedRoot = 0x800B0109
    }

    public enum UiContext {
        Execute = 0,
        Install
    };

    public enum TrustProviderFlags {
        UseIe4Trust = 1,
        NoIe4Chain = 2,
        NoPolicyUsage = 4,
        RevocationCheckNone = 16,
        RevocationCheckEndCert = 32,
        RevocationCheckChain = 64,
        RecovationCheckChainExcludeRoot = 128,
        Safer = 256,
        HashOnly = 512,
        UseDefaultOsVerCheck = 1024,
        LifetimeSigning = 2048
    };

    [StructLayout(LayoutKind.Sequential)]
    public struct WintrustData : IDisposable {
        public WintrustData(WintrustFileInfo fileInfo) {
            CbStruct = (uint)Marshal.SizeOf(typeof(WintrustData));
            InfoStruct = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(WintrustFileInfo)));
            Marshal.StructureToPtr(fileInfo, InfoStruct, false);
            UnionChoice = UnionChoice.File;
            PolicyCallbackData = IntPtr.Zero;
            SipCallbackData = IntPtr.Zero;
            UiChoice = UiChoice.NoUi;
            RevocationChecks = RevocationCheckFlags.None;
            StateAction = StateAction.Ignore;
            StateData = IntPtr.Zero;
            UrlReference = IntPtr.Zero;
            ProviderFlags = TrustProviderFlags.Safer;
            UiContext = UiContext.Execute;
        }

        public uint CbStruct;
        public IntPtr PolicyCallbackData;
        public IntPtr SipCallbackData;
        public UiChoice UiChoice;
        public RevocationCheckFlags RevocationChecks;
        public UnionChoice UnionChoice;
        public IntPtr InfoStruct;
        public StateAction StateAction;
        public IntPtr StateData;
        public IntPtr UrlReference;
        public TrustProviderFlags ProviderFlags;
        public UiContext UiContext;

        public void Dispose() {
            if (UnionChoice == UnionChoice.File) {
                using (var info = new WintrustFileInfo()) {
                    Marshal.PtrToStructure(InfoStruct, info);
                }

                Marshal.DestroyStructure(InfoStruct, typeof(WintrustFileInfo));
            }

            Marshal.FreeHGlobal(InfoStruct);
        }
    }

    public sealed class UnmanagedPointer : IDisposable {
        private IntPtr _p;
        private readonly AllocMethod _m;
        public UnmanagedPointer(IntPtr ptr, AllocMethod method) {
            _m = method;
            _p = ptr;
        }

        ~UnmanagedPointer() {
            Dispose(false);
        }

        private void Dispose(bool disposing) {
            if (_p != IntPtr.Zero) {
                if (_m == AllocMethod.HGlobal) {
                    Marshal.FreeHGlobal(_p);
                } else if (_m == AllocMethod.CoTaskMem) {
                    Marshal.FreeCoTaskMem(_p);
                }
                _p = IntPtr.Zero;
            }

            if (disposing) {
                GC.SuppressFinalize(this);
            }
        }

        public void Dispose() {
            Dispose(true);
        }

        public static implicit operator IntPtr(UnmanagedPointer ptr) {
            return ptr._p;
        }
    }
}