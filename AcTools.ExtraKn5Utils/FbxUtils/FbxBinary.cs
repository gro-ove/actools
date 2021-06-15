// https://www.nuget.org/packages/UkooLabs.FbxSharpie/
// https://github.com/UkooLabs/FBXSharpie
// License: MIT

using System;
using System.IO;
using System.Linq;
using System.Text;
using AcTools.ExtraKn5Utils.FbxUtils.Extensions;

namespace AcTools.ExtraKn5Utils.FbxUtils {
    /// <summary>
    /// Base class for binary stream wrappers
    /// </summary>
    public abstract class FbxBinary {
        // Header string, found at the top of all compliant files
        private static readonly byte[] headerString
                = Encoding.ASCII.GetBytes("Kaydara FBX Binary  \0\x1a\0");

        // This data was entirely calculated by me, honest. Turns out it works, fancy that!
        private static readonly byte[] sourceId =
        { 0x58, 0xAB, 0xA9, 0xF0, 0x6C, 0xA2, 0xD8, 0x3F, 0x4D, 0x47, 0x49, 0xA3, 0xB4, 0xB2, 0xE7, 0x3D };

        private static readonly byte[] key =
        { 0xE2, 0x4F, 0x7B, 0x5F, 0xCD, 0xE4, 0xC8, 0x6D, 0xDB, 0xD8, 0xFB, 0xD7, 0x40, 0x58, 0xC6, 0x78 };

        // This wasn't - it just appears at the end of every compliant file
        private static readonly byte[] extension =
        { 0xF8, 0x5A, 0x8C, 0x6A, 0xDE, 0xF5, 0xD9, 0x7E, 0xEC, 0xE9, 0x0C, 0xE3, 0x75, 0x8F, 0x29, 0x0B };

        // Number of null bytes between the footer version and extension code
        private const int footerZeroes = 120;

        /// <summary>
        /// The size of the footer code
        /// </summary>
        protected const int footerCodeSize = 16;

        /// <summary>
        /// The namespace separator in the binary format (remember to reverse the identifiers)
        /// </summary>
        protected const string binarySeparator = "\0\x1";

        /// <summary>
        /// The namespace separator in the ASCII format and in object data
        /// </summary>
        protected const string asciiSeparator = "::";

