using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AcManager.Pages.Windows;
using AcManager.Tools;
using AcManager.Tools.ContentInstallation;
using AcManager.Tools.ContentInstallation.Entries;
using AcManager.Tools.ContentInstallation.Implementations;
using AcManager.Tools.Managers.Plugins;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Pages.Dialogs {
    public class AdditionalContentEntryTemplateSelectorInner : DataTemplateSelector {
        public DataTemplate BasicTemplate { get; set; }
        public DataTemplate TrackTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container) {
            return item is TrackContentEntry ? TrackTemplate : BasicTemplate;
        }
    }

    public partial class InstallAdditionalContentDialog : INotifyPropertyChanged {
        private static InstallAdditionalContentDialog _dialog;

        public static void Initialize() {
            ContentInstallationManager.Instance.TaskAdded += OnTaskAdded;
            ContentInstallationManager.Instance.DownloadList.CollectionChanged += OnQueueChanged;
        }

        private static readonly Busy TaskAddedBusy = new Busy();
        private static readonly Busy QueueChangedBusy = new Busy();

        private static void OnTaskAdded(object sender, EventArgs e) {
            TaskAddedBusy.DoDelay(ShowInstallDialog, 100);
        }

        private static void OnQueueChanged(object o, NotifyCollectionChangedEventArgs a) {
            QueueChangedBusy.DoDelay(() => {
                if (ContentInstallationManager.Instance.DownloadList.Count == 0 && _dialog?.IsActive != true) {
                    CloseInstallDialog();
                }
            }, 100);
        }

        public static void ShowInstallDialog() {
            if (_dialog == null) {
                _dialog = new InstallAdditionalContentDialog();

                if (Application.Current?.MainWindow is MainWindow) {
                    _dialog.Owner = Application.Current?.MainWindow;
                    _dialog.ShowInTaskbar = false;
                    _dialog.WindowStyle = WindowStyle.ToolWindow;
                }

                _dialog.Show();
                _dialog.Closed += (sender, args) => {
                    if (IsAlone) {
                        ContentInstallationManager.Instance.Cancel();
                    }

                    _dialog = null;
                };
            }
        }

        private static void CloseInstallDialog() {
            _dialog?.Close();
        }

        private static bool IsAlone => Application.Current?.Windows.OfType<MainWindow>().FirstOrDefault()?.IsVisible != true;

        public BetterListCollectionView DownloadListView { get; }

        private InstallAdditionalContentDialog() {
            UpdateSevenZipPluginMissing();
            PluginsManager.Instance.PluginEnabled += OnPlugin;
            PluginsManager.Instance.PluginDisabled += OnPlugin;
            this.OnActualUnload(() => {
                PluginsManager.Instance.PluginEnabled -= OnPlugin;
                PluginsManager.Instance.PluginDisabled -= OnPlugin;
            });

            DownloadListView = new BetterListCollectionView(ContentInstallationManager.Instance.DownloadList);
            DownloadListView.SortDescriptions.Add(new SortDescription(nameof(ContentInstallationEntry.AddedDateTime), ListSortDirection.Descending));

            PluginsManager.Instance.UpdateIfObsolete().Forget();
            RecommendedListView = new BetterListCollectionView(PluginsManager.Instance.List);
            RecommendedListView.SortDescriptions.Add(new SortDescription(nameof(PluginEntry.Name), ListSortDirection.Ascending));
            RecommendedListView.Filter = o => (o as PluginEntry)?.Id == SevenZipContentInstallator.PluginId;

            DataContext = this;
            InitializeComponent();
            InputBindings.AddRange(new[] {
                new InputBinding(new DelegateCommand(ArgumentsHandler.OnPaste), new KeyGesture(Key.V, ModifierKeys.Control)),
            });
            Buttons = new[] { IsAlone ? CloseButton : CreateCloseDialogButton(UiStrings.Toolbar_Hide, true, false, MessageBoxResult.None) };

            if (ContentInstallationManager.Instance.DownloadList.Any(x => x.SevenZipInstallatorWouldNotHurt)) {
                SevenZipWarning.Visibility = Visibility.Visible;
                SevenZipObsoleteWarning.Visibility = Visibility.Visible;
            } else {
                ContentInstallationManager.Instance.DownloadList.ItemPropertyChanged += OnItemPropertyChanged;
                this.OnActualUnload(() => {
                    ContentInstallationManager.Instance.DownloadList.ItemPropertyChanged -= OnItemPropertyChanged;
                });
            }
        }

        private void OnItemPropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(ContentInstallationEntry.SevenZipInstallatorWouldNotHurt)) {
                SevenZipWarning.Visibility = Visibility.Visible;
                SevenZipObsoleteWarning.Visibility = Visibility.Visible;
            }
        }

        public BetterListCollectionView RecommendedListView { get; }

        private bool? _isSevenZipPluginMissing;

        public bool IsSevenZipPluginMissing {
            get => _isSevenZipPluginMissing ?? false;
            set {
                if (value == _isSevenZipPluginMissing) return;
                var oldValue = _isSevenZipPluginMissing;
                _isSevenZipPluginMissing = value;
                OnPropertyChanged();

                if (oldValue == true && value == false) {
                    foreach (var entry in ContentInstallationManager.Instance.DownloadList.Where(x => x.SevenZipInstallatorWouldNotHurt &&
                            x.CancelCommand.IsAbleToExecute && x.State == ContentInstallationEntryState.WaitingForConfirmation)) {
                        ContentInstallationManager.Instance.InstallAsync(entry.Source).ContinueWith(async v => {
                            if (!v.Result) {
                                await Task.Delay(1);
                                ContentInstallationManager.Instance.InstallAsync(entry.LoadedFilename ?? entry.Source).Forget();
                            }
                        }, TaskContinuationOptions.OnlyOnRanToCompletion);
                        entry.KeepLoaded = true;
                        entry.CancelCommand.Execute();
                    }
                }
            }
        }

        private bool _isSevenZipPluginObsolete;

        public bool IsSevenZipPluginObsolete {
            get => _isSevenZipPluginObsolete;
            set {
                if (Equals(value, _isSevenZipPluginObsolete)) return;
                _isSevenZipPluginObsolete = value;
                OnPropertyChanged();
            }
        }

        private void UpdateSevenZipPluginMissing() {
            IsSevenZipPluginMissing = !PluginsManager.Instance.IsPluginEnabled(SevenZipContentInstallator.PluginId);
            IsSevenZipPluginObsolete = PluginsManager.Instance.GetById(SevenZipContentInstallator.PluginId)?.Version.IsVersionOlderThan(
                    SevenZipContentInstallator.MinimalRecommendedVersion) == true;
        }

        private void OnPlugin(object o, AppAddonEventHandlerArgs e) {
            if (e.PluginId == SevenZipContentInstallator.PluginId) {
                UpdateSevenZipPluginMissing();
            }
        }

        private void OnClosed(object sender, EventArgs e) {}

        private void OnDrop(object sender, DragEventArgs e) {
            ArgumentsHandler.OnDrop(sender, e);
        }

        private void OnDragEnter(object sender, DragEventArgs e) {
            ArgumentsHandler.OnDragEnter(sender, e);
        }

        private void OnPasswordBoxKeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Enter) {
                e.Handled = true;
                (((FrameworkElement)sender).DataContext as ContentInstallationEntry)?.ApplyPasswordCommand.Execute();
            }
        }

        private void OnKeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Enter) {
                e.Handled = true;
            }
        }

        private void OnItemMouseDown(object sender, MouseButtonEventArgs e) {
            ItemsListBox.SelectedItem = ((FrameworkElement)sender).DataContext as ContentInstallationEntry;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        private void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
