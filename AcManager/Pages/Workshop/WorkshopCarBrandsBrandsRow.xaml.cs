using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Animation;
using AcManager.Workshop;
using AcManager.Workshop.Data;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;

namespace AcManager.Pages.Workshop {
    public partial class WorkshopCarBrandsRow : IParametrizedUriContent, ILoadableContent {
        private string _contentType;
        private string _categoryType;
        private string _searchKeyword;
        private List<WorkshopContentCategory> _categories;

        public void OnUri(Uri uri) {
            _contentType = uri.GetQueryParam("Content");
            _categoryType = uri.GetQueryParam("Category");
            _searchKeyword = uri.GetQueryParam("SearchBy");
        }

        public async Task LoadAsync(CancellationToken cancellationToken) {
            _categories = (await new WorkshopClient("http://192.168.1.10:3000")
                    .GetAsync<List<WorkshopContentCategory>>($"/{_categoryType}", cancellationToken)
                    .ConfigureAwait(false)).Repeat(10).ToList();
        }

        public void Load() {
            LoadAsync(CancellationToken.None).Wait();
        }

        public void Initialize() {
            DataContext = new ViewModel(_categories, _contentType, _searchKeyword);
            InitializeComponent();
            UpdateColumnsLayout();
            Model.PropertyChanged += OnModelChanged;
        }

        private void OnSizeChanged(object sender, EventArgs args) {
            UpdateColumnsLayout();
        }

        private double _lastWidth;
        private double _sectionSize;

        private double GetTranslateValue(int column) {
            switch (column) {
                case 0:
                    return Model.LayoutPhase == 0 ? ActualHeight : 106d;
                case 1:
                    return Model.LayoutPhase == 0 ? _sectionSize * 2d
                            : Model.LayoutPhase == 1 ? _sectionSize / 1.5d
                                    : 0d;
                case 2:
                    return Model.LayoutPhase <= 1 ? _sectionSize * 2d : _sectionSize;
                default:
                    return 0d;
            }
        }

        private void UpdateColumnsLayout() {
            if (_lastWidth != ActualWidth) {
                _lastWidth = ActualWidth;
                _sectionSize = _lastWidth / 3d;
                ColumnFirst.Width = _lastWidth;
                ColumnSecond.Width = _sectionSize;
                ColumnThird.Width = _sectionSize;

                ColumnFirst.BeginAnimation(HeightProperty, null);
                ColumnFirst.Height = GetTranslateValue(0);

                TransformSecond.BeginAnimation(TranslateTransform.XProperty, null);
                TransformThird.BeginAnimation(TranslateTransform.XProperty, null);
                TransformSecond.X = GetTranslateValue(1);
                TransformThird.X = GetTranslateValue(2);
            }
        }

        private EasingFunctionBase _easingFunction;

        private EasingFunctionBase GetEasingFunction() {
            return _easingFunction ?? (_easingFunction = (EasingFunctionBase)FindResource(@"StandardEase"));
        }

        private DoubleAnimation AnimateTo(double value) {
           return new DoubleAnimation {
                EasingFunction = GetEasingFunction(),
                Duration = TimeSpan.FromSeconds(0.2),
                To = value
            };
        }

        private bool _updating;

        private async Task UpdateStoryboard() {
            if (_updating) return;
            _updating = true;
            IsHitTestVisible = false;
            await Task.Yield();
            ColumnFirst.BeginAnimation(HeightProperty, AnimateTo(GetTranslateValue(0)));
            TransformSecond.BeginAnimation(TranslateTransform.XProperty, AnimateTo(GetTranslateValue(1)));
            TransformThird.BeginAnimation(TranslateTransform.XProperty, AnimateTo(GetTranslateValue(2)));
            _updating = false;
            await Task.Delay(200);
            IsHitTestVisible = true;
        }

        private void OnModelChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(Model.LayoutPhase)) {
                UpdateStoryboard().Ignore();
            }
        }

        private ViewModel Model => (ViewModel)DataContext;

        public class ViewModel : NotifyPropertyChanged {
            private string _contentType;
            private string _searchKeyword;

            private int _layoutPhase;

            public int LayoutPhase {
                get => _layoutPhase;
                set => Apply(value, ref _layoutPhase);
            }

            public List<WorkshopContentCategory> Categories { get; }

            public BetterObservableCollection<WorkshopContentCar> ObjectList { get; } = new BetterObservableCollection<WorkshopContentCar>();

            private WorkshopContentCategory _selectedCategory;

            public WorkshopContentCategory SelectedCategory {
                get => _selectedCategory;
                set => Apply(value, ref _selectedCategory, () => {
                    LoadObjectsAsync().Ignore();
                    UpdateLayoutPhase();
                });
            }

            private WorkshopContentCar _selectedObject;

            public WorkshopContentCar SelectedObject {
                get => _selectedObject;
                set => Apply(value, ref _selectedObject, () => {
                    LoadSelectedObjectAsync().Ignore();
                    UpdateLayoutPhase();
                });
            }

            public ViewModel(List<WorkshopContentCategory> categories, string contentType, string searchKeyword) {
                Categories = categories;
                _contentType = contentType;
                _searchKeyword = searchKeyword;
            }

            private void UpdateLayoutPhase() {
                LayoutPhase = Math.Max(LayoutPhase, SelectedCategory == null ? 0 : SelectedObject == null ? 1 : 2);
            }

            public async Task LoadObjectsAsync() {
                try {
                    var newList = await new WorkshopClient("http://192.168.1.10:3000")
                            .GetAsync<List<WorkshopContentCar>>($@"/{_contentType}?{_searchKeyword}={Uri.EscapeDataString(SelectedCategory.Name)}");
                    ObjectList.ReplaceEverythingBy_Direct(newList.Repeat(10));
                } catch (Exception e) {
                    Logging.Warning(e);
                }
            }

            public async Task LoadSelectedObjectAsync() {
                try {
                    var selectedObject = SelectedObject;
                    var newObject = await new WorkshopClient("http://192.168.1.10:3000")
                            .GetAsync<WorkshopContentCar>($@"/car/{Uri.EscapeDataString(selectedObject.Id)}");
                    var index = ObjectList.IndexOf(selectedObject);
                    if (index != -1) {
                        ObjectList[index] = newObject;
                    }
                } catch (Exception e) {
                    Logging.Warning(e);
                }
            }
        }
    }
}