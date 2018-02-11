using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using AcManager.Tools;
using AcManager.Tools.Helpers;
using AcManager.Tools.Objects;
using AcManager.Tools.Tyres;
using AcTools.Utils;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Pages.Dialogs {
    public partial class CarCreateTyresMachineDialog {
        public class KeyComparer : IComparer {
            public static readonly KeyComparer Instance = new KeyComparer();

            public int Compare(object x, object y) {
                if (!(x is SettingEntry sx) || !(y is SettingEntry sy)) return 0;

                if (sx.DisplayName.StartsWith("Δ")) {
                    if (!sy.DisplayName.StartsWith("Δ")) return -1;
                } else if (sy.DisplayName.StartsWith("Δ")) {
                    return 1;
                }

                return string.Compare(sx.DisplayName, sy.DisplayName, StringComparison.CurrentCultureIgnoreCase);
            }
        }

        public class CarTyres : NotifyPropertyChanged {
            public TyresEntry Entry { get; }

            public CarTyres(TyresEntry entry) {
                Entry = entry;
            }

            private bool _isChecked;

            public bool IsChecked {
                get => _isChecked;
                set {
                    if (Equals(value, _isChecked)) return;
                    _isChecked = value;
                    OnPropertyChanged();
                }
            }
        }

        public sealed class CommonTyres : Displayable {
            private readonly Action<string, bool> _changeTyresStateCallback;

            public int Count { get; set; }
            public List<CarObject> Cars { get; } = new List<CarObject>();
            public Lazier<string> DisplaySource { get; }

            private int _includedCount;

            public int IncludedCount {
                get => _includedCount;
                set {
                    if (Equals(value, _includedCount)) return;
                    _includedCount = value;
                    OnPropertyChanged();
                }
            }

            public CommonTyres(string name, Action<string, bool> changeTyresStateCallback) {
                _changeTyresStateCallback = changeTyresStateCallback;
                DisplaySource = Lazier.Create(() => Cars.Select(x => x.Name).JoinToReadableString());
                DisplayName = name;
            }

            private DelegateCommand _includeAllCommand;

            public DelegateCommand IncludeAllCommand
                => _includeAllCommand ?? (_includeAllCommand = new DelegateCommand(() => { _changeTyresStateCallback.Invoke(DisplayName, true); }));

            private DelegateCommand _excludeAllCommand;

            public DelegateCommand ExcludeAllCommand
                => _excludeAllCommand ?? (_excludeAllCommand = new DelegateCommand(() => { _changeTyresStateCallback.Invoke(DisplayName, false); }));
        }

        public class CarWithTyres : NotifyPropertyChanged {
            public CarWithTyres(CarObject car, List<CarTyres> tyres) {
                Car = car;
                Tyres = tyres;
                Tyres.ForEach(x => x.PropertyChanged += OnTyrePropertyChanged);
                UpdateChecked();
            }

            private void OnTyrePropertyChanged(object sender, PropertyChangedEventArgs e) {
                if (e.PropertyName == nameof(CarTyres.IsChecked)) {
                    UpdateChecked();
                }
            }

            public CarObject Car { get; }
            public List<CarTyres> Tyres { get; }

            private bool _isChecked;

            public bool IsChecked {
                get => _isChecked;
                set {
                    if (Equals(value, _isChecked)) return;
                    _isChecked = value;
                    OnPropertyChanged();
                }
            }

            private int _checkedCount;

            public int CheckedCount {
                get => _checkedCount;
                set {
                    if (Equals(value, _checkedCount)) return;
                    _checkedCount = value;
                    OnPropertyChanged();
                }
            }

            private string _displayChecked = ToolsStrings.Common_None;

            public string DisplayChecked {
                get => _displayChecked;
                set {
                    if (string.IsNullOrEmpty(value)) value = ToolsStrings.Common_None;
                    if (Equals(value, _displayChecked)) return;
                    _displayChecked = value;
                    OnPropertyChanged();
                }
            }

            private void UpdateChecked() {
                CheckedCount = Tyres.Count(x => x.IsChecked);
                DisplayChecked = Tyres.Where(x => x.IsChecked).Select(x => x.Entry.DisplayName).JoinToReadableString();
            }
        }
    }
}