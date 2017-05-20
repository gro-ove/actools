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
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Windows.Media;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Windows.Attached {
    internal class FancyHintControl : Control {
        static FancyHintControl() {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(FancyHintControl), new FrameworkPropertyMetadata(typeof(FancyHintControl)));
        }

        public static readonly DependencyProperty HintProperty = DependencyProperty.Register(nameof(Hint), typeof(FancyHint),
                typeof(FancyHintControl));

        public FancyHint Hint {
            get { return (FancyHint)GetValue(HintProperty); }
            set { SetValue(HintProperty, value); }
        }
    }
    
    internal class FancyHintAdorner : Adorner {
        private readonly AdornerLayer _layer;
        private readonly FrameworkElement _contentPresenter;

        public FancyHintAdorner(UIElement adornedElement, AdornerLayer layer, FancyHint hint) : base(adornedElement) {
            _layer = layer;
            IsHitTestVisible = true;

            var style = FindResource(@"HintMarkStyle") as Style;
            
            var offsetX = FancyHintsService.GetOffsetX(adornedElement);
            var offsetY = FancyHintsService.GetOffsetY(adornedElement);
            
            _contentPresenter = new FancyHintControl {
                Style = style,
                Hint = hint,
                HorizontalContentAlignment = FancyHintsService.GetHorizontalAlignment(adornedElement),
                VerticalContentAlignment = FancyHintsService.GetVerticalAlignment(adornedElement),
                Margin = new Thickness(-4000 + offsetX, -4000 + offsetY, -4000 - offsetX, -4000 - offsetY)
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

        private Window _window;

        private async void Show() {
            await Task.Delay(1);
            var cell = GetByName("PART_Cell");
            if (cell != null) {
                VisibilityAnimation.SetDuration(cell, TimeSpan.FromSeconds(0.3));
                VisibilityAnimation.SetVisible(cell, true);
                
                _window = Window.GetWindow(_contentPresenter);
                if (_window != null) {
                    _window.PreviewMouseDown += OnWindowMouseDown;
                }

                cell.PreviewMouseDown += OnMouseDown;

                var disableButton = GetByName("PART_DisableHintsButton") as Button;
                if (disableButton != null) {
                    disableButton.Command = new DelegateCommand(() => {
                        FancyHintsService.Instance.Enabled = false;
                        Close();
                    });
                }
            }
        }

        private void OnWindowMouseDown(object sender, MouseButtonEventArgs mouseButtonEventArgs) {
            Close();
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs mouseButtonEventArgs) {
            Close();
        }

        private async void Close() {
            IsHitTestVisible = false;
            if (_window != null) {
                _window.PreviewMouseDown -= OnWindowMouseDown;
            }
            
            var cell = GetByName("PART_Cell");
            if (cell != null) {
                VisibilityAnimation.SetVisible(cell, false);
                await Task.Delay(300);
            }
            
            _layer.Remove(this);
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