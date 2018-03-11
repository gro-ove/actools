using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using AcManager.Tools.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Media;
using JetBrains.Annotations;
using Newtonsoft.Json;
using StringBasedFilter;

namespace AcManager.Controls {
    public sealed class OriginIcon : Decorator {
        static OriginIcon() {
            FilesStorage.Instance.Watcher(ContentCategory.OriginIcons).Update += OnUpdate;
        }

        private static void OnUpdate(object sender, EventArgs args) {
            _icons = null;
            Cache.Clear();
            if (!SettingsHolder.Content.MarkKunosContent) return;
            foreach (var image in VisualTreeHelperEx.GetAllOfType<OriginIcon>()) {
                image.OnAuthorChanged(image.Author);
            }
        }

        public OriginIcon() {
            MaxWidth = 16d;
            Height = 16d;
        }

        public static readonly DependencyProperty AuthorProperty = DependencyProperty.Register(nameof(Author), typeof(string),
                typeof(OriginIcon), new PropertyMetadata(OnAuthorChanged));

        public string Author {
            get => (string)GetValue(AuthorProperty);
            set => SetValue(AuthorProperty, value);
        }

        private static void OnAuthorChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((OriginIcon)o).OnAuthorChanged((string)e.NewValue);
        }

        private OriginIconDescription _description;

        private void SetIcon([CanBeNull] OriginIconDescription description) {
            if (ReferenceEquals(_description, description)) return;

            _description?.Icon.Release(Child as FrameworkElement);
            if (description == null) {
                Child = null;
                ToolTip = null;
            } else {
                Child = description.Icon.Get();
                ToolTip = description.Name;
            }

            _description = description;
        }

        private void OnAuthorChanged(string newValue) {
            if (!SettingsHolder.Content.MarkKunosContent) {
                SetIcon(null);
                return;
            }

            SetIcon(FindIcon(newValue));
        }

        [CanBeNull]
        private static OriginIconDescription[] _icons;

        [NotNull]
        private static readonly Dictionary<string, OriginIconDescription> Cache = new Dictionary<string, OriginIconDescription>();

        [CanBeNull]
        private static OriginIconDescription FindIcon([CanBeNull] string obj) {
            if (string.IsNullOrWhiteSpace(obj)) {
                return null;
            }

            var key = obj.ToLowerInvariant();
            if (Cache.TryGetValue(key, out var cached)) {
                return cached;
            }

            if (_icons == null) {
                _icons = LoadIcons().Where(x => x.Icon != null).ToArray();
            }

            for (var i = _icons.Length - 1; i >= 0; i--) {
                var icon = _icons[i];
                if (icon.Test(obj)) {
                    Cache[key] = icon;
                    return icon;
                }
            }

            Cache[key] = default(OriginIconDescription);
            return null;
        }

        [UsedImplicitly]
        private class OriginIconDescription {
            public string Name { get; }
            public ElementPool Icon { get; }

            private readonly IFilter<string> _filter;

            [JsonConstructor]
            private OriginIconDescription([NotNull] string name, string authorFilter, string icon) {
                Name = ContentUtils.Translate(name);
                Icon = ContentUtils.GetIcon(icon ?? name + @".png", ContentCategory.OriginIcons, 16);
                _filter = Filter.Create(StringTester.Instance, authorFilter);
            }

            public bool Test(string obj) {
                return _filter.Test(obj);
            }
        }

        private static IEnumerable<OriginIconDescription> LoadIcons() {
            return FilesStorage.Instance.GetContentFilesFiltered(@"*.json", ContentCategory.OriginIcons).Select(x => x.Filename).SelectMany(x => {
                try {
                    return JsonConvert.DeserializeObject<OriginIconDescription[]>(File.ReadAllText(x));
                } catch (Exception e) {
                    Logging.Warning($"Cannot load file {Path.GetFileName(x)}: {e}");
                    return new OriginIconDescription[0];
                }
            });
        }
    }
}