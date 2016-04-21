using System.Windows.Controls;
using System.Windows.Input;
using AcManager.Annotations;
using AcManager.Pages.Dialogs;
using AcManager.Tools.AcObjectsNew;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Pages.Selected {
    public interface ISelectedAcObjectViewModel {
        AcCommonObject SelectedAcObject { get; }

        void Load();

        void Unload();
    }

    public class SelectedAcObjectViewModel<T> : NotifyPropertyChanged, ISelectedAcObjectViewModel where T : AcCommonObject {
        [NotNull]
        public T SelectedObject { get; }

        public AcCommonObject SelectedAcObject => SelectedObject;

        protected SelectedAcObjectViewModel([NotNull] T acObject) {
            SelectedObject = acObject;
        }

        public virtual void Load() { }

        public virtual void Unload() { }
    }

    public abstract class SelectedAcObjectPage : UserControl {
        public AcCommonObject SelectedAcObject { get; private set; }

        protected void InitializeAcObjectPage([NotNull] ISelectedAcObjectViewModel model) {
            SelectedAcObject = model.SelectedAcObject;
            InputBindings.AddRange(new[] {
                new InputBinding(SelectedAcObject.ViewInExplorerCommand, new KeyGesture(Key.F, ModifierKeys.Control)),
                new InputBinding(SelectedAcObject.ReloadCommand, new KeyGesture(Key.R, ModifierKeys.Control)),
                new InputBinding(SelectedAcObject.ToggleCommand, new KeyGesture(Key.D, ModifierKeys.Control)),
                new InputBinding(SelectedAcObject.SaveCommand, new KeyGesture(Key.S, ModifierKeys.Control)),
                new InputBinding(SelectedAcObject.DeleteCommand, new KeyGesture(Key.Delete, ModifierKeys.Control))
            });
            DataContext = model;

            Loaded += SelectedAcObjectPage_Loaded;
            Unloaded += SelectedAcObjectPage_Unloaded;
        }

        private bool _loaded;

        private void SelectedAcObjectPage_Loaded(object sender, System.Windows.RoutedEventArgs e) {
            if (_loaded) return;
            _loaded = true;

            ((ISelectedAcObjectViewModel)DataContext).Load();
        }

        private void SelectedAcObjectPage_Unloaded(object sender, System.Windows.RoutedEventArgs e) {
            if (!_loaded) return;
            _loaded = false;

            ((ISelectedAcObjectViewModel)DataContext).Unload();
        }
    }

    public abstract class SelectedAcJsonObjectPage : SelectedAcObjectPage {
        public AcJsonObjectNew SelectedAcJsonObject => (AcJsonObjectNew)((ISelectedAcObjectViewModel)DataContext).SelectedAcObject;

        protected void InitializeAcObjectPage<T>(SelectedAcObjectViewModel<T> model) where T : AcJsonObjectNew {
            base.InitializeAcObjectPage(model);
            InputBindings.AddRange(new[] {
                new InputBinding(SelectedAcJsonObject.TagsCleanUpAndSortCommand, new KeyGesture(Key.T, ModifierKeys.Control | ModifierKeys.Alt))
            });
        }

        protected void VersionInfoBlock_OnMouseDown(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton == MouseButton.Left && e.ClickCount == 1) {
                e.Handled = true;
                new VersionInfoEditor(SelectedAcJsonObject).ShowDialog();
            }
        }

        protected void TagsList_OnMouseDown(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton != MouseButton.Right) return;

            new ContextMenu { Items = {
                new MenuItem { Header = "Clean Up Tags", Command = SelectedAcJsonObject.TagsCleanUpCommand },
                new MenuItem { Header = "Sort Tags", Command = SelectedAcJsonObject.TagsSortCommand },
                new MenuItem { Header = "Clean Up & Sort Tags", Command = SelectedAcJsonObject.TagsCleanUpAndSortCommand, InputGestureText = "Ctrl+Alt+T" }
            } }.IsOpen = true;
            e.Handled = true;
        }
    }
}
