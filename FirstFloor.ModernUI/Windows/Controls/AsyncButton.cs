using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Windows.Controls {
    public class AsyncButton : Button, IProgress<AsyncProgressEntry> {
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

        public static readonly DependencyPropertyKey PercentageProgressPropertyKey = DependencyProperty.RegisterReadOnly(nameof(PercentageProgress), typeof(bool),
                typeof(AsyncButton), new PropertyMetadata(false));

        public static readonly DependencyProperty PercentageProgressProperty = PercentageProgressPropertyKey.DependencyProperty;

        public bool PercentageProgress => GetValue(PercentageProgressProperty) as bool? == true;

        public static readonly DependencyPropertyKey MessageProgressPropertyKey = DependencyProperty.RegisterReadOnly(nameof(MessageProgress), typeof(bool),
                typeof(AsyncButton), new PropertyMetadata(false));

        public static readonly DependencyProperty MessageProgressProperty = MessageProgressPropertyKey.DependencyProperty;

        public bool MessageProgress => GetValue(MessageProgressProperty) as bool? == true;

        private InvokeExecuteDelegate _commandInvoke;

        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e) {
            base.OnPropertyChanged(e);
            if (e.Property.Name == nameof(Command)) {
                _commandInvoke = AnalyzeCommand(Command as IAsyncCommand, out var cancellable, out var percentageProgress, out var messageProgress);
                SetValue(CancellablePropertyKey, cancellable);
                SetValue(PercentageProgressPropertyKey, percentageProgress);
                SetValue(MessageProgressPropertyKey, messageProgress);
            }
        }

        private Button _cancelButton;

        public override void OnApplyTemplate() {
            if (_cancelButton != null) {
                _cancelButton.Click -= OnCancelClick;
            }

            base.OnApplyTemplate();
            _cancelButton = GetTemplateChild(@"PART_CancelButton") as Button;
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
            if (Command is IAsyncCommand asyncCommand) {
                _busy.Task(async () => {
                    try {
                        SetValue(IsProcessingPropertyKey, true);
                        using (_cancellation = new CancellationTokenSource()) {
                            await _commandInvoke.Invoke(asyncCommand, this, _cancellation.Token);
                            if (PercentageProgress) {
                                Report(new AsyncProgressEntry(Progress.Message, 1d));
                            }
                        }
                    } catch (Exception e) when (e.IsCanceled()) { } catch (Exception e) {
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

        public static readonly DependencyPropertyKey ProgressPropertyKey = DependencyProperty.RegisterReadOnly(nameof(Progress), typeof(AsyncProgressEntry),
                typeof(AsyncButton), new PropertyMetadata(default(AsyncProgressEntry)));

        public static readonly DependencyProperty ProgressProperty = ProgressPropertyKey.DependencyProperty;

        public AsyncProgressEntry Progress => (AsyncProgressEntry)GetValue(ProgressProperty);

        public void Report(AsyncProgressEntry value) {
            ActionExtension.InvokeInMainThread(() => SetValue(ProgressPropertyKey, value));
        }

        #region Support for various types of commands
        [NotNull]
        private delegate Task InvokeExecuteDelegate([NotNull] IAsyncCommand command,
                [CanBeNull] IProgress<AsyncProgressEntry> progress, CancellationToken cancellation);

        [NotNull]
        private static InvokeExecuteDelegate AnalyzeCommand(IAsyncCommand command, out bool cancellable, out bool percentageProgress,
                out bool messageProgress) {
            try {
                var type = command?.GetType();
                if (type == null || !type.IsGenericType || type.GetGenericTypeDefinition() != typeof(AsyncCommand<>)) {
                    goto Nothing;
                }

                var paramType = type.GenericTypeArguments[0];
                if (IsCancellation(paramType)) {
                    cancellable = true;
                    percentageProgress = messageProgress = false;
                    return (c, p, t) => c.ExecuteAsync(t);
                }

                if (paramType.GetGenericTypeDefinition() == typeof(IProgress<>)) {
                    cancellable = false;

                    var progressParamType = paramType.GenericTypeArguments[0];
                    if (progressParamType == typeof(AsyncProgressEntry)) {
                        messageProgress = percentageProgress = true;
                        return (c, p, t) => c.ExecuteAsync(p);
                    }

                    percentageProgress = !(messageProgress = progressParamType == typeof(string));
                    return (c, p, t) => c.ExecuteAsync(GetProgress(progressParamType, p));
                }

                if (paramType.GetGenericTypeDefinition() == typeof(Tuple<,>)) {
                    var progressType = paramType.GenericTypeArguments[0];
                    if (progressType.GetGenericTypeDefinition() == typeof(IProgress<>) && IsCancellation(paramType.GenericTypeArguments[1])) {
                        cancellable = true;

                        var progressParamType = progressType.GenericTypeArguments[0];
                        if (progressParamType == typeof(AsyncProgressEntry)) {
                            messageProgress = percentageProgress = true;
                            return (c, p, t) => c.ExecuteAsync(Activator.CreateInstance(paramType, p, t));
                        }

                        percentageProgress = !(messageProgress = progressParamType == typeof(string));
                        return (c, p, t) => c.ExecuteAsync(Activator.CreateInstance(paramType, GetProgress(progressParamType, p), t));
                    }
                }
            } catch (Exception e) {
                Logging.Error(e);
                Logging.Error(command?.GetType());
            }

            Nothing:
            cancellable = percentageProgress = messageProgress = false;
            return (c, p, t) => c.ExecuteAsync(null);

            object GetProgress(Type t, IProgress<AsyncProgressEntry> p) {
                if (t == typeof(AsyncProgressEntry)) return p;
                if (t == typeof(string)) return new Progress<string>(v => p.Report(v, null));
                if (t == typeof(double)) return new Progress<double>(v => p.Report(null, v));
                if (t == typeof(double?)) return new Progress<double?>(v => p.Report(null, v));
                return null;
            }

            bool IsCancellation(Type possiblyNullableType) {
                return possiblyNullableType == typeof(CancellationToken)
                        || Nullable.GetUnderlyingType(possiblyNullableType) == typeof(CancellationToken);
            }
        }
        #endregion
    }
}