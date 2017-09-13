using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;

namespace FirstFloor.ModernUI.Windows.Controls {
    public class AsyncButton : Button {
        public AsyncButton() {
            DefaultStyleKey = typeof(AsyncButton);
        }

        public static readonly DependencyPropertyKey IsProcessingPropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsProcessing), typeof(bool),
                typeof(AsyncButton), new PropertyMetadata(false));

        public static readonly DependencyProperty IsProcessingProperty = IsProcessingPropertyKey.DependencyProperty;

        public bool IsProcessing => GetValue(IsProcessingProperty) as bool? == true;

        public static readonly DependencyPropertyKey CancellablePropertyKey = DependencyProperty.RegisterReadOnly(nameof(Cancellable), typeof(bool),
                typeof(AsyncButton), new PropertyMetadata(false));

        public static readonly DependencyProperty CancellableProperty = CancellablePropertyKey.DependencyProperty;

        public bool Cancellable => GetValue(CancellableProperty) as bool? == true;

        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e) {
            base.OnPropertyChanged(e);
            if (e.Property.Name == nameof(Command)) {
                SetValue(CancellablePropertyKey,
                        Command is AsyncCommand<CancellationToken> || Command is AsyncCommand<CancellationToken?>);
            }
        }

        private Button _cancelButton;

        public override void OnApplyTemplate() {
            if (_cancelButton != null) {
                _cancelButton.Click -= OnCancelClick;
            }

            base.OnApplyTemplate();
            _cancelButton = GetTemplateChild("PART_CancelButton") as Button;
            if (_cancelButton != null) {
                _cancelButton.Click += OnCancelClick;
            }
        }

        private void OnCancelClick(object sender, RoutedEventArgs routedEventArgs) {
            _cancellation?.Cancel();
        }

        private CancellationTokenSource _cancellation;
        private readonly Busy _busy = new Busy();

        protected override void OnClick() {
            var asyncCommand = Command as IAsyncCommand;
            if (asyncCommand != null) {
                _busy.Task(async () => {
                    try {
                        SetValue(IsProcessingPropertyKey, true);

                        switch (asyncCommand) {
                            case AsyncCommand<CancellationToken> cancellable:
                                using (_cancellation = new CancellationTokenSource()) {
                                    await cancellable.ExecuteAsync(_cancellation.Token);
                                }
                                break;
                            case AsyncCommand<CancellationToken?> cancellable:
                                using (_cancellation = new CancellationTokenSource()) {
                                    await cancellable.ExecuteAsync(_cancellation.Token);
                                }
                                break;
                            default:
                                await asyncCommand.ExecuteAsync(CommandParameter);
                                break;
                        }
                    } catch (Exception e) when (e.IsCanceled()) {
                    } catch (Exception e) when (e.IsCanceled()) {
                    } catch (Exception e){
                        NonfatalError.Notify("Unhandled error", e);
                    } finally {
                        _cancellation = null;
                        SetValue(IsProcessingPropertyKey, false);
                    }
                }).Forget();
            } else {
                base.OnClick();
            }
        }
    }

    [ContentProperty(@"MenuItems")]
    public class ButtonWithComboBox : ContentControl {
        public ButtonWithComboBox() {
            DefaultStyleKey = typeof(ButtonWithComboBox);
            MenuItems = new Collection<DependencyObject>();
            PreviewMouseRightButtonUp += OnRightClick;
        }

        private void OnRightClick(object sender, MouseButtonEventArgs args) {
            if (_item != null) {
                args.Handled = true;
                _item.IsSubmenuOpen = true;
            }
        }

        private MenuItem _item;

        public override void OnApplyTemplate() {
            if (_item != null) {
                _item.KeyDown -= OnMenuKeyDown;
            }

            base.OnApplyTemplate();

            _item = GetTemplateChild("PART_MenuItem") as MenuItem;
            if (_item != null) {
                _item.KeyDown += OnMenuKeyDown;
            }
        }

        private void OnMenuKeyDown(object sender, KeyEventArgs args) {
            if (_item == null) return;
            if (_item.IsSubmenuOpen != true && (args.Key == Key.Enter || args.Key == Key.Space)) {
                _item.IsSubmenuOpen = true;
            }
        }

        public IList MenuItems {
            get => (Collection<DependencyObject>)GetValue(MenuItemsProperty);
            set => SetValue(MenuItemsProperty, value);
        }

        public static readonly DependencyProperty MenuItemsProperty = DependencyProperty.Register("MenuItems", typeof(IList),
                typeof(ButtonWithComboBox));

        public object ButtonToolTip {
            get => GetValue(ButtonToolTipProperty);
            set => SetValue(ButtonToolTipProperty, value);
        }

        public static readonly DependencyProperty ButtonToolTipProperty = DependencyProperty.Register("ButtonToolTip", typeof(object),
                typeof(ButtonWithComboBox));

        public static readonly DependencyProperty CommandProperty = DependencyProperty.Register(nameof(Command), typeof(ICommand),
                typeof(ButtonWithComboBox));

        public ICommand Command {
            get => (ICommand)GetValue(CommandProperty);
            set => SetValue(CommandProperty, value);
        }

        public static readonly DependencyProperty CommandParameterProperty = DependencyProperty.Register(nameof(CommandParameter), typeof(object),
                typeof(ButtonWithComboBox));

        public object CommandParameter {
            get => (object)GetValue(CommandParameterProperty);
            set => SetValue(CommandParameterProperty, value);
        }
    }
}
