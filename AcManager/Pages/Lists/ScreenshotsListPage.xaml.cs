using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AcManager.Controls.Dialogs;
using AcManager.Tools.Filters;
using AcManager.Tools.Filters.Testers;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers.InnerHelpers;
using AcTools.Utils;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using StringBasedFilter;

namespace AcManager.Pages.Lists {
    public sealed class Screenshot : Displayable {
        public Screenshot(FileInfo fileInfo) {
            Filename = fileInfo.FullName;
            CreationTime = fileInfo.CreationTime;
            LastWriteTime = fileInfo.LastWriteTime;
            Size = fileInfo.Length;
            DisplayName = fileInfo.Name;
        }

        public string Filename { get; }
        public DateTime CreationTime { get; }
        public DateTime LastWriteTime { get; }
        public long Size { get; }

        private DelegateCommand _viewInExplorerCommand;

        public DelegateCommand ViewInExplorerCommand => _viewInExplorerCommand ?? (_viewInExplorerCommand = new DelegateCommand(() => {
            WindowsHelper.ViewFile(Filename);
        }));

        private bool _isDeleted;

        public bool IsDeleted {
            get => _isDeleted;
            private set {
                if (Equals(value, _isDeleted)) return;
                _isDeleted = value;
                OnPropertyChanged();
            }
        }

        private bool _isDeleting;

        public bool IsDeleting {
            get => _isDeleting;
            private set {
                if (value == _isDeleting) return;
                _isDeleting = value;
                OnPropertyChanged();
            }
        }

        private DelegateCommand _deleteCommand;

        public DelegateCommand DeleteCommand => _deleteCommand ?? (_deleteCommand = new DelegateCommand(() => {
            var isDeleting = IsDeleting;
            try {
                IsDeleting = true;
                FileUtils.Recycle(Filename);
                if (!File.Exists(Filename)) {
                    IsDeleted = true;
                }
            } finally {
                IsDeleting = isDeleting;
            }
        }));
    }

    public partial class ScreenshotsListPage : ILoadableContent, IParametrizedUriContent {
        private static readonly Regex ImageFilter = new Regex(@"\.(jpe?g|gif|bmp|png)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static IEnumerable<Screenshot> GetFiles(string directory, IFilter<FileInfo> filter) {
            if (!Directory.Exists(directory)) return new Screenshot[0];
            return new DirectoryInfo(directory).GetFiles("*.*").Where(x => ImageFilter.IsMatch(x.Name) && filter.Test(x))
                                               .OrderByDescending(x => x.CreationTime)
                                               .Select(x => new Screenshot(x));
        }

        private static IEnumerable<Screenshot> GetUpdateEntries(string directory, IFilter<FileInfo> filter, IList<Screenshot> existing) {
            if (!Directory.Exists(directory)) return new Screenshot[0];
            return new DirectoryInfo(directory).GetFiles("*.*").Where(x => ImageFilter.IsMatch(x.Name) && filter.Test(x))
                                               .OrderByDescending(x => x.CreationTime)
                                               .Select(x => {
                                                   var e = existing.FirstOrDefault(y => string.Equals(y.DisplayName, x.Name, StringComparison.Ordinal));
                                                   if (e != null && e.LastWriteTime == x.LastWriteTime) return e;
                                                   return new Screenshot(x);
                                               });
        }

        private string _directory;
        private IFilter<FileInfo> _filter;
        private List<Screenshot> _images;

        public void OnUri(Uri uri) {
            var filter = uri.GetQueryParam("Filter");
            _filter = filter == null ? Filter<FileInfo>.Any : Filter.Create(FileTester.Instance, filter);
        }

        public async Task LoadAsync(CancellationToken cancellationToken) {
            _directory = FileUtils.GetDocumentsScreensDirectory();
            _images = await Task.Run(() => GetFiles(_directory, _filter).ToList(), cancellationToken);
        }

        public void Load() {
            _directory = FileUtils.GetDocumentsScreensDirectory();
            _images = GetFiles(_directory, _filter).ToList();
        }

        public ViewModel Model => (ViewModel)DataContext;

        public void Initialize() {
            DataContext = new ViewModel(_directory, _filter, _images);
            this.OnActualUnload(Model);
            _images = null;
            InitializeComponent();
        }

        public class ViewModel : IDisposable {
            public ChangeableObservableCollection<Screenshot> Screenshots { get; }

            private readonly string _directory;
            private readonly IFilter<FileInfo> _filter;
            private readonly DirectoryWatcher _watcher;
            private readonly Busy _busy = new Busy(true);

            public ViewModel(string directory, IFilter<FileInfo> filter, IEnumerable<Screenshot> screenshots) {
                Screenshots = new ChangeableObservableCollection<Screenshot>(screenshots);
                Screenshots.ItemPropertyChanged += OnPropertyChanged;
                _directory = directory;
                _filter = filter;
                _watcher = new DirectoryWatcher(directory);
                _watcher.Update += OnUpdate;
            }

            private void OnUpdate(object sender, FileSystemEventArgs fileSystemEventArgs) {
                _busy.DoDelay(() => Screenshots.ReplaceEverythingBy(GetUpdateEntries(_directory, _filter, Screenshots)), 300);
            }

            private void OnPropertyChanged(object sender, PropertyChangedEventArgs e) {
                if (e.PropertyName == nameof(Screenshot.IsDeleting)) {
                    _busy.Delay(1000);
                }

                if (e.PropertyName == nameof(Screenshot.IsDeleted)) {
                    Screenshots.Remove((Screenshot)sender);
                }
            }

            public void Dispose() {
                _watcher?.Dispose();
            }
        }

        private void OnItemClick(object sender, MouseButtonEventArgs e) {
            if (Keyboard.Modifiers == ModifierKeys.None && !e.Handled && (sender as FrameworkElement)?.DataContext is Screenshot screenshot) {
                e.Handled = true;
                new ImageViewer(Model.Screenshots.Select(x => x.Filename), Model.Screenshots.IndexOf(screenshot),
                        4000, details: x => Path.GetFileName(x as string)).ShowDialog();
            }
        }

        private void OnContextMenu(object sender, MouseButtonEventArgs e) {
            if (!e.Handled && ((FrameworkElement)sender).DataContext is Screenshot image) {
                e.Handled = true;
                new ContextMenu()
                        .AddItem("View In Explorer", image.ViewInExplorerCommand)
                        .AddItem("Remove To Recycle Bin", image.DeleteCommand)
                        .IsOpen = true;
            }
        }

        private void OnRightButtonDown(object sender, MouseButtonEventArgs e) {
            e.Handled = true;
        }
    }
}
