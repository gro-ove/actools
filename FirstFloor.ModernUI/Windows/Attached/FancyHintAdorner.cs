using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Windows.Media;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Windows.Attached {
    internal class FancyHintControl : Control {
        static FancyHintControl() {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(FancyHintControl), new FrameworkPropertyMetadata(typeof(FancyHintControl)));
        }

        public static readonly DependencyProperty HorizontalPositionAlignmentProperty = DependencyProperty.Register(nameof(HorizontalPositionAlignment),
                typeof(HorizontalAlignment), typeof(FancyHintControl));

        public HorizontalAlignment HorizontalPositionAlignment {
            get { return (HorizontalAlignment)GetValue(HorizontalPositionAlignmentProperty); }
            set { SetValue(HorizontalPositionAlignmentProperty, value); }
        }

        public static readonly DependencyProperty VerticalPositionAlignmentProperty = DependencyProperty.Register(nameof(VerticalPositionAlignment),
                typeof(VerticalAlignment), typeof(FancyHintControl));

        public VerticalAlignment VerticalPositionAlignment {
            get { return (VerticalAlignment)GetValue(VerticalPositionAlignmentProperty); }
            set { SetValue(VerticalPositionAlignmentProperty, value); }
        }

        public static readonly DependencyProperty HintProperty = DependencyProperty.Register(nameof(Hint), typeof(FancyHint),
                typeof(FancyHintControl));

        public FancyHint Hint {
            get => (FancyHint)GetValue(HintProperty);
            set => SetValue(HintProperty, value);
        }
    }

    internal class FancyHintAdorner : Adorner {
        public static bool IsAnyShown { get; private set; }

        private readonly AdornerLayer _layer;
        private readonly Window _window;
        private readonly FancyHint _hint;
        private readonly FrameworkElement _contentPresenter;

        public FancyHintAdorner(UIElement adornedElement, UIElement parent, AdornerLayer layer, Window window, FancyHint hint) : base(adornedElement) {
            _layer = layer;
            _window = window;
            _hint = hint;

            IsHitTestVisible = true;

            var style = FindResource(@"HintMarkStyle") as Style;

            var offsetX = FancyHintsService.GetOffsetX(parent);
            var offsetY = FancyHintsService.GetOffsetY(parent);
            var horizontalAlignment = FancyHintsService.GetHorizontalAlignment(parent);
            var verticalAlignment = FancyHintsService.GetVerticalAlignment(parent);

            _contentPresenter = new FancyHintControl {
                Style = style,
                Hint = hint,
                HorizontalPositionAlignment = horizontalAlignment,
                VerticalPositionAlignment = verticalAlignment,
                HorizontalContentAlignment = FancyHintsService.GetHorizontalContentAlignment(parent),
                VerticalContentAlignment = FancyHintsService.GetVerticalContentAlignment(parent),
                Margin = new Thickness(
                    (horizontalAlignment == HorizontalAlignment.Left ? -36 : -4000) + offsetX,
                    (verticalAlignment == VerticalAlignment.Top ? -36 : -4000) + offsetY,
                    (horizontalAlignment == HorizontalAlignment.Right ? -36 : -4000) - offsetX,
                    (verticalAlignment == VerticalAlignment.Bottom ? -36 : -4000) - offsetY)
            };

            SetBinding(VisibilityProperty, new Binding(@"IsVisible") {
                Source = adornedElement,
                Converter = new BooleanToVisibilityConverter()
            });

            AddVisualChild(_contentPresenter);
            Show();
        }

        [CanBeNull]
        private FrameworkElement GetByName(string name) {
            return _contentPresenter.FindVisualChildren<FrameworkElement>().FirstOrDefault(x => x.Name == name);
        }

        private async void Show() {
            IsAnyShown = true;
            await Task.Delay(1);
            var cell = GetByName("PART_Cell");
            if (cell != null) {
                VisibilityAnimation.SetDuration(cell, TimeSpan.FromSeconds(0.3));
                VisibilityAnimation.SetVisible(cell, true);

                _window.PreviewMouseDown += OnWindowMouseDown;
                _window.PreviewKeyDown += OnWindowKeyDown;

                if (_hint.CloseOnResize) {
                    _window.SizeChanged += OnWindowSizeChanged;
                }

                cell.PreviewMouseDown += OnMouseDown;

                var disableButton = GetByName("PART_DisableHintsButton") as Button;
                if (disableButton != null) {
                    disableButton.Command = new DelegateCommand(() => {
                        FancyHintsService.Instance.Enabled = false;
                        Close();
                    });
                }
            } else {
                IsAnyShown = false;
            }
        }

        private void OnWindowKeyDown(object sender, KeyEventArgs args) {
            switch (args.Key) {
                case Key.LeftCtrl:
                case Key.RightCtrl:
                case Key.LeftShift:
                case Key.RightShift:
                case Key.LeftAlt:
                case Key.RightAlt:
                    return;
            }

            Close();
        }

        private void OnWindowMouseDown(object sender, EventArgs args) {
            Close();
        }

        private void OnWindowSizeChanged(object sender, SizeChangedEventArgs args) {
            Close();
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs args) {
            Close();
        }

        private async void Close() {
            IsHitTestVisible = false;

            _window.PreviewMouseDown -= OnWindowMouseDown;
            _window.PreviewKeyDown -= OnWindowMouseDown;

            if (_hint.CloseOnResize) {
                _window.SizeChanged -= OnWindowSizeChanged;
            }

            var cell = GetByName("PART_Cell");
            if (cell != null) {
                VisibilityAnimation.SetVisible(cell, false);
                await Task.Delay(300);
            }

            _layer.Remove(this);
            IsAnyShown = false;
        }

        protected override int VisualChildrenCount => 1;

        protected override Visual GetVisualChild(int index) {
            return _contentPresenter;
        }

        protected override Size MeasureOverride(Size constraint) {
            _contentPresenter?.Measure(AdornedElement.RenderSize);
            return AdornedElement.RenderSize;
        }

        protected override Size ArrangeOverride(Size finalSize) {
            _contentPresenter?.Arrange(new Rect(finalSize));
            return finalSize;
        }
    }
}