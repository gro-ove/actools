using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using AcManager.Internal;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Attached;
using FirstFloor.ModernUI.Windows.Media;
using JetBrains.Annotations;
using StringBasedFilter;

namespace AcManager.Pages.Settings {
    public partial class Search : IParametrizedUriContent, ILoadableContent {
        #region Attached properties
        public static string GetCategory(DependencyObject obj) {
            return (string)obj.GetValue(CategoryProperty);
        }

        public static void SetCategory(DependencyObject obj, string value) {
            obj.SetValue(CategoryProperty, value);
        }

        public static readonly DependencyProperty CategoryProperty = DependencyProperty.RegisterAttached("Category", typeof(string),
                typeof(Search), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.None));

        public static bool GetLimited(DependencyObject obj) {
            return obj.GetValue(LimitedProperty) as bool? == true;
        }

        public static void SetLimited(DependencyObject obj, bool value) {
            obj.SetValue(LimitedProperty, value);
        }

        public static readonly DependencyProperty LimitedProperty = DependencyProperty.RegisterAttached("Limited", typeof(bool),
                typeof(Search), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.None));

        public static string GetKeywords(DependencyObject obj) {
            return (string)obj.GetValue(KeywordsProperty);
        }

        public static void SetKeywords(DependencyObject obj, string value) {
            obj.SetValue(KeywordsProperty, value);
        }

        public static readonly DependencyProperty KeywordsProperty = DependencyProperty.RegisterAttached("Keywords", typeof(string),
                typeof(Search), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.None));

        public static bool GetInclude(DependencyObject obj) {
            return obj.GetValue(IncludeProperty) as bool? ?? true;
        }

        public static void SetInclude(DependencyObject obj, bool value) {
            obj.SetValue(IncludeProperty, value);
        }

        public static readonly DependencyProperty IncludeProperty = DependencyProperty.RegisterAttached("Include", typeof(bool),
                typeof(Search), new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.None));

        public static object GetLinkedTo(DependencyObject obj) {
            return obj.GetValue(LinkedToProperty);
        }

        public static void SetLinkedTo(DependencyObject obj, object value) {
            obj.SetValue(LinkedToProperty, value);
        }

        public static readonly DependencyProperty LinkedToProperty = DependencyProperty.RegisterAttached("LinkedTo", typeof(object),
                typeof(Search), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.None));

        public static string GetSubCategory(DependencyObject obj) {
            return (string)obj.GetValue(SubCategoryProperty);
        }

        public static void SetSubCategory(DependencyObject obj, string value) {
            obj.SetValue(SubCategoryProperty, value);
        }

        public static readonly DependencyProperty SubCategoryProperty = DependencyProperty.RegisterAttached("SubCategory", typeof(string),
                typeof(Search), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits));
        #endregion

        private static readonly Dictionary<string, string> Namespaces = new Dictionary<string, string> {
            ["AcManager.Pages.Settings"] = "Content Manager",
            ["AcManager.Pages.AcSettings"] = "Assetto Corsa"
        };

        private static readonly string[] Ignored = {
            "Search",

            "SettingsPage",
            "SettingsQuickSwitches",
            "SettingsPlugins",
            "SettingsLapTimes",
            "SettingsDev",
            "SettingsDebug",

            "AcSettingsControls",
            "AcSettingsControls_Keyboard",
            "AcSettingsControls_Wheel",
            "AcSettingsControls_Wheel_Buttons",
            "AcSettingsControls_Wheel_Main",
            "AcSettingsDamageDisplayer",
            "AcSettingsPage",
            "AcSettingsPython",
            "PresetsPerMode",
        };

        private IFilter<string> _filter;

        public void OnUri(Uri uri) {
            _filter = Filter.Create(StringTester.Instance, uri.GetQueryParam("Filter") ?? "*");
        }

        private bool TestKeywords([CanBeNull] string keywords) {
            return keywords?.Split(';').Select(x => x.Trim()).Where(x => x.Length > 0).Any(z => _filter.Test(z.Trim())) == true;
        }

        private bool PreTestElement([CanBeNull] FrameworkElement element) {
            if (element == null || !GetInclude(element)) return false;
            if (element is TextBlock || element is Button) return false;
            return true;
        }

        private bool TestElement([CanBeNull] FrameworkElement element) {
            if (element == null) return false;

            while (GetLinkedTo(element) is FrameworkElement refElement) {
                element = refElement;
            }

            if (LimitedService.GetLimited(element) && !AppKeyHolder.IsAllRight) {
                return false;
            }

            return element.FindLogicalChildren<TextBlock>().Any(z => _filter.Test(z.Text)) ||
                    element.FindLogicalChildren<Label>().Any(z => _filter.Test(z.Content?.ToString() ?? "")) ||
                    TestKeywords(GetKeywords(element)) ||
                    TestKeywords(GetKeywords(element.Parent));
        }

        private string GetSubCategoryFromHeader([CanBeNull] FrameworkElement element) {
            if (element == null) return null;

            var textBlock = (element.Parent as Panel)?.Children.OfType<FrameworkElement>().TakeWhile(x => !ReferenceEquals(x, element))
                                                      .OfType<TextBlock>().LastOrDefault();
            if (textBlock == null) return null;
            return GetSubCategory(textBlock) ?? textBlock.Text;
        }

        public async Task LoadAsync(CancellationToken cancellationToken) {
            var s = Stopwatch.StartNew();

            var types = Assembly.GetExecutingAssembly().GetTypes();
            Logging.Debug($"Get types: {s.Elapsed.TotalMilliseconds:F1} ms");

            s.Restart();
            var filteredTypes = types.Where(x => !x.IsAbstract && Namespaces.Keys.Contains(x.Namespace ?? "") && !Ignored.Contains(x.Name) &&
                    x.IsSubclassOf(typeof(UserControl))).ToList();
            Logging.Debug($"Filter types: {s.Elapsed.TotalMilliseconds:F1} ms");

            s.Restart();
            var pages = new List<UserControl>();
            foreach (var x in filteredTypes) {
                pages.Add((UserControl)Activator.CreateInstance(x));
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
            }
            Logging.Debug($"Creating pages: {s.Elapsed.TotalMilliseconds:F1} ms ({pages.Count} pages)");

            s.Restart();
            var panels = pages.SelectMany(x => x.FindLogicalChildren<StackPanel>().Select(y => new {
                Panel = y,
                NamespaceType = Namespaces.GetValueOrDefault(x.GetType().Namespace ?? "")
            })).Select(x => new {
                Category = GetCategory(x.Panel),
                Limited = GetLimited(x.Panel),
                x.NamespaceType,
                x.Panel
            }).Where(x => x.Category != null && (!x.Limited || AppKeyHolder.IsAllRight)).ToList();
            Logging.Debug($"Finding panels: {s.Elapsed.TotalMilliseconds:F1} ms ({panels.Count} panels)");

            s.Restart();
            var blocks = panels.GroupBy(x => string.IsNullOrWhiteSpace(x.NamespaceType) ? x.Category : $"{x.NamespaceType}/{x.Category}").Select(x => new {
                Category = x.Key,
                Blocks = x.SelectMany(y => y.Panel.Children.OfType<FrameworkElement>().Where(PreTestElement)).ToList()
            }).ToList();
            Logging.Debug($"Finding blocks: {s.Elapsed.TotalMilliseconds:F1} ms ({blocks.Count} blocks)");

            s.Restart();
            var filtered = blocks.Select(x => new {
                x.Category,
                Blocks = x.Blocks.Where(TestElement).ToList()
            }).ToList();
            Logging.Debug($"Filtering: {s.Elapsed.TotalMilliseconds:F1} ms ({filtered.Count} filtered)");

            s.Restart();
            var resultPanels = new List<Panel>();
            foreach (var category in filtered.Where(x => x.Blocks.Count > 0)) {
                var panelItems = new StackPanel {
                    Margin = new Thickness(20, 0, 0, 0),
                    Children = {
                        new TextBlock {
                            Text = category.Category,
                            Style = TryFindResource("Heading1") as Style,
                            Margin = new Thickness(-20, resultPanels.Count == 0 ? 0 : 20, 0, 8)
                        }
                    }
                };

                string currentSubCategory = null;
                void SetSubCategory(string value) {
                    if (value == currentSubCategory || value == null) return;

                    var first = panelItems.Children.Count <= 1;
                    currentSubCategory = value;

                    panelItems.Children.Add(new TextBlock {
                        Text = currentSubCategory,
                        Style = TryFindResource(first ? "SettingsPanel.Heading2.First" : "SettingsPanel.Heading2") as Style
                    });
                }

                foreach (var item in category.Blocks) {
                    SetSubCategory(GetSubCategoryFromHeader(item));
                    var panel = (Panel)item.Parent;
                    panel.Children.Remove(item);
                    item.DataContext = item.DataContext ?? panel.DataContext;
                    item.Margin = new Thickness(item.Margin.Left, 0, 0, 8);
                    panelItems.Children.Add(item);
                }

                resultPanels.Add(panelItems);
            }

            Logging.Debug($"Added: {s.Elapsed.TotalMilliseconds:F1} ms ({resultPanels.Count} children)");

            var left = (resultPanels.Count / 2d).Ceiling().FloorToInt();
            foreach (var panel in resultPanels.Take(left)) {
                LeftPanel.Children.Add(panel);
            }
            foreach (var panel in resultPanels.Skip(left)) {
                RightPanel.Children.Add(panel);
            }
        }

        public void Load() {
            // throw new NotImplementedException();
        }

        public void Initialize() {
            InitializeComponent();
            DataContext = new ViewModel();
            this.AddWidthCondition(1080).Add(v => Grid.Columns = v ? 2 : 1);
        }

        private void OnLoaded(object sender, RoutedEventArgs e) {
            /*var thumb = ScaleSlider.FindVisualChild<Thumb>();
            if (thumb != null) {
                thumb.DragCompleted += (s, a) => ScaleSlider.RemoveFocus();
            }*/
        }

        public class ViewModel : NotifyPropertyChanged {

        }
    }
}
