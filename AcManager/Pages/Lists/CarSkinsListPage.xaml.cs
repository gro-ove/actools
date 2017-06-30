using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using AcManager.Controls;
using JetBrains.Annotations;
using AcManager.Controls.ViewModels;
using AcManager.CustomShowroom;
using AcManager.Pages.Dialogs;
using AcManager.Pages.Windows;
using AcManager.Tools;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Filters;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Converters;
using StringBasedFilter;
using WaitingDialog = FirstFloor.ModernUI.Dialogs.WaitingDialog;

namespace AcManager.Pages.Lists {
    public partial class CarSkinsListPage : IParametrizedUriContent, ILoadableContent {
        public void OnUri(Uri uri) {
            _carId = uri.GetQueryParam("CarId");
            _filter = uri.GetQueryParam("Filter");
            if (_carId == null) {
                throw new Exception(ToolsStrings.Common_IdIsMissing);
            }
        }

        private string _carId;
        private CarObject _car;
        private string _filter;

        public async Task LoadAsync(CancellationToken cancellationToken) {
            if (_car == null) {
                _car = await CarsManager.Instance.GetByIdAsync(_carId);
                if (_car == null) throw new Exception(AppStrings.Common_CannotFindCarById);
            }

            await _car.SkinsManager.EnsureLoadedAsync();
        }

        public void Load() {
            if (_car == null) {
                _car = CarsManager.Instance.GetById(_carId);
                if (_car == null) throw new Exception(AppStrings.Common_CannotFindCarById);
            }

            _car.SkinsManager.EnsureLoaded();
        }

        public void Initialize() {
            DataContext = new ViewModel(_car, string.IsNullOrEmpty(_filter) ? null : Filter.Create(CarSkinObjectTester.Instance, _filter));
            InitializeComponent();
        }

        public CarSkinsListPage([NotNull] CarObject car, string filter = null) {
            _car = car;
            _filter = filter;
        }

        public CarSkinsListPage() { }

        private void OnLoaded(object sender, RoutedEventArgs e) {
            ((ViewModel)DataContext).Load();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            ((ViewModel)DataContext).Unload();
        }

        public class ViewModel : AcListPageViewModel<CarSkinObject> {
            public CarObject SelectedCar { get; private set; }

            public ViewModel([NotNull] CarObject car, IFilter<CarSkinObject> listFilter)
                    : base(car.SkinsManager, listFilter) {
                SelectedCar = car;
            }

            protected override string GetSubject() {
                return AppStrings.List_Skins;
            }

            private ICommand _resetPriorityCommand;

            public ICommand ResetPriorityCommand => _resetPriorityCommand ?? (_resetPriorityCommand = new AsyncCommand(async () => {
                var list = MainList.OfType<AcItemWrapper>().Select(x => x.Value as CarSkinObject).NonNull().Where(x => x.Priority.HasValue).ToList();
                var i = 0;
                using (var waiting = new WaitingDialog()) {
                    waiting.Report();
                    await Task.Delay(500, waiting.CancellationToken);
                    if (waiting.CancellationToken.IsCancellationRequested) return;

                    foreach (var skin in list) {
                        waiting.Report(++i, list.Count);
                        skin.Priority = 0;
                        skin.Save();
                        await Task.Delay(50, waiting.CancellationToken);
                        if (waiting.CancellationToken.IsCancellationRequested) return;
                    }
                }
            }));
        }

        public static void Open(CarObject car) {
            var mainWindow = Application.Current?.MainWindow as MainWindow;

            if (mainWindow == null || SettingsHolder.Interface.SkinsSetupsNewWindow) {
                CarSkinsDialog.Show(car);
                return;
            }

            var uri = UriExtension.Create("/Pages/Lists/CarSkinsListPage.xaml?CarId={0}", car.Id);
            var setupsLinks = mainWindow.MenuLinkGroups.OfType<LinkGroupFilterable>().Where(x => x.GroupKey == "skins").ToList();
            var existing = setupsLinks.FirstOrDefault(x => x.Source == uri);
            if (existing == null) {
                existing = new LinkGroupFilterable {
                    DisplayName = $"Skins for {car.DisplayName}",
                    GroupKey = "skins",
                    Source = uri
                };

                if (setupsLinks.Count >= 2) {
                    mainWindow.MenuLinkGroups.Remove(setupsLinks[0]);
                }

                mainWindow.MenuLinkGroups.Add(existing);
            }

            mainWindow.NavigateTo(uri);
        }

