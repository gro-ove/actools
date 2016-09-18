using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
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
    public abstract class SelectedAcObjectViewModel<T> : NotifyPropertyChanged, ISelectedAcObjectViewModel where T : AcCommonObject {
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
            (Application.Current.Windows.OfType<ModernWindow>().FirstOrDefault(x => x.IsActive)?.CurrentLinkGroup as LinkGroupFilterable)?.AddAndSelect(filter);
        }

        private ICommandExt _filterTagCommand;

        public ICommand FilterTagCommand => _filterTagCommand ?? (_filterTagCommand = new DelegateCommand<string>(o => {
            NewFilterTab($"tag:{Filter.Encode(o)}");
        }, o => o != null));

        [CanBeNull]
        protected ICommandExt InnerFilterCommand;

        public ICommand FilterCommand => InnerFilterCommand ?? (InnerFilterCommand = new DelegateCommand<string>(FilterExec));

        protected void FilterRange([Localizable(false)] string key, double value, double range = 0.05, bool relative = true, double roundTo = 1.0) {
            var delta = (relative ? range * value : range) / 2d;
            NewFilterTab(Equals(roundTo, 1d) && delta.Round(roundTo) < roundTo ?
                    $"{key}={value.Round(roundTo).ToInvariantString()}" :
                    string.Format(@"{0}>{1} & {0}<{2}", key, (Math.Max(value - delta, 0d).Round(roundTo) - Math.Min(roundTo, 1d)).ToInvariantString(),
                            ((value + delta).Round(roundTo) + Math.Min(roundTo, 1d)).ToInvariantString()));
        }

        protected void FilterRange([Localizable(false)] string key, string value, double range = 0.05, bool relative = true, double roundTo = 1.0) {
            double actual;
            if (!string.IsNullOrWhiteSpace(value) && FlexibleParser.TryParseDouble(value, out actual)) {
                FilterRange(key, actual, range, relative, roundTo);
            } else {
                NewFilterTab($"{key}-");
            }
        }

        protected virtual void FilterExec(string type) {
            var jsonObject = SelectedObject as AcJsonObjectNew;
            switch (type) {
                case "author":
                    if (jsonObject == null) return;
                    NewFilterTab(string.IsNullOrWhiteSpace(jsonObject.Author) ? @"author-" : $"author:{Filter.Encode(jsonObject.Author)}");
                    break;

                case "age":
                    FilterRange(@"age", SelectedObject.AgeInDays);
                    break;

                case "country":
                    if (jsonObject == null) return;
                    NewFilterTab(string.IsNullOrWhiteSpace(jsonObject.Country) ? @"country-" : $"country:{Filter.Encode(jsonObject.Country)}");
                    break;

                case "year":
                    NewFilterTab(SelectedObject.Year.HasValue ? $"year:{SelectedObject.Year}" : @"year-");
                    break;

                case "decade":
                    if (!SelectedObject.Year.HasValue) {
                        NewFilterTab(@"year-");
                    }

                    var start = (int)Math.Floor((SelectedObject.Year ?? 0) / 10d) * 10;
                    NewFilterTab($"year>{start - 1} & year<{start + 10}");
                    break;
            }
        }
        #endregion
    }
}