using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
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
}