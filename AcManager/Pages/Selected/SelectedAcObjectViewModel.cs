using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
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
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;
using StringBasedFilter;
using WaitingDialog = FirstFloor.ModernUI.Dialogs.WaitingDialog;

namespace AcManager.Pages.Selected {
    public abstract class SelectedAcObjectViewModel : NotifyPropertyChanged {
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

        private ICommand _cloneCommand;

        public ICommand CloneCommand => _cloneCommand ?? (_cloneCommand = new AsyncCommand(async () => {
            var newId = Prompt.Show(AppStrings.AcObject_EnterNewId, AppStrings.Toolbar_Clone, SelectedObject.Id, @"?");
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
    }
}