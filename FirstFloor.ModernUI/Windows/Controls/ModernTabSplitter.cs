using System.Linq;
using System.Windows;
using System.Windows.Controls;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Windows.Controls {
    public class ModernTabSplitter : GridSplitter {
        public ModernTabSplitter() {
            DefaultStyleKey = typeof(ModernTabSplitter);
            DragCompleted += OnDragCompleted;
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs routedEventArgs) {
            SetMaxWidth();
            SetWidth(LoadWidth());
        }

        protected override void OnVisualParentChanged(DependencyObject oldParent) {
            base.OnVisualParentChanged(oldParent);

            if (oldParent is FrameworkElement p) {
                p.SizeChanged -= OnParentSizeChanged;
            }

            if (Parent is FrameworkElement n) {
                n.SizeChanged += OnParentSizeChanged;
            }

            if (IsLoaded) {
                SetMaxWidth();
                SetWidth(LoadWidth());
            }
        }

        private void OnParentSizeChanged(object sender, SizeChangedEventArgs sizeChangedEventArgs) {
            if (IsLoaded) {
                SetMaxWidth();
            }
        }

        private void SetWidth(double width) {
            var column = GetTargetColumn();
            if (column != null) {
                column.Width = new GridLength(width, GridUnitType.Pixel);
            }
        }

        [CanBeNull]
        private ColumnDefinition GetTargetColumn() {
            return AdjustRightColumn ?
                    (Parent as Grid)?.ColumnDefinitions.LastOrDefault() :
                    (Parent as Grid)?.ColumnDefinitions.FirstOrDefault();
        }

        private void SetMaxWidth() {
            var target = GetTargetColumn();
            if (target == null) return;

            var grid = (Grid)Parent;
            var maxWidthAllowed = grid.ActualWidth - grid.ColumnDefinitions.Where(x =>
                    !ReferenceEquals(x, target)).Sum(x => x.Width.IsAbsolute ? x.ActualWidth : x.MinWidth);

            target.MaxWidth = maxWidthAllowed;
            if (target.ActualWidth > maxWidthAllowed - 10) {
                target.Width = new GridLength(maxWidthAllowed - 10, GridUnitType.Pixel);
            }
        }

        /*private double? GetWidth() {
            var target = GetTargetColumn();
            if (target == null) return null;

            var grid = (Grid)Parent;
            var columns = grid.ColumnDefinitions;
            var widths = columns.Where(x => !ReferenceEquals(x, target)).Sum(x => {
                Logging.Debug($"{x.Name}={x.Width.IsAbsolute} ({x.Width}); {x.ActualWidth}; {x.MinWidth}");
                return x.Width.IsAbsolute ? x.ActualWidth : x.MinWidth;
            });
            var maxWidthAllowed = grid.ActualWidth - widths;

            target.MaxWidth = 400;

            // Logging.Debug($"max allowed={maxWidthAllowed}; actual={target.ActualWidth}");

            if (target.ActualWidth > maxWidthAllowed) {
                target.Width = new GridLength(maxWidthAllowed, GridUnitType.Pixel);
                return maxWidthAllowed;
            }

            return Math.Min(maxWidthAllowed, target.ActualWidth);
        }*/

        private double? GetWidth() {
            return GetTargetColumn()?.ActualWidth;
        }

        private double LoadWidth() {
            return ValuesStorage.GetDouble(SaveKeyValue, InitialWidth);
        }

        private void SaveWidth(double value) {
            ValuesStorage.Set(SaveKeyValue, value);
        }

        private void OnDragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e) {
            if (SaveKey != null) {
                var value = GetWidth();
                if (value.HasValue) {
                    SaveWidth(value.Value);
                }
            }
        }

        public static readonly DependencyProperty AdjustRightColumnProperty = DependencyProperty.Register(nameof(AdjustRightColumn), typeof(bool),
                typeof(ModernTabSplitter), new PropertyMetadata(false, (o, e) => {
                    ((ModernTabSplitter)o)._adjustRightColumn = (bool)e.NewValue;
                }));

        private bool _adjustRightColumn;

        public bool AdjustRightColumn {
            get => _adjustRightColumn;
            set => SetValue(AdjustRightColumnProperty, value);
        }

        public static readonly DependencyProperty InitialWidthProperty = DependencyProperty.Register(nameof(InitialWidth), typeof(double),
                typeof(ModernTabSplitter), new PropertyMetadata(200d));

        public double InitialWidth {
            get => GetValue(InitialWidthProperty) as double? ?? 200d;
            set => SetValue(InitialWidthProperty, value);
        }

        private string SaveKeyValue => $"ModernTabSplitter:{SaveKey}";

        public static readonly DependencyProperty SaveKeyProperty = DependencyProperty.Register(nameof(SaveKey), typeof(string),
                typeof(ModernTabSplitter), new PropertyMetadata(@"ModernTab", OnSaveKeyChanged));

        public string SaveKey {
            get => (string)GetValue(SaveKeyProperty);
            set => SetValue(SaveKeyProperty, value);
        }

        private static void OnSaveKeyChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            //((ModernTabSplitter)o).OnSaveKeyChanged();
        }

        /*private void OnSaveKeyChanged() {
            if (!IsLoaded) return;
            SetWidth(LoadWidth());
        }*/
    }
}