        /// <summary>
        /// Checks if the first part of 'data' matches 'original'
        /// </summary>
        /// <param name="data"></param>
        /// <param name="original"></param>
        /// <returns><c>true</c> if it does, otherwise <c>false</c></returns>
        protected static bool CheckEqual(byte[] data, byte[] original) {
            for (int i = 0; i < original.Length; i++) {
                if (data[i] != original[i]) {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Writes the FBX header string
        /// </summary>
        /// <param name="stream"></param>
        protected static void WriteHeader(Stream stream) {
            stream.Write(headerString, 0, headerString.Length);
        }

        /// <summary>
        /// Reads the FBX header string
        /// </summary>
        /// <param name="stream"></param>
        /// <returns><c>true</c> if it's compliant</returns>
        internal static bool ReadHeader(Stream stream) {
            var buf = new byte[headerString.Length];
            stream.Read(buf, 0, buf.Length);
            return CheckEqual(buf, headerString);
        }

        // Turns out this is the algorithm they use to generate the footer. Who knew!
        static void Encrypt(byte[] a, byte[] b) {
            byte c = 64;
            for (int i = 0; i < footerCodeSize; i++) {
                a[i] = (byte)(a[i] ^ (byte)(c ^ b[i]));
                c = a[i];
            }
        }

        const string timePath1 = "FBXHeaderExtension";
        const string timePath2 = "CreationTimeStamp";

        // Gets a single timestamp component
        static int GetTimestampVar(FbxNode timestamp, string element) {
            var elementNode = timestamp[element].FirstOrDefault();
            if (elementNode != null && elementNode.Properties.Count > 0) {
                var prop = elementNode.Properties[0];
                if (prop.TryGetAsLong(out var longValue)) {
                    return (int)longValue;
                }
            }
            throw new FbxException(-1, "Timestamp has no " + element);
        }

        /// <summary>
        /// Generates the unique footer code based on the document's timestamp
        /// </summary>
        /// <param name="document"></param>
        /// <returns>A 16-byte code</returns>
        protected static byte[] GenerateFooterCode(FbxNodeList document) {
            var timestamp = document.GetRelative($"{timePath1}/{timePath2}");
            if (timestamp == null) {
                throw new FbxException(-1, "No creation timestamp");
            }

            try {
                return GenerateFooterCode(
                        GetTimestampVar(timestamp, "Year"),
                        GetTimestampVar(timestamp, "Month"),
                        GetTimestampVar(timestamp, "Day"),
                        GetTimestampVar(timestamp, "Hour"),
                        GetTimestampVar(timestamp, "Minute"),
                        GetTimestampVar(timestamp, "Second"),
                        GetTimestampVar(timestamp, "Millisecond")
                        );
            } catch (ArgumentOutOfRangeException) {
                throw new FbxException(-1, "Invalid timestamp");
            }
        }

        /// <summary>
        /// Generates a unique footer code based on a timestamp
        /// </summary>
        /// <param name="year"></param>
        /// <param name="month"></param>
        /// <param name="day"></param>
        /// <param name="hour"></param>
        /// <param name="minute"></param>
        /// <param name="second"></param>
        /// <param name="millisecond"></param>
        /// <returns>A 16-byte code</returns>
        protected static byte[] GenerateFooterCode(
                int year, int month, int day,
                int hour, int minute, int second, int millisecond) {
            if (year < 0 || year > 9999) {
                throw new ArgumentOutOfRangeException(nameof(year));
            }

            if (month < 0 || month > 12) {
                throw new ArgumentOutOfRangeException(nameof(month));
            }

            if (day < 0 || day > 31) {
                throw new ArgumentOutOfRangeException(nameof(day));
            }

            if (hour < 0 || hour >= 24) {
                throw new ArgumentOutOfRangeException(nameof(hour));
            }

            if (minute < 0 || minute >= 60) {
                throw new ArgumentOutOfRangeException(nameof(minute));
            }

            if (second < 0 || second >= 60) {
                throw new ArgumentOutOfRangeException(nameof(second));
            }

            if (millisecond < 0 || millisecond >= 1000) {
                throw new ArgumentOutOfRangeException(nameof(millisecond));
            }

            var str = (byte[])sourceId.Clone();
            var mangledTime = $"{second:00}{month:00}{hour:00}{day:00}{(millisecond / 10):00}{year:0000}{minute:00}";
            var mangledBytes = Encoding.ASCII.GetBytes(mangledTime);
            Encrypt(str, mangledBytes);
            Encrypt(str, key);
            Encrypt(str, mangledBytes);
            return str;
        }

        /// <summary>
        /// Writes the FBX footer extension (NB - not the unique footer code)
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="version"></param>
        protected void WriteFooter(BinaryWriter stream, int version) {
            var position = stream.BaseStream.Position;
            var paddingLength = (int)(16 - (position % 16));
            if (paddingLength == 0) {
                paddingLength = 16;
            }
            paddingLength += 4;
            var zeroes = new byte[Math.Max(paddingLength, footerZeroes)];
            stream.Write(zeroes, 0, paddingLength);
            stream.Write(version);
            stream.Write(zeroes, 0, footerZeroes);
            stream.Write(extension, 0, extension.Length);
        }

        static bool AllZero(byte[] array) {
            foreach (var b in array) {
                if (b != 0) {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Reads and checks the FBX footer extension (NB - not the unique footer code)
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="version"></param>
        /// <returns><c>true</c> if it's compliant</returns>
        protected bool CheckFooter(BinaryReader stream, FbxVersion version) {
            var position = stream.BaseStream.Position;
            var paddingLength = (int)(16 - (position % 16));
            if (paddingLength == 0) {
                paddingLength = 16;
            }
            paddingLength += 4;
            var buffer = new byte[Math.Max(paddingLength, footerZeroes)];
            stream.Read(buffer, 0, paddingLength);
            bool correct = AllZero(buffer);
            var readVersion = stream.ReadInt32();
            correct &= (readVersion == (int)version);
            stream.Read(buffer, 0, footerZeroes);
            correct &= AllZero(buffer);
            stream.Read(buffer, 0, extension.Length);
            correct &= CheckEqual(buffer, extension);
            return correct;
        }
    }
}