using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;

namespace AcTools.Utils {
    public static class AesUtils {
        private class PieceInfo : IWithId<uint> {
            public uint Id { get; }

            [NotNull]
            public string Context { get; }

            [CanBeNull]
            private readonly SecureString _key64;

            public PieceInfo(uint id, string context, [CanBeNull] SecureString key64) {
                Id = id;
                Context = context;
                _key64 = key64;
            }

            private Regex GetContextRegex() {
                var escaped = Regex.Replace(Regex.Replace(Context,
                        @"[-\/\\^$*+.()|[\]{}]", @"\$&"), @"\?", ".*");
                return new Regex($"^{escaped}$");
            }

            public void AssertContext([CanBeNull] string contextId) {
                if (contextId != null && !GetContextRegex().IsMatch(contextId)) {
                    throw new Exception("Not allowed for given context");
                }
            }

            public byte[] GetKey() {
                var unmanagedString = IntPtr.Zero;
                try {
                    unmanagedString = Marshal.SecureStringToGlobalAllocUnicode(_key64 ?? throw new InvalidDataException());
                    return Marshal.PtrToStringUni(unmanagedString).FromCutBase64();
                } finally {
                    if (unmanagedString != IntPtr.Zero) {
                        Marshal.ZeroFreeBSTR(unmanagedString);
                    }
                }
            }
        }

        private static readonly List<PieceInfo> Pieces = new List<PieceInfo>();

        private static byte[] RandomBytes(int length) {
            var result = new byte[length];
            using (var gen = RandomNumberGenerator.Create()) {
                gen.GetBytes(result);
            }
            return result;
        }

        [NotNull]
        private static PieceInfo GetKnownKey(uint keyId, [CanBeNull] string contextId) {
            var keyReg = Pieces.GetById(keyId);
            keyReg.AssertContext(contextId);
            return keyReg;
        }

        private static PieceInfo ParseAndRegisterPiece([NotNull] string pieceData) {
            var bytes = pieceData.Trim().FromCutBase64() ?? throw new InvalidDataException();
            var separator1 = Array.IndexOf(bytes, (byte)';');
            var separator2 = Array.IndexOf(bytes, (byte)';', separator1 + 1);
            if (separator1 == -1 || separator2 == -1) throw new InvalidDataException();
            var id = BitConverter.ToUInt32(bytes, separator2);
            var existing = Pieces.GetByIdOrDefault(id);
            if (existing != null) return existing;
            var piece64 = new SecureString();
            for (var i = separator2 + 1; i < bytes.Length; i++) {
                piece64.AppendChar((char)bytes[i]);
            }
            piece64.MakeReadOnly();
            // Console.WriteLine($"{separator1}, {separator2}");
            var piece = new PieceInfo(id, Encoding.UTF8.GetString(bytes, separator1 + 1, separator2 - separator1 - 1), piece64);
            Pieces.Add(piece);
            return piece;
        }

        public static void Register([NotNull] string pieceData) {
            ParseAndRegisterPiece(pieceData);
        }

        public static string EncryptData(string data, string keyData, string contextId) {
            var piece = ParseAndRegisterPiece(keyData);
            piece.AssertContext(contextId);
            var key = piece.GetKey();
            for (var i = 0; i < contextId.Length; i++) key[4 + i % (key.Length - 4)] ^= (byte)contextId[i];
            var iv = RandomBytes(16);
            Console.WriteLine(piece.Context);
            data = "length:" + data.Length + ";data:" + data;
            var keyIdEnc = BitConverter.ToUInt32(key, 0) ^ BitConverter.ToUInt32(iv, 0);
            var textBytes = Encoding.UTF8.GetBytes(data);
            using (var aes = new AesManaged()) {
                var enc = aes.CreateEncryptor(key.Skip(4).ToArray(), iv);
                using (var ms = new MemoryStream()) {
                    using (var cs = new CryptoStream(ms, enc, CryptoStreamMode.Write)) {
                        cs.Write(textBytes, 0, textBytes.Length);
                    }
                    return Convert.ToBase64String(BitConverter.GetBytes(keyIdEnc).Concat(iv).Concat(ms.ToArray()).ToArray());
                }
            }
        }

        public static string DecryptData(string data64, string contextId) {
            var data = data64.FromCutBase64() ?? throw new InvalidDataException();
            var iv = data.Skip(4).Take(16).ToArray();
            var keyId = BitConverter.ToUInt32(data, 0) ^ BitConverter.ToUInt32(iv, 0);
            var key = GetKnownKey(keyId, contextId).GetKey();
            for (var i = 0; i < contextId.Length; i++) key[4 + i % (key.Length - 4)] ^= (byte)contextId[i];
            using (var aes = new AesManaged()) {
                var enc = aes.CreateDecryptor(key.Skip(4).ToArray(), iv);
                using (var ms = new MemoryStream()) {
                    using (var cs = new CryptoStream(ms, enc, CryptoStreamMode.Write)) {
                        cs.Write(data.Skip(20).ToArray(), 0, data.Length - 20);
                    }
                    var decrypted = Encoding.UTF8.GetString(ms.ToArray());
                    var delimiter = decrypted.IndexOf(';');
                    if (delimiter < 8) return null;
                    var delimiter1 = decrypted.IndexOf(':');
                    var delimiter2 = decrypted.IndexOf(':', delimiter);
                    if (delimiter1 != 6 || delimiter2 != delimiter + 5) return null;
                    return decrypted.Substring(delimiter2 + 1, Int32.Parse(decrypted.Substring(delimiter1 + 1, delimiter - delimiter1 - 1)));
                }
            }
        }
    }
}