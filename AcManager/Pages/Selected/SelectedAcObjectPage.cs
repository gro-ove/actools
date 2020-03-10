using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using AcManager.Controls;
using AcManager.Controls.ViewModels;
using AcManager.Tools.AcManagersNew;
using JetBrains.Annotations;
using AcManager.Tools.AcObjectsNew;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Windows.Attached;
using FirstFloor.ModernUI.Windows.Media;

namespace AcManager.Pages.Selected {
    public class SelectedAcObjectPage : UserControl {
        public AcCommonObject SelectedAcObject { get; private set; }

        [UsedImplicitly]
        public SelectedAcObjectPage() { }

        protected void InitializeAcObjectPage([NotNull] ISelectedAcObjectViewModel model) {
            SelectedAcObject = model.SelectedAcObject;
            InputBindings.Clear();
            InputBindings.AddRange(new[] {
                new InputBinding(SelectedAcObject.ToggleFavouriteCommand, new KeyGesture(Key.B, ModifierKeys.Control)),
                new InputBinding(SelectedAcObject.CopyIdCommand, new KeyGesture(Key.C, ModifierKeys.Control)), // TODO: why doesn’t work after quick switching?
                new InputBinding(SelectedAcObject.CopyIdCommand, new KeyGesture(Key.C, ModifierKeys.Control | ModifierKeys.Shift | ModifierKeys.Alt)),
                new InputBinding(SelectedAcObject.CopyIdCommand, new KeyGesture(Key.C, ModifierKeys.Control | ModifierKeys.Shift)) {
                    CommandParameter = @"name"
                },
                new InputBinding(SelectedAcObject.CopyIdCommand, new KeyGesture(Key.C, ModifierKeys.Control | ModifierKeys.Alt)) { CommandParameter = @"path" },
                new InputBinding(SelectedAcObject.ViewInExplorerCommand, new KeyGesture(Key.F, ModifierKeys.Control)),
                new InputBinding(SelectedAcObject.ReloadCommand, new KeyGesture(Key.R, ModifierKeys.Control)) { CommandParameter = @"full" },
                // new InputBinding(SelectedAcObject.ToggleCommand, new KeyGesture(Key.D, ModifierKeys.Control)),
                new InputBinding(new DelegateCommand(ToggleObject), new KeyGesture(Key.D, ModifierKeys.Control)),
                new InputBinding(SelectedAcObject.SaveCommand, new KeyGesture(Key.S, ModifierKeys.Control)),
                new InputBinding(model.ChangeIdCommand, new KeyGesture(Key.F2, ModifierKeys.Control | ModifierKeys.Shift)),
                new InputBinding(model.CloneCommand, new KeyGesture(Key.D, ModifierKeys.Control | ModifierKeys.Shift)),
                new InputBinding(model.FindInformationCommand, new KeyGesture(Key.I, ModifierKeys.Control)),
                new InputBinding(SelectedAcObject.DeleteCommand, new KeyGesture(Key.Delete, ModifierKeys.Control))
            });
            DataContext = model;

            if (!_set) {
                _set = true;
                Loaded += OnLoaded;
                Unloaded += OnUnloaded;
            } else {
                model.Load();
            }

            UpdateBindingsLaterAsync().Forget();
        }

        private void ToggleObject() {
            if (this.GetParents().OfType<AcListPage>().FirstOrDefault()?.DataContext is IAcListPageViewModel v) {
                var collection = v.GetAcWrapperCollectionView();
                var index = collection.OfType<AcItemWrapper>().FindIndex(x => x.Id == SelectedAcObject.Id);
                var next = collection.OfType<AcItemWrapper>().ElementAtOrDefault(index + 1)
                        ?? collection.OfType<AcItemWrapper>().ElementAtOrDefault(index - 1);
                SelectedAcObject.ToggleCommand.Execute(null);
                if (next != null) {
                    collection.MoveCurrentTo(next);
                }
            } else {
                SelectedAcObject.ToggleCommand.Execute(null);
            }
        }

        protected void OnToggleClick(object sender, MouseButtonEventArgs e) {
            e.Handled = true;
            ToggleObject();
        }

        private async Task UpdateBindingsLaterAsync() {
            await Task.Delay(1);
            InputBindingBehavior.UpdateBindings(this);
        }

        private bool _set, _loaded;

        private void OnLoaded(object sender, System.Windows.RoutedEventArgs e) {
            if (_loaded) return;
            _loaded = true;

            ((ISelectedAcObjectViewModel)DataContext).Load();
        }

        private void OnUnloaded(object sender, System.Windows.RoutedEventArgs e) {
            if (!_loaded) return;
            _loaded = false;

            ((ISelectedAcObjectViewModel)DataContext).Unload();
        }
    }
}