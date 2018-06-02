using System;
using System.Collections.Generic;
using System.Linq;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.DiscordRpc {
    public struct DiscordJoinRequest {
        [NotNull]
        public string UserId { get; set; }

        [NotNull]
        public string UserName { get; set; }

        [NotNull]
        public string Discriminator { get; set; }

        [CanBeNull]
        public string AvatarUrl { get; set; }
    }

    public enum DiscordJoinRequestReply {
        No = 0,
        Yes = 1,
        Ignore = 2
    }

    public class DiscordImage {
        [NotNull]
        public static string OptionDefaultImage = "";

        [NotNull]
        public string Key, Text;

        public DiscordImage([NotNull] string key, [NotNull] string text) {
            Key = key == "" ? OptionDefaultImage : key;
            Text = text;
        }
    }

    public class DiscordParty {
        [NotNull]
        public readonly string Id;
        public string MatchSecret, JoinSecret, SpectateSecret;
        public int Size, Capacity;

        public DiscordParty([NotNull] string id) {
            Id = id;
        }
    }

    public class DiscordRichPresence : NotifyPropertyChanged, IDisposable, IComparer<DiscordRichPresence> {
        private readonly int _priority;

        private string _state;

        [NotNull]
        public string State {
            get => _state;
            set {
                if (Equals(value, _state)) return;
                _state = value;
                OnPropertyChanged();
                Update();
            }
        }

        private bool _instance;

        public bool Instance {
            get => _instance;
            set {
                if (Equals(value, _instance)) return;
                _instance = value;
                OnPropertyChanged();
                Update();
            }
        }

        private string _details;

        [NotNull]
        public string Details {
            get => _details;
            set {
                if (Equals(value, _details)) return;
                _details = value;
                OnPropertyChanged();
                Update();
            }
        }

        private DateTime? _start;

        public DateTime? Start {
            get => _start;
            set {
                if (Equals(value, _start)) return;
                _start = value;
                OnPropertyChanged();
                Update();
            }
        }

        private DateTime? _end;

        public DateTime? End {
            get => _end;
            set {
                if (Equals(value, _end)) return;
                _end = value;
                OnPropertyChanged();
                Update();
            }
        }

        private DiscordImage _largeImage;

        [CanBeNull]
        public DiscordImage LargeImage {
            get => _largeImage;
            set {
                if (Equals(value, _largeImage)) return;
                _largeImage = value;
                OnPropertyChanged();
                Update();
            }
        }

        private DiscordImage _smallImage;

        [CanBeNull]
        public DiscordImage SmallImage {
            get => _smallImage;
            set {
                if (Equals(value, _smallImage)) return;
                _smallImage = value;
                OnPropertyChanged();
                Update();
            }
        }

        private DiscordParty _party;

        [CanBeNull]
        public DiscordParty Party {
            get => _party;
            set {
                if (Equals(value, _party)) return;
                _party = value;
                OnPropertyChanged();
                Update();
            }
        }

        public DiscordRichPresence(int priority, [NotNull] string state, [NotNull] string details) {
            _priority = priority;
            State = state.ToTitle();
            Details = details;

            Instances.AddSorted(this, this);
            Update();
        }

        private bool _isDisposed;

        public bool IsDisposed {
            get => _isDisposed;
            set => Apply(value, ref _isDisposed);
        }

        public void Dispose() {
            IsDisposed = true;
            Instances.Remove(this);
            Update(true);
        }

        private static List<DiscordRichPresence> Instances = new List<DiscordRichPresence>();
        private static Busy _busy = new Busy();

        private void Update(bool force = false) {
            if (IsDisposed && !force) return;
            _busy.DoDelay(() => {
                if (!force && Instances.FirstOrDefault() != this) return;
                DiscordConnector.Instance?.Update(Instances.FirstOrDefault());
            }, 300);
        }

        public void ForceUpdate() {
            if (IsDisposed) return;
            DiscordConnector.Instance?.Update(Instances.FirstOrDefault());
        }

        int IComparer<DiscordRichPresence>.Compare(DiscordRichPresence x, DiscordRichPresence y) {
            return (y?._priority ?? 0) - (x?._priority ?? 0);
        }
    }
}