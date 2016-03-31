using System;
using System.IO;
using System.Linq;

namespace AcTools.AcdFile {
    internal class AcdEncryption {
        public string Id;
        public string Key;

        public AcdEncryption(string id) {
            Id = id;
            Key = CreateKey(Id);
        }

        public void Decrypt(byte[] data) {
            throw new NotImplementedException();
        }

        public byte Decrypt(int data, int position) {
            throw new NotImplementedException();
        }

        public void Encrypt(byte[] data) {
            throw new NotImplementedException();
        }

        public int Encrypt(byte data, int position) {
            throw new NotImplementedException();
        }

        public static string CreateKey(string s) {
            throw new NotImplementedException();
        }

        public static AcdEncryption FromAcdFilename(string acdFilename) {
            return new AcdEncryption(Path.GetFileName(Path.GetDirectoryName(acdFilename)));
        }
    }
}
