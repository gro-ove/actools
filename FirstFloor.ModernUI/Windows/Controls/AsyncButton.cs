using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
        private bool _commandCancellable, _commandPercentageProgress, _commandMessageProgress;

        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e) {
            base.OnPropertyChanged(e);
            if (e.Property.Name == nameof(Command)) {
                _commandInvoke = AnalyzeCommand(Command as IAsyncCommand,
                        out _commandCancellable, out _commandPercentageProgress, out _commandMessageProgress);
                SetValue(CancellablePropertyKey, _commandCancellable || CancelCommand != null);
                SetValue(PercentageProgressPropertyKey, _commandPercentageProgress || ProgressPercentage);
                SetValue(MessageProgressPropertyKey, _commandMessageProgress || ProgressMessage);
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
            CancelCommand?.Execute(null);
        }

        private CancellationTokenSource _cancellation;
        private TaskbarHolder _taskbar;
        private readonly Busy _busy = new Busy();

        protected override void OnClick() {
            if (Command is IAsyncCommand asyncCommand) {
                _busy.Task(async () => {
                    try {
                        SetValue(IsProcessingPropertyKey, true);
                        using (_cancellation = new CancellationTokenSource())
                        using (_taskbar = TaskbarService.Create("Async operation", 1200)) {
                            await _commandInvoke.Invoke(asyncCommand, this, _cancellation.Token, CommandParameter);
                            if (_commandPercentageProgress) {
                                // Report(new AsyncProgressEntry(Progress.Message, 1d));
                                Report(AsyncProgressEntry.Finished);
                            }
                        }
                    } catch (Exception e) when (e.IsCanceled()) { } catch (Exception e) {
                        NonfatalError.Notify("Unhandled error", e);
                    } finally {
                        _cancellation = null;
                        SetValue(IsProcessingPropertyKey, false);
                    }
                }).Ignore();
            } else {
                base.OnClick();
            }
        }

        public static readonly DependencyProperty CancelCommandProperty = DependencyProperty.Register(nameof(CancelCommand), typeof(ICommand),
                typeof(AsyncButton), new PropertyMetadata(null, (o, e) => {
                    var b = (AsyncButton)o;
                    b._cancelCommand = (ICommand)e.NewValue;
                    (b.CancelCommand as CommandBase)?.SubscribeWeak(b.OnCancelCommandPropertyChanged);
                    b.SetValue(CancellablePropertyKey, b._commandCancellable || b.CancelCommand != null);
                }));

        private void OnCancelCommandPropertyChanged(object sender, PropertyChangedEventArgs args) {
            if (args.PropertyName == nameof(CommandBase.IsAbleToExecute) && !_busy.Is) {
                SetValue(IsProcessingPropertyKey, (CancelCommand as CommandBase)?.IsAbleToExecute);
            }
        }

        private ICommand _cancelCommand;

        public ICommand CancelCommand {
            get => _cancelCommand;
            set => SetValue(CancelCommandProperty, value);
        }

        public static readonly DependencyProperty ProgressPercentageProperty = DependencyProperty.Register(nameof(ProgressPercentage), typeof(bool),
                typeof(AsyncButton), new PropertyMetadata(false, (o, e) => {
                    var b = (AsyncButton)o;
                    b._progressPercentage = (bool)e.NewValue;
                    b.SetValue(PercentageProgressPropertyKey, b._commandPercentageProgress || b.ProgressPercentage);
                }));

        private bool _progressPercentage;

        public bool ProgressPercentage {
            get => _progressPercentage;
            set => SetValue(ProgressPercentageProperty, value);
        }

        public static readonly DependencyProperty ProgressMessageProperty = DependencyProperty.Register(nameof(ProgressMessage), typeof(bool),
                typeof(AsyncButton), new PropertyMetadata(false, (o, e) => {
                    var b = (AsyncButton)o;
                    b._progressMessage = (bool)e.NewValue;
                    b.SetValue(MessageProgressPropertyKey, b._commandMessageProgress || b.ProgressMessage);
                }));

        private bool _progressMessage;

        public bool ProgressMessage {
            get => _progressMessage;
            set => SetValue(ProgressMessageProperty, value);
        }

        public static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(nameof(Progress), typeof(AsyncProgressEntry),
                typeof(AsyncButton));

        public AsyncProgressEntry Progress {
            get => (AsyncProgressEntry)GetValue(ProgressProperty);
            set => SetValue(ProgressProperty, value);
        }

        public void Report(AsyncProgressEntry value) {
            _taskbar?.Set(value.IsIndeterminate ? TaskbarState.Indeterminate : TaskbarState.Normal, value.Progress ?? 0.5);
            ActionExtension.InvokeInMainThread(() => Progress = value);
        }

        #region Support for various types of commands
        [NotNull]
        private delegate Task InvokeExecuteDelegate([NotNull] IAsyncCommand command,
                [CanBeNull] IProgress<AsyncProgressEntry> progress, CancellationToken cancellation, object param);

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
                    return (c, p, t, o) => c.ExecuteAsync(t);
                }

                if (!paramType.IsGenericType) {
                    Logging.Debug("Non-generic argument: " + paramType);
                    goto Nothing;
                }

                var genericTypeDefinitions = paramType.GetGenericTypeDefinition();
                if (genericTypeDefinitions == typeof(IProgress<>)) {
                    cancellable = false;

                    var progressParamType = paramType.GenericTypeArguments[0];
                    if (progressParamType == typeof(AsyncProgressEntry)) {
                        messageProgress = percentageProgress = true;
                        return (c, p, t, o) => c.ExecuteAsync(p);
                    }

                    percentageProgress = !(messageProgress = progressParamType == typeof(string));
                    return (c, p, t, o) => c.ExecuteAsync(GetProgress(progressParamType, p));
                }

                if (genericTypeDefinitions == typeof(Tuple<,>)) {
                    var progressType = paramType.GenericTypeArguments[0];
                    if (!progressType.IsGenericType) {
                        Logging.Debug("Non-generic progress argument: " + paramType);
                        goto Nothing;
                    }

                    if (progressType.GetGenericTypeDefinition() == typeof(IProgress<>) && IsCancellation(paramType.GenericTypeArguments[1])) {
                        cancellable = true;

                        var progressParamType = progressType.GenericTypeArguments[0];
                        if (progressParamType == typeof(AsyncProgressEntry)) {
                            messageProgress = percentageProgress = true;
                            return (c, p, t, o) => c.ExecuteAsync(Activator.CreateInstance(paramType, p, t));
                        }

                        percentageProgress = !(messageProgress = progressParamType == typeof(string));
                        return (c, p, t, o) => c.ExecuteAsync(Activator.CreateInstance(paramType, GetProgress(progressParamType, p), t));
                    }
                }
            } catch (Exception e) {
                Logging.Error(e);
                Logging.Error(command?.GetType());
            }

            Nothing:
            cancellable = percentageProgress = messageProgress = false;
            return (c, p, t, o) => c.ExecuteAsync(o);

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