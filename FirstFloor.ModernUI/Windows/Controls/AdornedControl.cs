using System;
using System.Collections;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Media;

namespace FirstFloor.ModernUI.Windows.Controls {
    public enum AdornerPlacement {
        Inside,
        Outside
    }

    public class FrameworkElementAdorner : Adorner {
        // The framework element that is the adorner.
        private readonly FrameworkElement _child;

        // Placement of the child.
        private readonly AdornerPlacement _horizontalAdornerPlacement = AdornerPlacement.Inside;
        private readonly AdornerPlacement _verticalAdornerPlacement = AdornerPlacement.Inside;

        // Offset of the child.
        private readonly double _offsetX;
        private readonly double _offsetY;

        public FrameworkElementAdorner(FrameworkElement adornerChildElement, UIElement adornedElement) : base(adornedElement) {
            _child = adornerChildElement;

            AddLogicalChild(adornerChildElement);
            AddVisualChild(adornerChildElement);
        }

        public FrameworkElementAdorner(FrameworkElement adornerChildElement, FrameworkElement adornedElement, AdornerPlacement horizontalAdornerPlacement,
                AdornerPlacement verticalAdornerPlacement, double offsetX, double offsetY) : base(adornedElement) {
            _child = adornerChildElement;
            _horizontalAdornerPlacement = horizontalAdornerPlacement;
            _verticalAdornerPlacement = verticalAdornerPlacement;
            _offsetX = offsetX;
            _offsetY = offsetY;

            adornedElement.SizeChanged += adornedElement_SizeChanged;

            AddLogicalChild(adornerChildElement);
            AddVisualChild(adornerChildElement);
        }

        /// <summary>
        /// Event raised when the adorned control's size has changed.
        /// </summary>
        private void adornedElement_SizeChanged(object sender, SizeChangedEventArgs e) {
            InvalidateMeasure();
        }

        // Position of the child (when not set to NaN).
        public double PositionX { get; set; } = double.NaN;

        public double PositionY { get; set; } = double.NaN;

        protected override Size MeasureOverride(Size constraint) {
            _child.Measure(constraint);
            return _child.DesiredSize;
        }

        /// <summary>
        /// Determine the X coordinate of the child.
        /// </summary>
        private double DetermineX() {
            switch (_child.HorizontalAlignment) {
                case HorizontalAlignment.Left:
                    if (_horizontalAdornerPlacement == AdornerPlacement.Outside) {
                        return -_child.DesiredSize.Width + _offsetX;
                    }
                    return _offsetX;
                case HorizontalAlignment.Right:
                    if (_horizontalAdornerPlacement == AdornerPlacement.Outside) {
                        var adornedWidth = AdornedElement.ActualWidth;
                        return adornedWidth + _offsetX;
                    } else {
                        var adornerWidth = _child.DesiredSize.Width;
                        var adornedWidth = AdornedElement.ActualWidth;
                        var x = adornedWidth - adornerWidth;
                        return x + _offsetX;
                    }
                case HorizontalAlignment.Center: {
                    var adornerWidth = _child.DesiredSize.Width;
                    var adornedWidth = AdornedElement.ActualWidth;
                    var x = adornedWidth / 2 - adornerWidth / 2;
                    return x + _offsetX;
                }
                case HorizontalAlignment.Stretch:
                    return 0.0;
            }

            return 0.0;
        }

        /// <summary>
        /// Determine the Y coordinate of the child.
        /// </summary>
        private double DetermineY() {
            switch (_child.VerticalAlignment) {
                case VerticalAlignment.Top:
                    if (_verticalAdornerPlacement == AdornerPlacement.Outside) {
                        return -_child.DesiredSize.Height + _offsetY;
                    }
                    return _offsetY;
                case VerticalAlignment.Bottom:
                    if (_verticalAdornerPlacement == AdornerPlacement.Outside) {
                        var adornedHeight = AdornedElement.ActualHeight;
                        return adornedHeight + _offsetY;
                    } else {
                        var adornerHeight = _child.DesiredSize.Height;
                        var adornedHeight = AdornedElement.ActualHeight;
                        var x = adornedHeight - adornerHeight;
                        return x + _offsetY;
                    }
                case VerticalAlignment.Center: {
                        var adornerHeight = _child.DesiredSize.Height;
                        var adornedHeight = AdornedElement.ActualHeight;
                        var x = adornedHeight / 2 - adornerHeight / 2;
                        return x + _offsetY;
                    }
                case VerticalAlignment.Stretch:
                    return 0d;
                default:
                    return 0d;
            }
        }

