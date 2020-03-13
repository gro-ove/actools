using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using AcManager.Tools.Helpers.AcSettings;
using AcManager.Tools.Helpers.DirectInput;
using AcTools.DataFile;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Serialization;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers.AcSettingsControls {
    public class CustomButtonEntry : KeyboardButtonEntry {
        private readonly Keys? _defaultKey;

        [CanBeNull]
        private readonly List<Keys> _defaultModifiers;

        public CustomButtonEntry([LocalizationRequired(false)] string id, string name, Keys? defaultKey, [CanBeNull] List<Keys> defaultModifiers) : base(id, name) {
            _defaultKey = defaultKey;
            _defaultModifiers = defaultModifiers;
        }

        public BetterObservableCollection<Keys> Modifiers { get; } = new BetterObservableCollection<Keys>();

        public override EntryLayer Layer {
            get {
                var ret = EntryLayer.Basic;
                if (Modifiers.Contains(Keys.Control)) ret |= EntryLayer.CtrlShortcut;
                if (Modifiers.Contains(Keys.Alt)) ret |= EntryLayer.AltShortcut;
                if (Modifiers.Contains(Keys.Shift)) ret |= EntryLayer.ShiftShortcut;
                return ret;
            }
        }

        private class ModifiersComparer : IComparer<Keys> {
            public static ModifiersComparer Instance = new ModifiersComparer();

            private static int GetOrderValue(Keys v) {
                switch (v) {
                    case Keys.Control:
                        return 1000002;
                    case Keys.Alt:
                        return 1000001;
                    case Keys.Shift:
                        return 1000000;
                    default:
                        return (int)v;
                }
            }

            public int Compare(Keys x, Keys y) {
                return GetOrderValue(y) - GetOrderValue(x);
            }
        }

        private bool _isEntering;

        public bool IsEntering {
            get => _isEntering;
            set => Apply(value, ref _isEntering, () => OnPropertyChanged(nameof(IsWaitingWithoutPreview)));
        }

        private List<Keys> _previousModifiers;

        public bool IsWaitingWithoutPreview => IsWaiting && !IsEntering;

        protected override void OnIsWaitingChanged() {
            IsEntering = true;
            if (IsWaiting) {
                _previousModifiers = Modifiers.ToList();
                Modifiers.Clear();
            } else if (_previousModifiers != null) {
                Modifiers.ReplaceEverythingBy_Direct(_previousModifiers);
            }
            OnPropertyChanged(nameof(IsWaitingWithoutPreview));
            OnPropertyChanged(nameof(DisplayInput));
        }

        public void AddModifier(Keys modifier) {
            if (Modifiers.Contains(modifier)) return;
            Modifiers.AddSorted(modifier, ModifiersComparer.Instance);
            OnPropertyChanged(nameof(DisplayInput));
        }

        public string DisplayInput => IsWaiting || Input != null
                ? Modifiers.Select(x => x.ToReadableKey()).Append(IsWaiting ? @"…" : Input?.DisplayName ?? @"…").JoinToString(@"+")
                : null;

        public override void OnInputArrived() {
            base.OnInputArrived();
            _previousModifiers = null;
            IsEntering = false;
        }

        protected override void OnInputChanged(KeyboardInputButton oldValue, KeyboardInputButton newValue) {
            base.OnInputChanged(oldValue, newValue);
            OnPropertyChanged(nameof(DisplayInput));
        }

        private DelegateCommand _resetCommand;

        public DelegateCommand ResetCommand => _resetCommand ?? (_resetCommand = new DelegateCommand(() => {
            if (_defaultKey.HasValue) {
                Input = AcSettingsHolder.Controls.GetKeyboardInputButton((int)_defaultKey.Value);
            }
            Modifiers.Clear();
        }, () => _defaultKey.HasValue));

        public override void Load(IniFile ini, IReadOnlyList<IDirectInputDevice> devices) {
            var section = ini[Id];
            Input = AcSettingsHolder.Controls.GetKeyboardInputButton(section.GetInt("KEY",
                    _defaultKey.HasValue ? (int)_defaultKey.Value : -1));
            if (section.ContainsKey("KEY_MODIFICATOR")) {
                Modifiers.ReplaceEverythingBy_Direct(section.GetStrings("KEY_MODIFICATOR")
                        .Select(x => x.As(Keys.None))
                        .Select(x => x.IsInputModifier(out var modifier) ? modifier : Keys.None)
                        .ApartFrom(Keys.None)
                        .Distinct()
                        .OrderBy(x => x, ModifiersComparer.Instance));
            } else if (_defaultModifiers != null) {
                Modifiers.ReplaceEverythingBy_Direct(_defaultModifiers);
            } else {
                Modifiers.Clear();
            }
        }

        private int MapModifierToAc(Keys v) {
            switch (v) {
                case Keys.Control:
                    return 0x11;
                case Keys.Alt:
                    return 0x12;
                case Keys.Shift:
                    return 0x10;
                default:
                    return 0x0;
            }
        }

        public override void Save(IniFile ini) {
            var section = ini[Id];
            var input = Input;
            section.SetCommentary("KEY", input?.DisplayName);
            section.Set("KEY", input == null || !CheckValue(input.Id) ? @"-1" : @"0x" + input.Id.ToString(@"X"));
            section.SetCommentary("KEY_MODIFICATOR", DisplayInput);
            section.Set("KEY_MODIFICATOR", Modifiers.Select(MapModifierToAc).JoinToString(", "));
        }
    }
}