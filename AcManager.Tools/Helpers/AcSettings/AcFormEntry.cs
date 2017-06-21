using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers.AcSettings {
    public sealed class AcFormDesktopEntry : NotifyPropertyChanged {
        public int Index { get; }

        public AcFormDesktopEntry(int index) {
            Index = index;
        }

        public void LoadFrom(IniFileSection section) {
            PosX = section.GetInt("POSX", 0);
            PosY = section.GetInt("POSY", 0);
            IsVisible = section.GetBool("VISIBLE", false);
            IsBlocked = section.GetBool("BLOCKED", false);
            Scale = section.GetDouble("SCALE", 1d).ToIntPercentage();
        }

        public void SaveTo(IniFileSection section) {
            section.Set("POSX", PosX);
            section.Set("POSY", PosY);
            section.Set("VISIBLE", IsVisible);
            section.Set("BLOCKED", IsBlocked);
            section.Set("SCALE", Scale.ToDoublePercentage());
        }

        private int _posX;

        public int PosX {
            get { return _posX; }
            set {
                if (Equals(value, _posX)) return;
                _posX = value;
                OnPropertyChanged();
            }
        }

        private int _posY;

        public int PosY {
            get { return _posY; }
            set {
                if (Equals(value, _posY)) return;
                _posY = value;
                OnPropertyChanged();
            }
        }

        private bool _isVisible;

        public bool IsVisible {
            get { return _isVisible; }
            set {
                if (Equals(value, _isVisible)) return;
                _isVisible = value;
                OnPropertyChanged();
            }
        }

        private bool _isBlocked;

        public bool IsBlocked {
            get { return _isBlocked; }
            set {
                if (Equals(value, _isBlocked)) return;
                _isBlocked = value;
                OnPropertyChanged();
            }
        }

        private int _scale = 100;

        public int Scale {
            get { return _scale; }
            set {
                value = value.Clamp(0, 10000);
                if (Equals(value, _scale)) return;
                _scale = value;
                OnPropertyChanged();
            }
        }

        public void CopyFrom(AcFormDesktopEntry form) {
            IsVisible = form.IsVisible;
            IsBlocked = form.IsBlocked;
            Scale = form.Scale;
            PosX = form.PosX;
            PosY = form.PosY;
        }
    }

    public static class AcFormEntryExtension {
        [CanBeNull]
        public static string GetVisibleForms(this IEnumerable<AcFormEntry> forms, int? desktop = null) {
            var result = forms.Where(y => desktop.HasValue ? y.Desktops[desktop.Value].IsVisible : y.Desktops.Any(z => z.IsVisible))
                        .Select(y => y.DisplayName).JoinToReadableString();
            return result.Length == 0 ? null : result;
        }
    }

    public sealed class AcFormEntry : Displayable, IWithId {
        public string Id { get; }

        public AcFormDesktopEntry[] Desktops { get; } = {
            new AcFormDesktopEntry(0),
            new AcFormDesktopEntry(1),
            new AcFormDesktopEntry(2),
            new AcFormDesktopEntry(3)
        };

        // for that table in settings
        public AcFormDesktopEntry First => Desktops[0];
        public AcFormDesktopEntry Second => Desktops[1];
        public AcFormDesktopEntry Third => Desktops[2];
        public AcFormDesktopEntry Forth => Desktops[3];

        private static string FixName(string name) {
            Logging.Debug(name);
            name = Regex.Replace(name, @"Ksmap\b", "KS Map");
            name = Regex.Replace(name, @"Tyre\b(?:(?= Wearing Debug)|(?= Tester))", "Tyres");
            name = Regex.Replace(name, @"Cam\b", "Camera");
            name = Regex.Replace(name, @"Susp?\b", "Suspension");
            name = Regex.Replace(name, @"Perf\b", "Performance");
            name = Regex.Replace(name, @"Rstats\b", "Rendering Stats");
            name = Regex.Replace(name, @"AC(?=[A-Z])", "AC ");
            name = Regex.Replace(name, @"^Form (?=\w)|\bD(?=Car)| Form$", "");
            Logging.Debug(name);
            return name;
        }

        public AcFormEntry(string id) {
            Id = id;
            DisplayName = FixName(AcStringValues.NameFromId(id.All(x => !char.IsLower(x)) ? id.ToLowerInvariant() : id));
            AnyVisible = Desktops.Any(x => x.IsVisible);
            foreach (var desktopEntry in Desktops) {
                desktopEntry.PropertyChanged += OnDesktopEntryPropertyChanged;
            }
        }

        public AcFormEntry(string id, int desktop, [NotNull] IniFileSection section) {
            Id = id;
            DisplayName = FixName(AcStringValues.NameFromId(id.All(x => !char.IsLower(x)) ? id.ToLowerInvariant() : id));
            Desktops.ElementAtOrDefault(desktop)?.LoadFrom(section);
            AnyVisible = Desktops.Any(x => x.IsVisible);
            foreach (var desktopEntry in Desktops) {
                desktopEntry.PropertyChanged += OnDesktopEntryPropertyChanged;
            }
        }

        private void OnDesktopEntryPropertyChanged(object sender, PropertyChangedEventArgs args) {
            if (args.PropertyName == nameof(First.IsVisible)) {
                AnyVisible = Desktops.Any(x => x.IsVisible);
            }
        }

        public void Extend(int desktop, IniFileSection section) {
            Desktops.ElementAtOrDefault(desktop)?.LoadFrom(section);
        }

        private bool _anyVisible;

        public bool AnyVisible {
            get { return _anyVisible; }
            set {
                if (Equals(value, _anyVisible)) return;
                _anyVisible = value;
                OnPropertyChanged();
            }
        }

        public void SetVisibility(bool value) {
            foreach (var desktop in Desktops) {
                desktop.IsVisible = value;
            }
        }

        public void SetScale(int value) {
            foreach (var desktop in Desktops) {
                desktop.Scale = value;
            }
        }

        public void SaveTo(IniFile iniFile) {
            foreach (var desktop in Desktops) {
                desktop.SaveTo(iniFile[$"DESK_{desktop.Index}_FORM_{Id}"]);
            }
        }
    }
}