        /// <summary>
        /// Determine the width of the child.
        /// </summary>
        private double DetermineWidth() {
            if (!double.IsNaN(PositionX)) return _child.DesiredSize.Width;
            switch (_child.HorizontalAlignment) {
                case HorizontalAlignment.Left:
                    return _child.DesiredSize.Width;
                case HorizontalAlignment.Right:
                    return _child.DesiredSize.Width;
                case HorizontalAlignment.Center:
                    return _child.DesiredSize.Width;
                case HorizontalAlignment.Stretch:
                    return AdornedElement.ActualWidth;
                default:
                    return 0d;
            }
        }

        /// <summary>
        /// Determine the height of the child.
        /// </summary>
        private double DetermineHeight() {
            if (!double.IsNaN(PositionY)) return _child.DesiredSize.Height;
            switch (_child.VerticalAlignment) {
                case VerticalAlignment.Top:
                    return _child.DesiredSize.Height;
                case VerticalAlignment.Bottom:
                    return _child.DesiredSize.Height;
                case VerticalAlignment.Center:
                    return _child.DesiredSize.Height;
                case VerticalAlignment.Stretch:
                    return AdornedElement.ActualHeight;
                default:
                    return 0d;
            }
        }

        protected override Size ArrangeOverride(Size finalSize) {
            var x = PositionX;
            if (double.IsNaN(x)) {
                x = DetermineX();
            }
            var y = PositionY;
            if (double.IsNaN(y)) {
                y = DetermineY();
            }
            var adornerWidth = DetermineWidth();
            var adornerHeight = DetermineHeight();
            _child.Arrange(new Rect(x, y, adornerWidth, adornerHeight));
            return finalSize;
        }

        protected override int VisualChildrenCount => 1;

        protected override Visual GetVisualChild(int index) {
            return _child;
        }

        protected override IEnumerator LogicalChildren => new ArrayList { _child }.GetEnumerator();

        /// <summary>
        /// Disconnect the child element from the visual tree so that it may be reused later.
        /// </summary>
        public void DisconnectChild() {
            RemoveLogicalChild(_child);
            RemoveVisualChild(_child);
        }

        /// <summary>
        /// Override AdornedElement from base class for less type-checking.
        /// </summary>
        public new FrameworkElement AdornedElement => (FrameworkElement)base.AdornedElement;
    }

    public class AdornedControl : ContentControl {
        #region Dependency Properties
        public static readonly DependencyProperty IsAdornerVisibleProperty = DependencyProperty.Register(nameof(IsAdornerVisible), typeof(bool),
                typeof(AdornedControl), new FrameworkPropertyMetadata(IsAdornerVisible_PropertyChanged));

        public static readonly DependencyProperty AdornerContentProperty = DependencyProperty.Register(nameof(AdornerContent), typeof(FrameworkElement),
                typeof(AdornedControl), new FrameworkPropertyMetadata(AdornerContent_PropertyChanged));

        public static readonly DependencyProperty HorizontalAdornerPlacementProperty = DependencyProperty.Register(nameof(HorizontalAdornerPlacement),
                typeof(AdornerPlacement), typeof(AdornedControl), new FrameworkPropertyMetadata(AdornerPlacement.Inside));

        public static readonly DependencyProperty VerticalAdornerPlacementProperty = DependencyProperty.Register(nameof(VerticalAdornerPlacement),
                typeof(AdornerPlacement), typeof(AdornedControl), new FrameworkPropertyMetadata(AdornerPlacement.Inside));

        public static readonly DependencyProperty AdornerOffsetXProperty = DependencyProperty.Register(nameof(AdornerOffsetX), typeof(double),
                typeof(AdornedControl));

        public static readonly DependencyProperty AdornerOffsetYProperty = DependencyProperty.Register(nameof(AdornerOffsetY), typeof(double),
                typeof(AdornedControl));

        public static readonly DependencyProperty OrderProperty = DependencyProperty.Register(nameof(Order), typeof(int),
                typeof(AdornedControl), new PropertyMetadata(OnOrderChanged));

        public static readonly DependencyProperty AvoidUsingScrollContentPresenterProperty =
                DependencyProperty.Register(nameof(AvoidUsingScrollContentPresenter), typeof(bool), typeof(AdornedControl), new FrameworkPropertyMetadata(true));
        #endregion Dependency Properties

        #region Commands
        public static readonly RoutedCommand ShowAdornerCommand = new RoutedCommand(nameof(ShowAdorner), typeof(AdornedControl));

        public static readonly RoutedCommand HideAdornerCommand = new RoutedCommand(nameof(HideAdorner), typeof(AdornedControl));
        #endregion Commands

        public AdornedControl() {
            Focusable = false; // By default don't want 'AdornedControl' to be focusable.
            DataContextChanged += OnDataContextChanged;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        /// <summary>
        /// Event raised when the DataContext of the adorned control changes.
        /// </summary>
        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e) {
            UpdateAdornerDataContext();
        }

