using System.Collections.Generic;
using AcManager.Tools.Objects;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;

namespace AcManager.Tools.ContentInstallation.Entries {
    public sealed class TrackContentLayoutEntry : NotifyPropertyChanged, IWithId {
        /// <summary>
        /// KN5-files referenced in assigned models.ini if exists.
        /// </summary>
        [CanBeNull]
        public readonly List<string> Kn5Files;

        public string DisplayKn5Files => Kn5Files?.JoinToReadableString();

        // Similar to Kn5Files, but here is a list of files required, but not provided in the source.
        [CanBeNull]
        public readonly List<string> RequiredKn5Files;

        private string[] _missingKn5Files = new string[0];

        public string[] MissingKn5Files {
            get => _missingKn5Files;
            set {
                value = value ?? new string[0];
                if (Equals(value, _missingKn5Files)) return;
                _missingKn5Files = value;
                DisplayMissingKn5Files = value.JoinToReadableString();
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayMissingKn5Files));
            }
        }

        public string DisplayMissingKn5Files { get; private set; }

        /// <summary>
        /// If it’s not an actual layout, but instead just a base-track in a multi-layout situation, Id is empty!
        /// </summary>
        [NotNull]
        public string Id { get; }

        private bool _active = true;

        public bool Active {
            get => _active;
            set => Apply(value, ref _active);
        }

        [CanBeNull]
        public string Name { get; }

        [CanBeNull]
        public string Version { get; }

        [CanBeNull]
        public byte[] IconData { get; }

        public TrackContentLayoutEntry([NotNull] string id, [CanBeNull] List<string> kn5Files, [CanBeNull] List<string> requiredKn5Files,
                string name = null, string version = null, byte[] iconData = null) {
            Kn5Files = kn5Files;
            RequiredKn5Files = requiredKn5Files;
            Id = id;
            Name = name;
            Version = version;
            IconData = iconData;
        }

        public string DisplayId => string.IsNullOrEmpty(Id) ? "N/A" : Id;

        public string DisplayName => ExistingLayout == null ? $"{Name} (new layout)" :
                Name == ExistingLayout.LayoutName ? $"{Name} (update for layout)" : $"{Name} (update for {ExistingLayout.LayoutName})";

        private TrackObjectBase _existingLayout;

        public TrackObjectBase ExistingLayout {
            get => _existingLayout;
            set => Apply(value, ref _existingLayout);
        }

        private BetterImage.Image? _icon;
        public BetterImage.Image? Icon => IconData == null ? null :
                _icon ?? (_icon = BetterImage.LoadBitmapSourceFromBytes(IconData, 32));
    }
}