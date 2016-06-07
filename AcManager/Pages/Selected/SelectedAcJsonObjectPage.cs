using System.Windows.Controls;
using System.Windows.Input;
using AcManager.Pages.Dialogs;
using AcManager.Tools.AcObjectsNew;

namespace AcManager.Pages.Selected {
    public abstract class SelectedAcJsonObjectPage : SelectedAcObjectPage {
        public AcJsonObjectNew SelectedAcJsonObject => (AcJsonObjectNew)((ISelectedAcObjectViewModel)DataContext).SelectedAcObject;

        protected void InitializeAcObjectPage<T>(SelectedAcObjectViewModel<T> model) where T : AcJsonObjectNew {
            base.InitializeAcObjectPage(model);
            InputBindings.AddRange(new[] {
                new InputBinding(SelectedAcJsonObject.TagsCleanUpAndSortCommand, new KeyGesture(Key.T, ModifierKeys.Control | ModifierKeys.Alt))
            });
        }

        protected virtual void VersionInfoBlock_OnMouseDown(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton == MouseButton.Left && e.ClickCount == 1) {
                e.Handled = true;
                new VersionInfoEditor(SelectedAcJsonObject).ShowDialog();
            }
        }

        protected void TagsList_OnMouseDown(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton != MouseButton.Right) return;

            new ContextMenu {
                Items = {
                    new MenuItem { Header = "Clean Up Tags", Command = SelectedAcJsonObject.TagsCleanUpCommand },
                    new MenuItem { Header = "Sort Tags", Command = SelectedAcJsonObject.TagsSortCommand },
                    new MenuItem { Header = "Clean Up & Sort Tags", Command = SelectedAcJsonObject.TagsCleanUpAndSortCommand, InputGestureText = "Ctrl+Alt+T" }
                }
            }.IsOpen = true;
            e.Handled = true;
        }
    }
}