        /// <summary>
        /// Update the DataContext of the adorner from the adorned control.
        /// </summary>
        private void UpdateAdornerDataContext() {
            if (AdornerContent != null) {
                AdornerContent.DataContext = DataContext;
            }
        }

        /// <summary>
        /// Show the adorner.
        /// </summary>
        public void ShowAdorner() {
            IsAdornerVisible = true;
        }

        /// <summary>
        /// Hide the adorner.
        /// </summary>
        public void HideAdorner() {
            IsAdornerVisible = false;
        }

        /// <summary>
        /// Shows or hides the adorner.
        /// Set to 'true' to show the adorner or 'false' to hide the adorner.
        /// </summary>
        public bool IsAdornerVisible {
            get { return (bool)GetValue(IsAdornerVisibleProperty); }
            set { SetValue(IsAdornerVisibleProperty, value); }
        }

        /// <summary>
        /// Used in XAML to define the UI content of the adorner.
        /// </summary>
        public FrameworkElement AdornerContent {
            get { return (FrameworkElement)GetValue(AdornerContentProperty); }
            set { SetValue(AdornerContentProperty, value); }
        }

        /// <summary>
        /// Specifies the horizontal placement of the adorner relative to the adorned control.
        /// </summary>
        public AdornerPlacement HorizontalAdornerPlacement {
            get { return (AdornerPlacement)GetValue(HorizontalAdornerPlacementProperty); }
            set { SetValue(HorizontalAdornerPlacementProperty, value); }
        }

        /// <summary>
        /// Specifies the vertical placement of the adorner relative to the adorned control.
        /// </summary>
        public AdornerPlacement VerticalAdornerPlacement {
            get { return (AdornerPlacement)GetValue(VerticalAdornerPlacementProperty); }
            set { SetValue(VerticalAdornerPlacementProperty, value); }
        }

        /// <summary>
        /// X offset of the adorner.
        /// </summary>
        public double AdornerOffsetX {
            get { return (double)GetValue(AdornerOffsetXProperty); }
            set { SetValue(AdornerOffsetXProperty, value); }
        }

        /// <summary>
        /// Y offset of the adorner.
        /// </summary>
        public double AdornerOffsetY {
            get { return (double)GetValue(AdornerOffsetYProperty); }
            set { SetValue(AdornerOffsetYProperty, value); }
        }

        /// <summary>
        /// Skip ScrollContentPresenter and connect only to AdornerDecorator.
        /// </summary>
        public bool AvoidUsingScrollContentPresenter {
            get { return (bool)GetValue(AvoidUsingScrollContentPresenterProperty); }
            set { SetValue(AvoidUsingScrollContentPresenterProperty, value); }
        }

        /// <summary>
        /// Adorner’s order.
        /// </summary>
        public int Order {
            get { return (int)GetValue(OrderProperty); }
            set { SetValue(OrderProperty, value); }
        }

        #region Private Data Members
        private static readonly CommandBinding ShowAdornerCommandBinding = new CommandBinding(ShowAdornerCommand, ShowAdornerCommand_Executed);

        private static readonly CommandBinding HideAdornerCommandBinding = new CommandBinding(HideAdornerCommand, HideAdornerCommand_Executed);

        /// <summary>
        /// Caches the adorner layer.
        /// </summary>
        private AdornerLayer _adornerLayer;

        /// <summary>
        /// The actual adorner create to contain our 'adorner UI content'.
        /// </summary>
        private FrameworkElementAdorner _adorner;
        #endregion

        #region Private/Internal Functions
        /// <summary>
        /// Static constructor to register command bindings.
        /// </summary>
        static AdornedControl() {
            CommandManager.RegisterClassCommandBinding(typeof(AdornedControl), ShowAdornerCommandBinding);
            CommandManager.RegisterClassCommandBinding(typeof(AdornedControl), HideAdornerCommandBinding);
        }

        /// <summary>
        /// Event raised when the Show command is executed.
        /// </summary>
        private static void ShowAdornerCommand_Executed(object target, ExecutedRoutedEventArgs e) {
            ((AdornedControl)target).ShowAdorner();
        }

        /// <summary>
        /// Event raised when the Hide command is executed.
        /// </summary>
        private static void HideAdornerCommand_Executed(object target, ExecutedRoutedEventArgs e) {
            ((AdornedControl)target).HideAdorner();
        }

        /// <summary>
        /// Event raised when the value of IsAdornerVisible has changed.
        /// </summary>
        private static void IsAdornerVisible_PropertyChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((AdornedControl)o).ShowOrHideAdornerInternal();
        }