        #region Batch actions
        protected override IEnumerable<BatchAction> GetBatchActions() {
            return new BatchAction[] {
                CommonBatchActions.BatchAction_AddToFavourites.Instance,
                CommonBatchActions.BatchAction_RemoveFromFavourites.Instance,
                CommonBatchActions.BatchAction_SetRating.Instance,

                BatchAction_UpdateLivery.Instance,
                BatchAction_UpdatePreviews.Instance,
                BatchAction_ResetPriority.Instance,
                BatchAction_RemoveNumbers.Instance,
                BatchAction_RemoveUiSkinJson.Instance,
                BatchAction_CreateMissingUiSkinJson.Instance,
            };
        }

        public class BatchAction_UpdateLivery : BatchAction<CarSkinObject> {
            public static readonly BatchAction_UpdateLivery Instance = new BatchAction_UpdateLivery();
            public BatchAction_UpdateLivery()
                    : base("Update Liveries", "With previously used params", "Look", "Batch.UpdateLivery") {
                DisplayApply = "Update";
            }

            private bool _randomShape = ValuesStorage.GetBool("_ba.updateLivery.random", true);
            public bool RandomShape {
                get => _randomShape;
                set {
                    if (Equals(value, _randomShape)) return;
                    _randomShape = value;
                    OnPropertyChanged();
                    ValuesStorage.Set("_ba.updateLivery.random", value);
                }
            }

            protected override Task ApplyOverrideAsync(CarSkinObject obj) {
                return RandomShape ? LiveryIconEditor.GenerateRandomAsync(obj) : LiveryIconEditor.GenerateAsync(obj);
            }
        }

        public class BatchAction_UpdatePreviews : BatchAction<CarSkinObject> {
            public static readonly BatchAction_UpdatePreviews Instance = new BatchAction_UpdatePreviews();
            public BatchAction_UpdatePreviews()
                    : base("Update Previews", "With previously used params", "Look", null) {
                DisplayApply = "Update";
                InternalWaitingDialog = true;
            }

            public override Task ApplyAsync(IList list, IProgress<AsyncProgressEntry> progress, CancellationToken cancellation) {
                return OfType(list).Select(obj => {
                    var car = CarsManager.Instance.GetById(obj.CarId) ?? throw new Exception("Car not found");
                    return new ToUpdatePreview(car, obj);
                }).Run();
            }
        }

        public class BatchAction_RemoveNumbers : BatchAction<CarSkinObject> {
            public static readonly BatchAction_RemoveNumbers Instance = new BatchAction_RemoveNumbers();
            public BatchAction_RemoveNumbers()
                    : base("Remove Numbers", "In case they set incorrectly", "UI", null) {
                DisplayApply = "Reset";
            }

            protected override void ApplyOverride(CarSkinObject obj) {
                obj.SkinNumber = null;
            }
        }

        public class BatchAction_ResetPriority : BatchAction<CarSkinObject> {
            public static readonly BatchAction_ResetPriority Instance = new BatchAction_ResetPriority();
            public BatchAction_ResetPriority()
                    : base("Reset Priorities", "Set all priorities to 0", "UI", null) {
                DisplayApply = "Reset";
            }

            protected override void ApplyOverride(CarSkinObject obj) {
                obj.Priority = null;
            }
        }

        public class BatchAction_RemoveUiSkinJson : BatchAction<CarSkinObject> {
            public static readonly BatchAction_RemoveUiSkinJson Instance = new BatchAction_RemoveUiSkinJson();
            public BatchAction_RemoveUiSkinJson()
                    : base("Remove UI Files", "Easy way to clean up", "UI File", null) {
                DisplayApply = "Remove";
            }

            public override Task ApplyAsync(IList list, IProgress<AsyncProgressEntry> progress, CancellationToken cancellation) {
                return Task.Run(() => FileUtils.Recycle(OfType(list).Select(x => x.JsonFilename).ToArray()));
            }
        }

        public class BatchAction_CreateMissingUiSkinJson : BatchAction<CarSkinObject> {
            public static readonly BatchAction_CreateMissingUiSkinJson Instance = new BatchAction_CreateMissingUiSkinJson();
            public BatchAction_CreateMissingUiSkinJson()
                    : base("Create Missing UI Files", "And generate names from IDs", "UI File", null) {
                DisplayApply = "Check";
            }

            protected override void ApplyOverride(CarSkinObject obj) {
                obj.Save();
            }
        }
        #endregion
    }
}
