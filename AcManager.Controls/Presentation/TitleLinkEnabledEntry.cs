using System.ComponentModel;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Controls.Presentation {
    public sealed class TitleLinkEnabledEntry : Displayable, IWithId {
        public string Id { get; }

        private readonly StoredValue<bool> _isEnabled;

        public bool IsEnabled {
            get => _isEnabled.Value;
            set => Apply(value, _isEnabled);
        }

        private bool _isAvailable = true;

        public bool IsAvailable {
            get => _isAvailable;
            set => Apply(value, ref _isAvailable);
        }

        public TitleLinkEnabledEntry([Localizable(false)] string key, string displayName, bool enabledByDefault = true) {
            Id = key;
            DisplayName = displayName;
            _isEnabled = Stored.Get("AppAppearanceManager.TitleLink:" + key, enabledByDefault);
        }
    }
}