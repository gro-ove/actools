using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers.AcSettingsControls {
    public class CustomButtonEntryCombined : NotifyPropertyChanged {
        [CanBeNull]
        public CustomButtonEntry Button { get; }

        [CanBeNull]
        public CustomButtonEntry ButtonModifier { get; }

        [NotNull]
        public WheelButtonEntry WheelButton { get; }

        public string ToolTip { get; }

        public CustomButtonEntryCombined([LocalizationRequired(false)] string id, string displayName,
                string toolTip = null, Keys? defaultKey = null, Keys? defaultModifierKey = null) {
            WheelButton = new WheelButtonEntry(id, displayName, true);
            Button = new CustomButtonEntry(id, displayName, defaultKey, false);
            ButtonModifier = new CustomButtonEntry(id, displayName, defaultModifierKey, true);
            Button.ModifierReference = ButtonModifier;
            ToolTip = toolTip;
        }
    }

    public class SystemButtonEntryCombined : NotifyPropertyChanged {
        [CanBeNull]
        private readonly SystemButtonEntry _systemButtonReference;

        [CanBeNull]
        private readonly Func<Keys?, IEnumerable<Keys>> _fixedValueCallback;

        public bool ShiftToInvert { get; }
        public bool CustomCommand { get; }
        public bool Delayed { get; }
        public string DisplayModifiers { get; }
        public string ToolTip { get; }

        public SystemButtonEntryCombined([LocalizationRequired(false)] string id, string displayName,
                bool shiftToInvert = false, bool customCommand = false, Func<Keys?, IEnumerable<Keys>> fixedValueCallback = null,
                SystemButtonEntry buttonReference = null, bool delayed = false, string toolTip = null, string displayModifiers = "Ctrl+",
                Keys? defaultKey = null) {
            _fixedValueCallback = fixedValueCallback;
            WheelButton = new WheelButtonEntry(id, displayName, true);
            SystemButton = fixedValueCallback == null ? new SystemButtonEntry(id, displayName, defaultKey) : null;
            ShiftToInvert = shiftToInvert;
            CustomCommand = customCommand;
            Delayed = delayed;
            ToolTip = toolTip;
            DisplayModifiers = displayModifiers;

            _systemButtonReference = buttonReference ?? SystemButton;
            if (_systemButtonReference != null) {
                _systemButtonReference.PropertyChanged += OnSystemButtonPropertyChanged;
            }

            UpdateDisplayFixedValue();
        }

        private void OnSystemButtonPropertyChanged(object sender, PropertyChangedEventArgs args) {
            if (args.PropertyName == nameof(SystemButtonEntry.Input)) {
                UpdateDisplayFixedValue();
                OnPropertyChanged(nameof(IsWheelButtonAllowed));
            }
        }

        private void UpdateDisplayFixedValue() {
            DisplayFixedValue = _fixedValueCallback?.Invoke(_systemButtonReference?.Input?.Key)?
                                                    .Select(x => x.ToReadableKey()).JoinToString('+');
        }

        private string _displayFixedValue;

        [CanBeNull]
        public string DisplayFixedValue {
            get => _displayFixedValue;
            private set => Apply(value, ref _displayFixedValue);
        }

        [NotNull]
        public WheelButtonEntry WheelButton { get; }

        [CanBeNull]
        public SystemButtonEntry SystemButton { get; }

        public bool IsWheelButtonAllowed => CustomCommand || DisplayModifiers == null
                || _systemButtonReference == null || _systemButtonReference.Input != null;
    }
}