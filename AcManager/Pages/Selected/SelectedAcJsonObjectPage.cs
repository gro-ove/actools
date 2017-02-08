using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AcManager.Controls.UserControls;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Helpers;
using AcManager.Tools.Objects;

namespace AcManager.Pages.Selected {
    public abstract class SelectedAcJsonObjectPage : SelectedAcObjectPage {
        public AcJsonObjectNew SelectedAcJsonObject => (AcJsonObjectNew)((ISelectedAcObjectViewModel)DataContext).SelectedAcObject;

        protected void InitializeAcObjectPage<T>(SelectedAcObjectViewModel<T> model) where T : AcJsonObjectNew {
            base.InitializeAcObjectPage(model);
            InputBindings.AddRange(new[] {
                new InputBinding(SelectedAcJsonObject.TagsCleanUpAndSortCommand, new KeyGesture(Key.T, ModifierKeys.Control | ModifierKeys.Alt))
            });
        }

        protected virtual void OnVersionInfoBlockClick(object sender, MouseButtonEventArgs e) {
            if (SelectedAcJsonObject.Author == AcCommonObject.AuthorKunos) return;

            if (e.ChangedButton == MouseButton.Left && e.ClickCount == 1) {
                e.Handled = true;

                if (Keyboard.Modifiers != ModifierKeys.Control) {
                    new ModernPopup {
                        Content = new PopupAuthor((ISelectedAcObjectViewModel)DataContext),
                        PlacementTarget = sender as UIElement,
                        StaysOpen = false
                    }.IsOpen = true;
                } else if (SelectedAcJsonObject.Url != null) {
                    WindowsHelper.ViewInBrowser(SelectedAcJsonObject.Url);
                }
            }
        }

        protected void OnTagsListMouseDown(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton != MouseButton.Right) return;

            new ContextMenu {
                Items = {
                    new MenuItem {
                        Header = AppStrings.Tags_CleanUp,
                        Command = SelectedAcJsonObject.TagsCleanUpCommand
                    },
                    new MenuItem {
                        Header = AppStrings.Tags_Sort,
                        Command = SelectedAcJsonObject.TagsSortCommand
                    },
                    new MenuItem {
                        Header = AppStrings.Tags_CleanUpAndSort,
                        Command = SelectedAcJsonObject.TagsCleanUpAndSortCommand,
                        InputGestureText = @"Ctrl+Alt+T"
                    }
                }
            }.IsOpen = true;
            e.Handled = true;
        }
    }
}