        /// <summary>
        /// Event raised when the value of AdornerContent has changed.
        /// </summary>
        private static void AdornerContent_PropertyChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((AdornedControl)o).ShowOrHideAdornerInternal();
        }

        /// <summary>
        /// Event raised when the value of Order has changed.
        /// </summary>
        private static void OnOrderChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((AdornedControl)o).UpdateOrder();
        }

        /// <summary>
        /// Internal method to show or hide the adorner based on the value of IsAdornerVisible.
        /// </summary>
        private void ShowOrHideAdornerInternal() {
            if (IsAdornerVisible) {
                _isShown = true;
                if (_isLoaded) {
                    ShowAdornerInternal();
                }
            } else {
                _isShown = false;
                if (_isLoaded) {
                    HideAdornerInternal();
                }
            }
        }

        private bool _isLoaded, _isShown;

        private void OnLoaded(object sender, RoutedEventArgs e) {
            if (_isLoaded) return;
            _isLoaded = true;
            if (_isShown) {
                ShowAdornerInternal();
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            if (!_isLoaded) return;
            _isLoaded = false;
            if (_isShown) {
                HideAdornerInternal();
            }
        }

        public static AdornerLayer GetAdornerLayer(Visual visual) {
            if (visual == null) throw new ArgumentNullException(nameof(visual));

            foreach (var parent in visual.GetParents()) {
                {
                    var decorator = parent as AdornerDecorator;
                    if (decorator != null) return decorator.AdornerLayer;
                }

                {
                    var dialog = parent as Window;
                    if (dialog != null) return dialog.FindVisualChild<AdornerDecorator>()?.AdornerLayer;
                }
            }

            return null;
        }

        /// <summary>
        /// Internal method to show the adorner.
        /// </summary>
        private void ShowAdornerInternal() {
            // Already adorned
            if (_adorner != null) return;

            if (AdornerContent != null) {
                if (_adornerLayer == null) {
                    _adornerLayer = AvoidUsingScrollContentPresenter ? (GetAdornerLayer(this) ?? AdornerLayer.GetAdornerLayer(this)) :
                            AdornerLayer.GetAdornerLayer(this);
                }

                if (_adornerLayer != null) {
                    _adorner = new FrameworkElementAdorner(AdornerContent, this, HorizontalAdornerPlacement, VerticalAdornerPlacement,
                            AdornerOffsetX, AdornerOffsetY);
                    _adornerLayer.Add(_adorner);
                    UpdateAdornerDataContext();
                }

                UpdateOrder();
            }
        }

        /// <summary>
        /// Internal method to hide the adorner.
        /// </summary>
        private void HideAdornerInternal() {
            if (_adornerLayer == null || _adorner == null) return;

            _adornerLayer.Remove(_adorner);
            _adorner.DisconnectChild();

            _adorner = null;
            _adornerLayer = null;
        }

        /// <summary>
        /// Update adorner’s order.
        /// </summary>
        private void UpdateOrder() {
            if (_adornerLayer == null || _adorner == null) return;
            try {
                _adornerLayer.GetType().GetMethod("SetAdornerZOrder", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                             .Invoke(_adornerLayer, new object[] { _adorner, Order });
            } catch {
                // ignored
            }
        }
        #endregion

        #region Order for AdornedElementPlaceholder
        public static int GetZOrder(DependencyObject obj) {
            return (int)obj.GetValue(ZOrderProperty);
        }

        public static void SetZOrder(DependencyObject obj, int value) {
            obj.SetValue(ZOrderProperty, value);
        }

        public static readonly DependencyProperty ZOrderProperty = DependencyProperty.RegisterAttached("ZOrder", typeof(int),
                typeof(AdornedControl), new UIPropertyMetadata(OnZOrderChanged));

        private static void OnZOrderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var element = d as AdornedElementPlaceholder;
            if (element == null || !(e.NewValue is int)) return;
            if (element.IsLoaded) {
                UpdateAdornedElementPlaceholderZOrder(element);
            } else {
                element.Loaded += OnZOrderElementLoaded;
            }
        }

        private static async void OnZOrderElementLoaded(object sender, RoutedEventArgs routedEventArgs) {
            await Task.Delay(100);
            var element = (AdornedElementPlaceholder)sender;
            UpdateAdornedElementPlaceholderZOrder(element);
        }

        private static void UpdateAdornedElementPlaceholderZOrder(AdornedElementPlaceholder p) {
            var adorner = p.GetType().GetProperty("TemplatedAdorner",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(p) as Adorner;
            var adornerLayer = AdornerLayer.GetAdornerLayer(p.AdornedElement);
            if (adornerLayer == null || adorner == null) return;

            try {
                adornerLayer.GetType().GetMethod("SetAdornerZOrder", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                             .Invoke(adornerLayer, new object[] { adorner, GetZOrder(p) });
            } catch (Exception) {
                // ignored
            }
        }
        #endregion
    }
}