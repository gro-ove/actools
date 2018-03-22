using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Helpers {
    public static class StringCipher {
        // This constant is used to determine the keysize of the encryption algorithm in bits.
        // We divide this by 8 within the code below to get the equivalent number of bytes.
        private const int Keysize = 256;

        // This constant determines the number of iterations for the password bytes generation function.
        private const int DerivationIterations = 1000;

        [UsedImplicitly]
        public static byte[] Encrypt([NotNull] byte[] decryptesBytes, [NotNull] string passPhrase) {
            try {
                byte[] salt, iv;

                using (var sha1 = SHA256.Create()) {
                    // Sorry, security, but encrypted strings should be the same.
                    salt = sha1.ComputeHash(decryptesBytes);
                    iv = sha1.ComputeHash(salt);
                }

                using (var password = new Rfc2898DeriveBytes(passPhrase, salt, DerivationIterations)) {
                    var keyBytes = password.GetBytes(Keysize / 8);
                    using (var symmetricKey = new RijndaelManaged {
                        BlockSize = 256,
                        Mode = CipherMode.CBC,
                        Padding = PaddingMode.PKCS7
                    })
                    using (var encryptor = symmetricKey.CreateEncryptor(keyBytes, iv))
                    using (var memoryStream = new MemoryStream())
                    using (var cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write)) {
                        cryptoStream.Write(decryptesBytes, 0, decryptesBytes.Length);
                        cryptoStream.FlushFinalBlock();
                        return salt.Concat(iv).Concat(memoryStream.ToArray()).ToArray();
                    }
                }
            } catch (Exception e) {
                Logging.Warning(e.Message);
                return null;
            }
        }

        [UsedImplicitly]
        public static byte[] Decrypt([NotNull] byte[] input, [NotNull] string passPhrase) {
            try {
                var salt = input.Take(Keysize / 8).ToArray();
                var iv = input.Skip(Keysize / 8).Take(Keysize / 8).ToArray();
                var actual = input.Skip(Keysize / 8 * 2).Take(input.Length - Keysize / 8 * 2).ToArray();
                using (var password = new Rfc2898DeriveBytes(passPhrase, salt, DerivationIterations)) {
                    var keyBytes = password.GetBytes(Keysize / 8);
                    using (var symmetricKey = new RijndaelManaged {
                        BlockSize = 256,
                        Mode = CipherMode.CBC,
                        Padding = PaddingMode.PKCS7
                    })
                    using (var decryptor = symmetricKey.CreateDecryptor(keyBytes, iv))
                    using (var memoryStream = new MemoryStream(actual))
                    using (var cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read)) {
                        var result = new byte[actual.Length];
                        var count = cryptoStream.Read(result, 0, result.Length);
                        return count != result.Length ? result.Take(count).ToArray() : result;
                    }
                }
            } catch (Exception e) {
                Logging.Warning(e.Message);
                return null;
            }
        }

        [CanBeNull]
        public static string Encrypt([CanBeNull] string input, [NotNull] string pass) {
            try {
                if (input == null) return null;
                var encrypted = Encrypt(Encoding.UTF8.GetBytes(input), pass);
                return encrypted?.ToCutBase64();
            } catch (Exception e) {
                Logging.Warning(e.Message);
                return null;
            }
        }

        [CanBeNull]
        public static string Decrypt([CanBeNull] string input, [NotNull] string pass) {
            try {
                if (input == null) return null;
                var decrypted = Decrypt(input.FromCutBase64(), pass);
                return decrypted == null ? null : Encoding.UTF8.GetString(decrypted);
            } catch (Exception e) {
                Logging.Warning(e.Message);
                return null;
            }
        }

        [Pure, NotNull]
        private static byte[] FromCutBase64([CanBeNull] this string encoded) {
            if (string.IsNullOrWhiteSpace(encoded)) return new byte[0];

            var padding = 4 - encoded.Length % 4;
            if (padding > 0 && padding < 4) {
                encoded = encoded + @"=".RepeatString(padding);
            }
            return Convert.FromBase64String(encoded);
        }

        [Pure, CanBeNull]
        private static string ToCutBase64([CanBeNull] this byte[] decoded) {
            return decoded != null ? Convert.ToBase64String(decoded).TrimEnd('=') : null;
        }

        [Pure, ContractAnnotation("s:null=>null")]
        private static string RepeatString([CanBeNull] this string s, int number) {
            if (s == null) return null;
            switch (number) {
                case 0:
                    return string.Empty;
                case 1:
                    return s;
                case 2:
                    return s + s;
                default:
                    var b = new StringBuilder();
                    for (var i = 0; i < number; i++) {
                        b.Append(s);
                    }
                    return b.ToString();
            }
        }
    }
}