using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace FirstFloor.ModernUI.Helpers {
    public static class StringCipher {
        // This constant is used to determine the keysize of the encryption algorithm in bits.
        // We divide this by 8 within the code below to get the equivalent number of bytes.
        private const int Keysize = 256;

        // This constant determines the number of iterations for the password bytes generation function.
        private const int DerivationIterations = 1000;

        public static byte[] Encrypt(byte[] decryptesBytes, string passPhrase) {
            if (decryptesBytes == null) return null;

            try {
                var salt = Generate256BitsOfRandomEntropy();
                var iv = Generate256BitsOfRandomEntropy();

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
                Logging.Warning("[StringCipher] Decrypt exception: " + e);
                return null;
            }
        }

        public static byte[] Decrypt(byte[] input, string passPhrase) {
            if (input == null) return null;

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
                Logging.Warning("[StringCipher] Decrypt exception: " + e);
                return null;
            }
        }

        public static string Encrypt(string input, string pass) {
            try {
                if (input == null) return null;

                var encrypted = Encrypt(Encoding.UTF8.GetBytes(input), pass);
                return encrypted == null ? null : Convert.ToBase64String(encrypted);
            } catch (Exception e) {
                Logging.Warning("[StringCipher] Decrypt exception: " + e);
                return null;
            }
        }

        public static string Decrypt(string input, string pass) {
            try {
                if (input == null) return null;

                var decrypted = Decrypt(Convert.FromBase64String(input), pass);
                return decrypted == null ? null : Encoding.UTF8.GetString(decrypted);
            } catch (Exception e) {
                Logging.Warning("[StringCipher] Decrypt exception: " + e);
                return null;
            }
        }

        private static byte[] Generate256BitsOfRandomEntropy() {
            var result = new byte[32];
            using (var provider = new RNGCryptoServiceProvider()) {
                provider.GetBytes(result);
            }
            return result;
        }
    }
}
