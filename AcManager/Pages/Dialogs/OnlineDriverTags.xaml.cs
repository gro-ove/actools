using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using AcManager.Tools.Managers.Online;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;

using DriverTag = AcManager.Tools.Managers.Online.ServerEntry.DriverTag;

namespace AcManager.Pages.Dialogs {
    public partial class OnlineDriverTags {
        private ViewModel Model => (ViewModel)DataContext;

        public OnlineDriverTags() {
            DataContext = new ViewModel();
            InitializeComponent();
            Buttons = new[] { CloseButton };
        }

        public class ViewModel : NotifyPropertyChanged {
            public BetterObservableCollection<TagEntry> Entries { get; }

            private IEnumerable<TagEntry> GetList() {
                return DriverTag.GetTags().Select(x => new TagEntry(this, x));
            }

            public ViewModel() {
                Entries = new BetterObservableCollection<TagEntry>(GetList());
            }

            private DelegateCommand _newTagCommand;

            public DelegateCommand NewTagCommand => _newTagCommand ?? (_newTagCommand = new DelegateCommand(() => {
                var dialog = new OnlineNewDriverTag();
                dialog.ShowDialog();

                if (dialog.IsResultOk) {
                    Update();
                }
            }));

            private void Update() {
                Entries.ReplaceEverythingBy(GetList());
            }

            private class RemovedTag {
                public RemovedTag(DriverTag tag) {
                    Tag = tag;
                    Names = DriverTag.GetNames(tag.Id).ToList();
                }

                public DriverTag Tag { get; }

                public List<string> Names { get; }
            }

            private static readonly List<RemovedTag> Removed = new List<RemovedTag>();

            public void Remove(DriverTag tag) {
                Removed.Add(new RemovedTag(tag));
                DriverTag.Remove(tag.Id);
                Update();
                _restoreDeletedCommand.RaiseCanExecuteChanged();
            }

            private DelegateCommand _restoreDeletedCommand;

            public DelegateCommand RestoreDeletedCommand => _restoreDeletedCommand ?? (_restoreDeletedCommand = new DelegateCommand(() => {
                var removed = Removed.LastOrDefault();
                if (removed != null) {
                    Removed.Remove(removed);
                    _restoreDeletedCommand.RaiseCanExecuteChanged();

                    var tagId = DriverTag.CreateTag(removed.Tag.DisplayName, removed.Tag.Color).Id;
                    DriverTag.SetNames(tagId, removed.Names);
                    Update();
                }
            }, () => Removed.Count > 0));
        }

        public class TagEntry : NotifyPropertyChanged {
            private readonly ViewModel _model;
            public DriverTag Tag { get; }

            public bool IsBuiltIn { get; }

            public TagEntry(ViewModel model, DriverTag tag) {
                _model = model;

                Tag = tag;
                IsBuiltIn = ReferenceEquals(tag, DriverTag.FriendTag);
            }

            private string _data;

            public string Data {
                get { return _data ?? (_data = DriverTag.GetNames(Tag.Id).Append("").JoinToString('\n')); }
                set {
                    if (value == null) value = string.Empty;
                    if (Equals(value, _data)) return;
                    _data = value;
                    OnPropertyChanged();

                    if (!_updating) {
                        UpdateNames();
                    }
                }
            }

            private bool _updating;

            private async void UpdateNames() {
                if (_updating) return;

                try {
                    _updating = true;
                    await Task.Delay(300);
                    DriverTag.SetNames(Tag.Id, _data.Split('\n').Select(x => x.Trim()).Where(x => x.Length > 0));
                } finally {
                    _updating = false;
                }
            }

            private DelegateCommand _removeCommand;

            public DelegateCommand DeleteCommand => _removeCommand ?? (_removeCommand = new DelegateCommand(() => {
                if (ShowMessage("Tag will be deleted. Are you sure?", $"Delete Tag “{Tag.DisplayName}”",
                        MessageBoxButton.YesNo) == MessageBoxResult.Yes) {
                    _model.Remove(Tag);
                }
            }, () => !IsBuiltIn));
        }

        private void OnClosed(object sender, EventArgs e) {}
    }
}