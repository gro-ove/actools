using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using AcManager.Controls.Dialogs;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;

namespace AcManager.Pages.Lists {
    public sealed class Screenshot : Displayable {
        public Screenshot(string filename, DateTime creationTime, long size) {
            Filename = filename;
            CreationTime = creationTime;
            Size = size;
            DisplayName = Path.GetFileName(filename);
        }

        public string Filename { get; }

        public DateTime CreationTime { get; }

        public long Size { get; }
    }

    public partial class ScreenshotsListPage : ILoadableContent, IParametrizedUriContent {
        private static readonly Regex Filter = new Regex(@"\.(jpe?g|gif|bmp|png)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static Screenshot[] GetFiles(string filter) {
            var directory = FileUtils.GetDocumentsScreensDirectory();
            if (!Directory.Exists(directory)) return new Screenshot[0];
            return new DirectoryInfo(directory).GetFiles(filter).Where(x => Filter.IsMatch(x.Name))
                .OrderByDescending(x => x.CreationTime)
                .Select(x => new Screenshot(x.FullName, x.CreationTime, x.Length)).ToArray();
        }

        private string _filter;
        private Screenshot[] _images;

        public void OnUri(Uri uri) {
            var filter = uri.GetQueryParam("Filter");
            _filter = filter == null ? @"*" : $"*{filter}*";
        }

        public async Task LoadAsync(CancellationToken cancellationToken) {
            _images = await Task.Run(() => GetFiles(_filter), cancellationToken);
        }

        public void Load() {
            _images = GetFiles(_filter);
        }

        public ViewModel Model => (ViewModel)DataContext;

        public void Initialize() {
            DataContext = new ViewModel(_images);
            _images = null;

            InitializeComponent();
        }

        private void ScreenshotsListPage_OnLoaded(object sender, RoutedEventArgs e) {
            Model.Load();
        }

        private void ScreenshotsListPage_OnUnloaded(object sender, RoutedEventArgs e) {
            Model.Unload();
        }

        public class ViewModel {
            public Screenshot[] Screenshots { get; }

            public ViewModel(Screenshot[] screenshots) {
                Screenshots = screenshots;
            }

            public void Load() {}
            public void Unload() {}
        }

        private void Item_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            var screenshot = (sender as FrameworkElement)?.DataContext as Screenshot;
            if (screenshot == null) return;

            new ImageViewer(Model.Screenshots.Select(x => x.Filename), Model.Screenshots.IndexOf(screenshot),
                    4000, details: x => Path.GetFileName(x as string)).ShowDialog();
        }
    }
}
