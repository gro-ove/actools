using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Windows.Converters;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Dialogs {
    public static class ErrorMessage {
        public static void Show(NonfatalErrorEntry entry) {
            var text = (entry.Exception == null
                    ? $"{entry.DisplayName.TrimEnd('.')}."
                    : $"{entry.DisplayName.TrimEnd('.')}{ColonConverter.Colon}\n\n[b][mono]{entry.Exception.Message}[/mono][/b]") +
                    (entry.Commentary == null ? "" : $"\n\n[i]{entry.Commentary}[/i]");
            var dlg = new ModernDialog {
                Title = UiStrings.Common_Oops,
                Content = new ScrollViewer {
                    Content = new SelectableBbCodeBlock { Text = text, Margin = new Thickness(0, 0, 0, 8) },
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
                },
                MinHeight = 0,
                MinWidth = 0,
                MaxHeight = 480,
                MaxWidth = 640,
                Owner = null
            };

            var fixButtons = entry.Solutions.Select(x => dlg.CreateFixItButton(x, entry)).Where(x => x != null).ToList();
            if (fixButtons.Count > 0) {
                fixButtons[0].IsDefault = true;

                var closeButton = dlg.CloseButton;
                closeButton.IsDefault = false;
                dlg.Buttons = fixButtons.Concat(new[] { dlg.CloseButton });
            } else {
                dlg.Buttons = new[] { dlg.OkButton };
            }
            dlg.Show();

            entry.Unseen = false;
        }

        [CanBeNull]
        public static Button CreateFixItButton([NotNull] this ModernDialog dlg, [CanBeNull] NonfatalErrorSolution solution, NonfatalErrorEntry entry = null) {
            if (dlg == null) throw new ArgumentNullException(nameof(dlg));
            if (solution == null) return null;

            return new Button {
                Content = GetFixButtonContent(solution),
                Command = solution,
                CommandParameter = null,
                IsDefault = false,
                IsCancel = false,
                MinHeight = 21,
                MinWidth = 65,
                Margin = new Thickness(4, 0, 0, 0)
            };
        }

        private static object GetFixButtonContent(NonfatalErrorSolution solution) {
            if (solution.IconData == null) {
                return solution.DisplayName;
            }

            var icon = new Path {
                Data = solution.IconData,
                Margin = new Thickness(0, 0, 6, -1),
                VerticalAlignment = VerticalAlignment.Center,
                Stretch = Stretch.Uniform,
                Width = 12,
                Height = 12
            };

            icon.SetResourceReference(Shape.FillProperty, @"Accent");
            return new DockPanel { Children = { icon, new TextBlock { Text = solution.DisplayName } } };
        }
    }
}