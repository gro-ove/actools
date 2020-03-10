using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Dialogs {
    public class MessageDialogButton {
        public static readonly MessageDialogButton OK = new MessageDialogButton(MessageBoxButton.OK);
        public static readonly MessageDialogButton OKCancel = new MessageDialogButton(MessageBoxButton.OKCancel);
        public static readonly MessageDialogButton YesNoCancel = new MessageDialogButton(MessageBoxButton.YesNoCancel);
        public static readonly MessageDialogButton YesNo = new MessageDialogButton(MessageBoxButton.YesNo);

        private readonly MessageBoxButton? _type;

        public MessageDialogButton(MessageBoxButton type) {
            _type = type;
        }

        public MessageDialogButton() {}

        private Dictionary<MessageBoxResult, string> _customButtons;

        public string this[MessageBoxResult key] {
            get => _customButtons.TryGetValue(key, out var v) ? v : null;
            set {
                if (_customButtons == null) {
                    _customButtons = new Dictionary<MessageBoxResult, string>();
                }
                _customButtons[key] = value;
            }
        }

        public static implicit operator MessageDialogButton(MessageBoxButton a) {
            return new MessageDialogButton(a);
        }

        public IEnumerable<Control> GetButtons([NotNull] ModernDialog dialog) {
            var isFirst = true;
            if (_customButtons != null) {
                foreach (var button in _customButtons) {
                    yield return dialog.CreateCloseDialogButton(button.Value, isFirst, button.Key == MessageBoxResult.Cancel, button.Key);
                    isFirst = false;
                }
            }

            switch (_type) {
                case MessageBoxButton.OK:
                    if (_customButtons?.ContainsKey(MessageBoxResult.OK) != true) {
                        yield return dialog.OkButton;
                    }
                    break;
                case MessageBoxButton.OKCancel:
                    if (_customButtons?.ContainsKey(MessageBoxResult.OK) != true) {
                        yield return dialog.OkButton;
                    }
                    if (_customButtons?.ContainsKey(MessageBoxResult.Cancel) != true) {
                        yield return dialog.CancelButton;
                    }
                    break;
                case MessageBoxButton.YesNo:
                    if (_customButtons?.ContainsKey(MessageBoxResult.Yes) != true) {
                        yield return dialog.YesButton;
                    }
                    if (_customButtons?.ContainsKey(MessageBoxResult.No) != true) {
                        yield return dialog.NoButton;
                    }
                    break;
                case MessageBoxButton.YesNoCancel:
                    if (_customButtons?.ContainsKey(MessageBoxResult.Yes) != true) {
                        yield return dialog.YesButton;
                    }
                    if (_customButtons?.ContainsKey(MessageBoxResult.No) != true) {
                        yield return dialog.NoButton;
                    }
                    if (_customButtons?.ContainsKey(MessageBoxResult.Cancel) != true) {
                        yield return dialog.CancelButton;
                    }
                    break;
                case null:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    public static class MessageDialog {
        private static MessageBoxResult ShowMessageInner(string text, string title, MessageDialogButton button,
                ShowMessageCallbacks doNotAskAgainLoadSave, Window owner = null) {
            var value = doNotAskAgainLoadSave?.Item1?.Invoke();
            if (value != null) return value.Value;

            FrameworkElement content = new SelectableBbCodeBlock {
                Text = text,
                Margin = new Thickness(0, 0, 0, 8)
            };

            CheckBox doNotAskAgainCheckbox;
            if (doNotAskAgainLoadSave != null) {
                doNotAskAgainCheckbox = new CheckBox {
                    Content = new Label { Content = "Don’t ask again" }
                };

                content = new SpacingStackPanel {
                    Spacing = 8,
                    Children = {
                        content,
                        doNotAskAgainCheckbox
                    }
                };
            } else {
                doNotAskAgainCheckbox = null;
            }

            var dlg = new ModernDialog {
                Title = title,
                Content = new ScrollViewer {
                    Content = content,
                    MaxWidth = 640,
                    MaxHeight = 520,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
                },
                MinHeight = 0,
                MinWidth = 0,
                MaxHeight = 640,
                MaxWidth = 800
            };

            if (owner != null) {
                dlg.Owner = owner;
            }

            dlg.Buttons = button.GetButtons(dlg);
            dlg.ShowDialog();

            if (doNotAskAgainCheckbox != null) {
                doNotAskAgainLoadSave.Item2.Invoke(doNotAskAgainCheckbox.IsChecked == true ?
                        dlg.MessageBoxResult : (MessageBoxResult?)null);
            }

            return dlg.MessageBoxResult;
        }

        public class ShowMessageCallbacks : Tuple<Func<MessageBoxResult?>, Action<MessageBoxResult?>> {
            public ShowMessageCallbacks(Func<MessageBoxResult?> load, Action<MessageBoxResult?> save) : base(load, save) { }
        }

        public static MessageBoxResult Show(string text, string title, MessageDialogButton button,
                ShowMessageCallbacks doNotAskAgainLoadSave, Window owner = null) {
            return ShowMessageInner(text, title, button, doNotAskAgainLoadSave, owner);
        }

        public static MessageBoxResult Show(string text, string title, MessageDialogButton button, Window owner = null) {
            return ShowMessageInner(text, title, button, null, owner);
        }

        public static MessageBoxResult Show(string text, string title, MessageDialogButton button, [NotNull] string doNotAskAgainKey, Window owner = null) {
            var key = $@"__doNotAskAgain:{doNotAskAgainKey}";
            return Show(text, title, button,
                    new ShowMessageCallbacks(() => ValuesStorage.Get<MessageBoxResult?>(key), k => {
                        if (!k.HasValue || k == MessageBoxResult.Cancel || k == MessageBoxResult.No) {
                            ValuesStorage.Remove(key);
                        } else {
                            ValuesStorage.Set(key, k.Value);
                        }
                    }), owner);
        }

        public static MessageBoxResult Show(string text) {
            return Show(text, "", MessageDialogButton.OK);
        }
    }
}