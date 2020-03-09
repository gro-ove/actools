using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using AcManager.Controls;
using JetBrains.Annotations;
using AcManager.Pages.Dialogs;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Helpers;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;
using StringBasedFilter;
using WaitingDialog = FirstFloor.ModernUI.Dialogs.WaitingDialog;

namespace AcManager.Pages.Selected {
    public abstract class SelectedAcObjectViewModel : NotifyPropertyChanged {
        public static string SpecsFormat(string format, object value) {
            return format.Replace(@"…", value.ToInvariantString()).Replace(@"...", value.ToInvariantString());
        }

        private class InnerVersionInfoLabelConverter : IMultiValueConverter {
            public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) {
                var inSentence = string.Equals(parameter as string, "insentence", StringComparison.OrdinalIgnoreCase);

                if (!(values.FirstOrDefault() is IAcObjectFullAuthorshipInformation obj) ||
                        obj.Author != null || obj.Url == null && obj.Version == null) {
                    return inSentence ? ControlsStrings.AcObject_AuthorLabel.ToSentenceMember() : ControlsStrings.AcObject_AuthorLabel;
                }

                if (obj.Version != null) {
                    return inSentence ? ControlsStrings.AcObject_VersionLabel.ToSentenceMember() : ControlsStrings.AcObject_VersionLabel;
                }

                return inSentence ? ControlsStrings.AcObject_UrlLabel.ToSentenceMember() : ControlsStrings.AcObject_UrlLabel;
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

        public virtual void Load() { }

        public virtual void Unload() { }

        private ICommand _findInformationCommand;

        public ICommand FindInformationCommand
            =>
                    _findInformationCommand
                            ?? (_findInformationCommand =
                                    new DelegateCommand(() => { new FindInformationDialog((AcJsonObjectNew)SelectedAcObject).ShowDialog(); },
                                            () => SelectedAcObject is AcJsonObjectNew));

        private ICommand _changeIdCommand;

        public ICommand ChangeIdCommand => _changeIdCommand ?? (_changeIdCommand = new DelegateCommand(() => {
            var newId = Prompt.Show(AppStrings.AcObject_EnterNewId, AppStrings.Toolbar_ChangeId, SelectedObject.Id, @"?", AppStrings.AcObject_ChangeId_Tooltip);
            if (string.IsNullOrWhiteSpace(newId)) return;
            SelectedObject.ChangeIdCommand.Execute(newId);
        }));

        protected virtual string PrepareIdForInput(string id) {
            if (string.IsNullOrWhiteSpace(id)) return null;
            return SelectedAcObject is AcCommonSingleFileObject singleFile ?
                    id.ApartFromLast(singleFile.Extension, StringComparison.OrdinalIgnoreCase) : id;
        }

        protected virtual string FixIdFromInput(string id) {
            if (string.IsNullOrWhiteSpace(id)) return null;
            return SelectedAcObject is AcCommonSingleFileObject singleFile ?
                    id + singleFile.Extension : id;
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

        private DelegateCommand<string> _filterTagCommand;

        public DelegateCommand<string> FilterTagCommand
            => _filterTagCommand ?? (_filterTagCommand = new DelegateCommand<string>(o => { NewFilterTab($@"#{Filter.Encode(o)}"); }, o => o != null));

        [CanBeNull]
        protected DelegateCommand<string> InnerFilterCommand;

        public DelegateCommand<string> FilterCommand => InnerFilterCommand ?? (InnerFilterCommand = new DelegateCommand<string>(FilterExec));

        private DelegateCommand _clearNotes;

        public DelegateCommand ClearNotesCommand => _clearNotes ?? (_clearNotes = new DelegateCommand(() => {
            SelectedObject.Notes = null;
        }));

        protected void FilterRange([Localizable(false)] string key, double value, double range = 0.05, bool relative = true, double roundTo = 1.0,
                string postfix = "") {
            var delta = (relative ? range * value : range) / 2d;
            NewFilterTab(Equals(roundTo, 1d) && delta.Round(roundTo) < roundTo ?
                    $@"{key}={value.Round(roundTo).ToInvariantString()}{postfix}" :
                    string.Format(@"{0}>{1}{3} & {0}<{2}{3}", key, (Math.Max(value - delta, 0d).Round(roundTo) - Math.Min(roundTo, 1d)).ToInvariantString(),
                            ((value + delta).Round(roundTo) + Math.Min(roundTo, 1d)).ToInvariantString(), postfix));
        }

        protected void FilterRange([Localizable(false)] string key, TimeSpan value, TimeSpan range, string postfix = "") {
            NewFilterTab(string.Format(@"{0}>{1}{3} & {0}<{2}{3}", key, (value - range).ToProperString(),
                    (value + range).ToProperString(), postfix));
        }

        protected void FilterDistance([Localizable(false)] string key, double value, double range = 0.05, bool relative = true, double roundTo = 1.0) {
            FilterRange(key, value / 1e3, range, relative, roundTo, " km");
        }

        protected void FilterRange([Localizable(false)] string key, string value, double range = 0.05, bool relative = true, double roundTo = 1.0,
                string postfix = "") {
            if (!string.IsNullOrWhiteSpace(value) && FlexibleParser.TryParseDouble(value, out var actual)) {
                FilterRange(key, actual, range, relative, roundTo, postfix);
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
                    FilterRange(@"age", SelectedObject.Age.TotalDays, postfix: " days");
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

                    var yearString = SelectedObject.Year.ToString();
                    if (yearString.Length == 4) {
                        NewFilterTab($@"year:{yearString.Substring(0, 3)}?");
                    } else {
                        var start = (int)Math.Floor((SelectedObject.Year ?? 0) / 10d) * 10;
                        NewFilterTab($@"year>{start - 1} & year<{start + 10}");
                    }
                    break;

                case "notes":
                    NewFilterTab(SelectedObject.HasNotes ? @"notes+" : @"notes-");
                    break;
            }
        }
        #endregion

        #region Specs, fix formats
        private readonly List<Tuple<string, string, Func<string>, Action<string>>> _specs =
                new List<Tuple<string, string, Func<string>, Action<string>>>(7);

        protected void RegisterSpec([Localizable(false), NotNull] string key, [NotNull] string format, [NotNull] Func<string> getter,
                [NotNull] Action<string> setter) {
            _specs.Add(Tuple.Create(key, format, getter, setter));
        }

        protected void RegisterSpec([Localizable(false), NotNull] string key, [NotNull] string format, [NotNull] string propertyName) {
            RegisterSpec(key, format, () => {
                var type = SelectedObject.GetType();
                var property = type.GetProperty(propertyName)?.GetGetMethod();
                return property?.Invoke(SelectedObject, new object[0])?.ToString();
            }, v => {
                var type = SelectedObject.GetType();
                var property = type.GetProperty(propertyName)?.GetSetMethod();
                property?.Invoke(SelectedObject, new object[] { v });
            });
        }

        [NotNull]
        protected string GetFormat(string key) {
            return _specs.FirstOrDefault(x => x.Item1 == key)?.Item2 ?? @"…";
        }

        [CanBeNull]
        protected string GetSpecsValue(string key) {
            return _specs.FirstOrDefault(x => x.Item1 == key)?.Item3.Invoke();
        }

        protected void SetSpecsValue(string key, string value) {
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
            return value == null || Regex.IsMatch(value, @"^" + Regex.Escape(format).Replace(@"…|\.\.\.", @"-?\d+(?:\.\d+)?") + @"$");
        }

        protected virtual void FixFormat(string key) {
            var format = GetFormat(key);
            var originalValue = GetSpecsValue(key);
            if (originalValue == null) return;

            var fixedValue = FixFormatCommon(originalValue);
            var replacement = FlexibleParser.TryParseDouble(fixedValue, out var actualValue) ? actualValue.Round(0.01).ToInvariantString() : @"--";
            fixedValue = SpecsFormat(format, replacement);
            SetSpecsValue(key, originalValue.IndexOf('*') == -1 ? fixedValue : fixedValue + "*");
        }

        protected virtual string FixFormatCommon(string value) {
            return value;
        }
        #endregion
    }
}