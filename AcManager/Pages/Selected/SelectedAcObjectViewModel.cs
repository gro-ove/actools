using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using AcManager.Controls;
using JetBrains.Annotations;
using AcManager.Controls.Dialogs;
using AcManager.Pages.Dialogs;
using AcManager.Tools.AcObjectsNew;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;
using StringBasedFilter;
using WaitingDialog = FirstFloor.ModernUI.Dialogs.WaitingDialog;

namespace AcManager.Pages.Selected {
    public abstract class SelectedAcObjectViewModel : NotifyPropertyChanged {
        public static string SpecsFormat(string format, object value) {
            return format.Replace(@"…", value.ToInvariantString());
        }

        private class InnerVersionInfoLabelConverter : IMultiValueConverter {
            public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) {
                var obj = values.FirstOrDefault() as IAcObjectAuthorInformation;
                if (obj == null || obj.Author != null || obj.Url == null && obj.Version == null) {
                    return ControlsStrings.AcObject_AuthorLabel;
                }

                if (obj.Version != null) {
                    return ControlsStrings.AcObject_VersionLabel;
                }

                return ControlsStrings.AcObject_UrlLabel;
            }

            public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) {
                throw new NotSupportedException();
            }
        }

        public static IMultiValueConverter VersionInfoLabelConverter { get; } = new InnerVersionInfoLabelConverter();
    }

    public abstract class SelectedAcObjectViewModel<T> : SelectedAcObjectViewModel, ISelectedAcObjectViewModel where T : AcCommonObject {
        [NotNull]
        public T SelectedObject { get; }

        public AcCommonObject SelectedAcObject => SelectedObject;

        protected SelectedAcObjectViewModel([NotNull] T acObject) {
            SelectedObject = acObject;
        }

        public virtual void Load() {}

        public virtual void Unload() {}

        private ICommand _findInformationCommand;

        public ICommand FindInformationCommand => _findInformationCommand ?? (_findInformationCommand = new DelegateCommand(() => {
            new FindInformationDialog((AcJsonObjectNew)SelectedAcObject).ShowDialog();
        }, () => SelectedAcObject is AcJsonObjectNew));

        private ICommand _changeIdCommand;

        public ICommand ChangeIdCommand => _changeIdCommand ?? (_changeIdCommand = new DelegateCommand(() => {
            var newId = Prompt.Show(AppStrings.AcObject_EnterNewId, AppStrings.Toolbar_ChangeId, SelectedObject.Id, @"?", AppStrings.AcObject_ChangeId_Tooltip);
            if (string.IsNullOrWhiteSpace(newId)) return;
            SelectedObject.ChangeIdCommand.Execute(newId);
        }));

        protected virtual string PrepareIdForInput(string id) {
            if (string.IsNullOrWhiteSpace(id)) return null;
            var singleFile = SelectedAcObject as AcCommonSingleFileObject;
            return singleFile == null ? id : id.ApartFromLast(singleFile.Extension, StringComparison.OrdinalIgnoreCase);
        }

        protected virtual string FixIdFromInput(string id) {
            if (string.IsNullOrWhiteSpace(id)) return null;
            var singleFile = SelectedAcObject as AcCommonSingleFileObject;
            return singleFile == null ? id : id + singleFile.Extension;
        }

        private ICommand _cloneCommand;

        public ICommand CloneCommand => _cloneCommand ?? (_cloneCommand = new AsyncCommand(async () => {
            string defaultId;
            if (SelectedObject.Location.EndsWith(SelectedObject.Id, StringComparison.OrdinalIgnoreCase)) {
                var unique = FileUtils.EnsureUnique(SelectedObject.Location);
                defaultId = unique.Substring(SelectedObject.Location.Length - SelectedObject.Id.Length);
            } else {
                defaultId = SelectedObject.Id + @"-copy";
            }

            var newId = FixIdFromInput(Prompt.Show(AppStrings.AcObject_EnterNewId,
                    AppStrings.Toolbar_Clone, PrepareIdForInput(defaultId), @"?"));
            if (string.IsNullOrWhiteSpace(newId)) return;

            using (var waiting = new WaitingDialog()) {
                waiting.Report(AppStrings.AcObject_Cloning);
                await SelectedObject.CloneAsync(newId);
            }
        }));

        #region Filter Commands
        public void NewFilterTab(string filter) {
            (Application.Current?.Windows.OfType<ModernWindow>().FirstOrDefault(x => x.IsActive)?.CurrentLinkGroup as LinkGroupFilterable)?.AddAndSelect(filter);
        }

        private CommandBase _filterTagCommand;

        public ICommand FilterTagCommand => _filterTagCommand ?? (_filterTagCommand = new DelegateCommand<string>(o => {
            NewFilterTab($@"#{Filter.Encode(o)}");
        }, o => o != null));

        [CanBeNull]
        protected CommandBase InnerFilterCommand;

        public ICommand FilterCommand => InnerFilterCommand ?? (InnerFilterCommand = new DelegateCommand<string>(FilterExec));

        protected void FilterRange([Localizable(false)] string key, double value, double range = 0.05, bool relative = true, double roundTo = 1.0) {
            var delta = (relative ? range * value : range) / 2d;
            NewFilterTab(Equals(roundTo, 1d) && delta.Round(roundTo) < roundTo ?
                    $@"{key}={value.Round(roundTo).ToInvariantString()}" :
                    string.Format(@"{0}>{1} & {0}<{2}", key, (Math.Max(value - delta, 0d).Round(roundTo) - Math.Min(roundTo, 1d)).ToInvariantString(),
                            ((value + delta).Round(roundTo) + Math.Min(roundTo, 1d)).ToInvariantString()));
        }

        protected void FilterDistance([Localizable(false)] string key, double value, double range = 0.05, bool relative = true, double roundTo = 1.0) {
            value /= 1e3;
            var delta = (relative ? range * value : range) / 2d;
            NewFilterTab(Equals(roundTo, 1d) && delta.Round(roundTo) < roundTo ?
                    $@"{key}={value.Round(roundTo).ToInvariantString()}km" :
                    string.Format(@"{0}>{1}km & {0}<{2}km", key, (Math.Max(value - delta, 0d).Round(roundTo) - Math.Min(roundTo, 1d)).ToInvariantString(),
                            ((value + delta).Round(roundTo) + Math.Min(roundTo, 1d)).ToInvariantString()));
        }

        protected void FilterRange([Localizable(false)] string key, string value, double range = 0.05, bool relative = true, double roundTo = 1.0) {
            double actual;
            if (!string.IsNullOrWhiteSpace(value) && FlexibleParser.TryParseDouble(value, out actual)) {
                FilterRange(key, actual, range, relative, roundTo);
            } else {
                NewFilterTab($@"{key}-");
            }
        }

        protected virtual void FilterExec(string type) {
            var jsonObject = SelectedObject as AcJsonObjectNew;
            switch (type) {
                case "author":
                    if (jsonObject == null) return;
                    NewFilterTab(string.IsNullOrWhiteSpace(jsonObject.Author) ? @"author-" : $@"author:{Filter.Encode(jsonObject.Author)}");
                    break;

                case "origin":
                    if (jsonObject == null) return;
                    if (jsonObject.Author != AcCommonObject.AuthorKunos) {
                        NewFilterTab(@"k-");
                    } else if (jsonObject.Dlc != null) {
                        NewFilterTab(@"dlc:" + jsonObject.Dlc.DisplayName);
                    } else {
                        NewFilterTab(@"k+");
                    }
                    break;

                case "age":
                    FilterRange(@"age", SelectedObject.AgeInDays);
                    break;

                case "country":
                    if (jsonObject == null) return;
                    NewFilterTab(string.IsNullOrWhiteSpace(jsonObject.Country) ? @"country-" : $@"country:{Filter.Encode(jsonObject.Country)}");
                    break;

                case "year":
                    NewFilterTab(SelectedObject.Year.HasValue ? $@"year:{SelectedObject.Year}" : @"year-");
                    break;

                case "decade":
                    if (!SelectedObject.Year.HasValue) {
                        NewFilterTab(@"year-");
                    }

                    var start = (int)Math.Floor((SelectedObject.Year ?? 0) / 10d) * 10;
                    NewFilterTab($@"year>{start - 1} & year<{start + 10}");
                    break;
            }
        }
        #endregion

        #region Specs, fix formats
        private readonly List<Tuple<string, string, Func<string>, Action<string>>> _specs =
                new List<Tuple<string, string, Func<string>, Action<string>>>(7);

        protected void RegisterSpec([Localizable(false),NotNull] string key, [NotNull] string format, [NotNull] Func<string> getter, [NotNull] Action<string> setter) {
            _specs.Add(Tuple.Create(key, format, getter, setter));
        }

        protected void RegisterSpec([Localizable(false),NotNull] string key, [NotNull] string format, [NotNull] string propertyName) {
            RegisterSpec(key, format, () => {
                var type = SelectedObject.GetType();
                var property = type.GetProperty(propertyName).GetGetMethod();
                return property.Invoke(SelectedObject, new object[0])?.ToString();
            }, v => {
                var type = SelectedObject.GetType();
                var property = type.GetProperty(propertyName).GetSetMethod();
                property.Invoke(SelectedObject, new object[] { v });
            });
        }

        [NotNull]
        private string GetFormat(string key) {
            return _specs.FirstOrDefault(x => x.Item1 == key)?.Item2 ?? @"…";
        }

        [CanBeNull]
        private string GetSpecsValue(string key) {
            return _specs.FirstOrDefault(x => x.Item1 == key)?.Item3.Invoke() ?? null;
        }

        private void SetSpecsValue(string key, string value) {
            var spec = _specs.FirstOrDefault(x => x.Item1 == key);
            spec?.Item4.Invoke(value);

            if (spec == null) {
                Logging.Warning("Unexpected key: " + key);
            }
        }

        private IEnumerable<string> GetSpecsKeys() {
            return _specs.Select(x => x.Item1);
        }

        private DelegateCommand<string> _fixFormatCommand;

        public DelegateCommand<string> FixFormatCommand => _fixFormatCommand ?? (_fixFormatCommand = new DelegateCommand<string>(key => {
            if (key == null) {
                foreach (var k in GetSpecsKeys()) {
                    FixFormat(k);
                }
            } else {
                FixFormat(key);
            }
        }, key => key == null || !IsFormatCorrect(key)));

        private bool IsFormatCorrect(string key) {
            var format = GetFormat(key);
            var value = GetSpecsValue(key);
            return value == null || Regex.IsMatch(value, @"^" + Regex.Escape(format).Replace(@"…", @"-?\d+(?:\.\d+)?") + @"$");
        }

        private void FixFormat(string key) {
            var format = GetFormat(key);
            var value = GetSpecsValue(key);
            if (value == null) return;

            value = FixFormatCommon(value);

            double actualValue;
            var replacement = FlexibleParser.TryParseDouble(value, out actualValue) ? actualValue.Round(0.01).ToInvariantString() : @"--";
            value = SpecsFormat(format, replacement);
            SetSpecsValue(key, value);
        }

        protected virtual string FixFormatCommon(string value) {
            return value;
        }
        #endregion
    }
}