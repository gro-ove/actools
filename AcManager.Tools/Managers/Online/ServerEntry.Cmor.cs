using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using FirstFloor.ModernUI.Windows.Controls;

namespace AcManager.Tools.Managers.Online {
    public partial class ServerEntry {
        public static class ServerIdEncoder {
            public static ulong Encode(string host, int port) {
                if (port == 0) port = 1;
                return IPAddress.TryParse(host, out var addr) && addr.AddressFamily != AddressFamily.InterNetworkV6
                        ? ((ulong)(uint)addr.GetHashCode() << 16) | (ushort)port
                        : 0x8000_0000_0000_0000UL | Fnv1a64(Encoding.UTF8.GetBytes(host), port);
            }

            private static ulong Fnv1a64(byte[] data, int port) {
                #if DEBUG
                ModernDialog.ShowMessage("hostname route?");
                #endif
                const ulong offset = 14695981039346656037UL;
                const ulong prime = 1099511628211UL;
                var hash = offset;
                for (var i = 0; i < data.Length; i++) {
                    hash ^= data[i];
                    hash *= prime;
                }
                hash &= ~0xFFFFUL;
                hash |= (ushort)port;
                return hash;
            }
        }

        private ulong _id64;

        public ulong Id64 => _id64 != 0 ? _id64 : _id64 = ServerIdEncoder.Encode(Ip, PortHttp);
        
        private int _votesUp = -2;
        
        public int VotesUp {
            get => _votesUp < 0 ? 0 : _votesUp;
            set => Apply(value, ref _votesUp, () => {
                OnPropertyChanged(nameof(VotesTotal));
                OnPropertyChanged(nameof(DisplayVotes));
                OnPropertyChanged(nameof(VotesRating));
            });
        }

        private int _votesDown;

        public int VotesDown {
            get => _votesDown;
            set => Apply(value, ref _votesDown, () => {
                OnPropertyChanged(nameof(VotesTotal));
                OnPropertyChanged(nameof(DisplayVotes));
                OnPropertyChanged(nameof(VotesRating));
            });
        }

        public void SyncVotes(int down, int up) {
            var chDown = _votesDown != down;
            var chUp = Math.Max(_votesUp, 0) != up;
            if (!chDown && up == _votesUp /* for -2 thing to work */) return;
            _votesDown = down;
            _votesUp = up;
            if (chDown || chUp) {
                if (chDown) OnPropertyChanged(nameof(VotesDown));
                if (chUp) OnPropertyChanged(nameof(VotesUp));
                OnPropertyChanged(nameof(VotesTotal));
                OnPropertyChanged(nameof(DisplayVotes));
                OnPropertyChanged(nameof(VotesRating));
            }
        }

        public int VotesTotal => _votesUp + _votesDown;
        private int _ownVote;

        public int OwnVote {
            get => _ownVote;
            set => Apply(value, ref _ownVote);
        }

        public double VotesRating {
            get {
                if (_votesUp == -2) {
                    _votesUp = -1;
                    CmorProvider.EnsureInitialized(this);
                }
                if (_votesUp == -1) return 0.5;
                var up = _votesUp;
                var down = _votesDown;
                var mixed = Math.Max(4 - (up + down), 0);
                return (up + mixed * 0.5) / (up + down + mixed);
            }
        }
        
        public string DisplayVotes {
            get {
                if (_votesUp == -2) {
                    _votesUp = -1;
                    CmorProvider.EnsureInitialized(this);
                }
                if (_votesUp == -1) return "N/A";
                if (_votesUp == 0 && _votesDown == 0) return "no votes yet";
                return $"{100d * VotesRating:F0}% ({VotesTotal})";
            }
        }
    }
}