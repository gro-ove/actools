using System;
using System.IO;
using System.IO.Compression;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Tools.About {
    public sealed class PieceOfInformation : Displayable, IWithId {
        private readonly string _sid;

        private PieceOfInformation(bool packed, string sid, string id, string displayName, string version, string url, string content, bool limited, bool hidden) {
            _sid = @"PieceOfInformation.IsNotNew_" + sid;
            Id = id;

            DisplayName = displayName;
            Version = version;
            Url = url;
            IsLimited = limited;

            IsNew = !ValuesStorage.Get<bool>(_sid);
            IsHidden = IsNew && hidden;

            if (packed) {
                _packed = content;
            } else {
                _content = content;
            }
        }

        public PieceOfInformation(string sid, string id, string displayName, string version, string url, string content, bool limited, bool hidden)
                : this(true, sid, id, displayName, version, url, content, limited, hidden) {}

        public static PieceOfInformation Create(string sid, string id, string displayName, string version, string url, string content, bool limited, bool hidden) {
            return new PieceOfInformation(false, sid, id, displayName, version, url, content, limited, hidden);
        }

        public static PieceOfInformation Create(string displayName, string content) {
            return new PieceOfInformation(false, "xc" + displayName.GetHashCode(), null, displayName, null, null, content, false, false);
        }

        public string Id { get; }

        public string Version { get; }

        public string Url { get; }

        private readonly string _packed;
        private string _content;

        [CanBeNull]
        public string Content => _content ?? (_packed == null ? null : (_content = Unpack(_packed)));

        private static string Unpack(string packed) {
            var bytes = Convert.FromBase64String(packed);
            using (var inputStream = new MemoryStream(bytes)) {
                return new DeflateStream(inputStream, CompressionMode.Decompress).ReadAsStringAndDispose();
            }
        }

        public bool IsLimited { get; }

        public bool IsHidden { get; }

        private bool _isNew;

        public bool IsNew {
            get { return _isNew; }
            private set => Apply(value, ref _isNew);
        }

        public void MarkAsRead() {
            IsNew = false;
            AboutHelper.Instance.CheckIfThereIsSomethingNew();
            ValuesStorage.Set(_sid, true);
        }
    }
}