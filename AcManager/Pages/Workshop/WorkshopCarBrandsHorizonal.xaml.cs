using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using AcManager.Workshop;
using AcManager.Workshop.Data;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Media;

namespace AcManager.Pages.Workshop {
    public partial class WorkshopCarBrandsHorizonal : IParametrizedUriContent, ILoadableContent {
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
            _categories = (await WorkshopHolder.Client
                    .GetAsync<List<WorkshopContentCategory>>($"/{_categoryType}", cancellationToken)
                    .ConfigureAwait(false))
                    // .Repeat(10).Select(x => new WorkshopContentCategory {Icon = x.Icon,Name = x.Name,NewItems = x.NewItems,Uses = x.Uses}).ToList()
                    ;
        }

        public void Load() {
            LoadAsync(CancellationToken.None).Wait();
        }

        public void Initialize() {
            DataContext = new ViewModel(_categories, _contentType, _searchKeyword);
            InitializeComponent();
            UpdateColumnsLayout();
            UpdatePhaseState();
            Model.PropertyChanged += OnModelChanged;
        }

        private void OnPageLoaded(object sender, RoutedEventArgs e) { }

        private void OnSizeChanged(object sender, EventArgs args) {
            UpdateColumnsLayout();
        }

        private double _lastWidth;

        private double GetCategoriesItemSize() {
            return 106d;
        }

        private double GetCategoriesMaxColumns() {
            return ActualWidth < 1000d ? 3d : 5d;
        }

        private double GetCategoriesColumns() {
            return Model.LayoutPhase == 1 ? GetCategoriesMaxColumns() : 1d;
        }

        private double GetCategoriesWidth(double columns) {
            return GetCategoriesItemSize() * columns + 8d * 2d;
        }

        private double GetCategoriesCompactWidth() {
            return GetCategoriesWidth(1d);
        }

        private double GetDynamicCategoriesWidth() {
            return Model.LayoutPhase == 0 ? _lastWidth : GetCategoriesWidth(Model.LayoutPhase == 2 ? 1d : GetCategoriesColumns());
        }

        private double GetDynamicItemsOffset() {
            return GetDynamicCategoriesWidth();
        }

        private double GetItemsCompactWidth() {
            return Math.Min(_lastWidth / 4, 280d);
        }

        private double GetDynamicItemsWidth() {
            return Model.LayoutPhase < 2 ? _lastWidth - GetCategoriesWidth(GetCategoriesMaxColumns()) : GetItemsCompactWidth();
        }

        private double GetDynamicSelectedOffset() {
            return GetDynamicCategoriesWidth() + GetDynamicItemsWidth();
        }

        private void UpdatePhaseState() {
            CategoriesScroll.IsHitTestVisible = Model.LayoutPhase < 2;
            CategoriesOverlay.Visibility = Model.LayoutPhase < 2 ? Visibility.Collapsed : Visibility.Visible;
        }

        private void UpdateColumnsLayout() {
            if (_lastWidth != ActualWidth) {
                _lastWidth = ActualWidth;

                var categoryMaxRows = Math.Floor(ActualHeight / 106d);
                var categoryColumns = Math.Ceiling(_categories.Count / categoryMaxRows);
                if (categoryColumns < categoryMaxRows) {
                    ColumnFirst.MaxHeight = 20d + 106d * Math.Ceiling(Math.Sqrt(_categories.Count));
                }

                ColumnFirst.BeginAnimation(WidthProperty, null);
                ColumnFirst.Width = GetDynamicCategoriesWidth();
                CategoriesScroll.BeginAnimation(AnimatableScrollViewer.CustomHorizontalOffsetProperty, null);
                if (Model.LayoutPhase > 0) {
                    CategoriesList.ScrollIntoView(CategoriesList.SelectedItem);
                }

                ColumnSecond.BeginAnimation(WidthProperty, null);
                TransformSecond.BeginAnimation(TranslateTransform.XProperty, null);
                ColumnSecond.Width = GetDynamicItemsWidth();
                TransformSecond.X = GetDynamicItemsOffset();

                TransformThird.BeginAnimation(TranslateTransform.XProperty, null);
                ColumnThird.Width = _lastWidth - GetCategoriesCompactWidth() - GetItemsCompactWidth();
                TransformThird.X = GetDynamicSelectedOffset();
            }
        }

        private EasingFunctionBase _easingFunction;
        private EasingFunctionBase _invertedEasingFunction;

        private class InvertingEasingFunction : EasingFunctionBase {
            EasingFunctionBase _baseFunction;

            public InvertingEasingFunction(EasingFunctionBase baseFunction) {
                _baseFunction = baseFunction;
            }

            protected override Freezable CreateInstanceCore() {
                return new InvertingEasingFunction(null);
            }

            protected override double EaseInCore(double normalizedTime) {
                return 1d - _baseFunction?.Ease(1d - normalizedTime) ?? normalizedTime;
            }
        }

        private EasingFunctionBase GetEasingFunction() {
            return _easingFunction ?? (_easingFunction = (EasingFunctionBase)FindResource(@"StandardEase"));
        }

        private EasingFunctionBase GetInvertedEasingFunction() {
            return _invertedEasingFunction ?? (_invertedEasingFunction = new InvertingEasingFunction(GetEasingFunction()));
        }

        private DoubleAnimation AnimateX(double value, double currentValue, bool invertEasing = false, double duration = 0.4) {
            return new DoubleAnimation {
                EasingFunction = invertEasing ? GetInvertedEasingFunction() : GetEasingFunction(),
                Duration = TimeSpan.FromSeconds(duration == 0d ? 0.4 * ((currentValue - value) / 400d).Abs().Saturate() : duration),
                To = value == currentValue ? value + 0.01 : value
            };
        }

        private bool _updating;

        private async Task UpdateStoryboard() {
            if (_updating) return;
            _updating = true;
            IsHitTestVisible = false;
            await Task.Yield();

            var widthAnimation = AnimateX(GetDynamicCategoriesWidth(), ColumnFirst.Width);
            ColumnFirst.BeginAnimation(WidthProperty, widthAnimation);
            if (Model.LayoutPhase > 0 && CategoriesList.GetItemVisual(Model.SelectedCategory) is FrameworkElement element) {
                var transform = element.TransformToVisual(CategoriesList);
                var positionInScrollViewer = transform.Transform(new Point(0, 0));
                CategoriesScroll.BeginAnimation(AnimatableScrollViewer.CustomHorizontalOffsetProperty,
                        AnimateX(positionInScrollViewer.X - (GetCategoriesColumns() - 1d) * GetCategoriesItemSize() / 2d,
                                CategoriesScroll.CustomHorizontalOffset, duration: widthAnimation.Duration.TimeSpan.TotalSeconds));
            }

            ColumnSecond.BeginAnimation(WidthProperty, AnimateX(GetDynamicItemsWidth(), ColumnSecond.Width));
            TransformSecond.BeginAnimation(TranslateTransform.XProperty, AnimateX(GetDynamicItemsOffset(), TransformSecond.X));

            TransformThird.BeginAnimation(TranslateTransform.XProperty, AnimateX(GetDynamicSelectedOffset(), TransformThird.X));
            _updating = false;

            await Task.Delay(200);
            IsHitTestVisible = true;
        }

        private void OnModelChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(Model.LayoutPhase)) {
                UpdatePhaseState();
                UpdateStoryboard().Ignore();
            }
        }

        private ViewModel Model => (ViewModel)DataContext;

        public class ViewModel : NotifyPropertyChanged {
            private string _contentType;
            private string _searchKeyword;

            public int MaxPhase => _selectedObjectPrev != null ? 2 : _selectedCategoryPrev != null ? 1 : 0;

            private int _layoutPhase;

            public int LayoutPhase {
                get => _layoutPhase;
                set => Apply(value.Clamp(0, MaxPhase), ref _layoutPhase, () => {
                    SelectedCategory = value < 1 ? null : (_selectedCategory ?? _selectedCategoryPrev);
                    SelectedObject = value < 2 ? null : (_selectedObject ?? _selectedObjectPrev);
                });
            }

            public List<WorkshopContentCategory> Categories { get; }

            public BetterObservableCollection<ContentInfoBase> ObjectList { get; } = new BetterObservableCollection<ContentInfoBase>();

            private WorkshopContentCategory _selectedCategory;
            private WorkshopContentCategory _selectedCategoryPrev;

            public WorkshopContentCategory SelectedCategory {
                get => _selectedCategory;
                set => Apply(value, ref _selectedCategory, () => {
                    if (value != null) {
                        _selectedCategoryPrev = value;
                        OnPropertyChanged(nameof(AnySelectedCategory));
                    }
                    SelectedObject = null;
                    LoadObjectsAsync().Ignore();
                    UpdateLayoutPhase();
                });
            }

            private ContentInfoBase _selectedObject;
            private ContentInfoBase _selectedObjectPrev;

            public ContentInfoBase SelectedObject {
                get => _selectedObject;
                set => Apply(value, ref _selectedObject, () => {
                    if (value != null) {
                        _selectedObjectPrev = value;
                        OnPropertyChanged(nameof(AnySelectedObject));
                    }
                    LoadSelectedObjectAsync().Ignore();
                    UpdateLayoutPhase();
                });
            }

            public WorkshopContentCategory AnySelectedCategory => _selectedCategory ?? _selectedCategoryPrev;
            public ContentInfoBase AnySelectedObject => _selectedObject ?? _selectedObjectPrev;

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
                    var selectedCategory = SelectedCategory;
                    if (selectedCategory == null) return;
                    ObjectList.Clear();
                    var newList = await new WorkshopClient("http://192.168.1.10:3000")
                            .GetAsync<List<WorkshopContentCar>>($@"/{_contentType}?{_searchKeyword}={Uri.EscapeDataString(selectedCategory.Name)}");
                    // newList = newList.Repeat(10).ToList();
                    foreach (var item in newList) {
                        if (SelectedCategory == selectedCategory) {
                            ObjectList.Add(item);
                            await Task.Delay(30);
                        } else {
                            break;
                        }
                    }
                } catch (Exception e) {
                    Logging.Warning(e);
                }
            }

            public async Task LoadSelectedObjectAsync() {
                try {
                    var selectedObject = SelectedObject;
                    if (selectedObject == null || selectedObject.Versions?.Count > 0) return;
                    await Task.Delay(200);
                    var newObject = await new WorkshopClient("http://192.168.1.10:3000")
                            .GetAsync<WorkshopContentCar>($@"/{_contentType}/{Uri.EscapeDataString(selectedObject.Id)}");
                    var index = ObjectList.IndexOf(selectedObject);
                    var stillSelected = SelectedObject == selectedObject;
                    if (index != -1) {
                        ObjectList[index] = newObject;
                    }
                    if (stillSelected) {
                        Logging.Here();
                        _selectedObject = _selectedObjectPrev = newObject;
                        OnPropertyChanged(nameof(SelectedObject));
                        OnPropertyChanged(nameof(AnySelectedObject));
                    }
                } catch (Exception e) {
                    Logging.Warning(e);
                }
            }
        }

        private void OnCategoriesOverlayClick(object sender, MouseButtonEventArgs e) {
            Model.LayoutPhase = 1;
        }

        private void BrowseBack_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = Model.LayoutPhase > 0;
            e.Handled = Model.LayoutPhase > 0;
        }

        private void BrowseBack_Executed(object sender, ExecutedRoutedEventArgs e) {
            e.Handled = Model.LayoutPhase > 0;
            --Model.LayoutPhase;
        }

        private void BrowseForward_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = Model.LayoutPhase < Model.MaxPhase;
            e.Handled = Model.LayoutPhase < Model.MaxPhase;
        }

        private void BrowseForward_Executed(object sender, ExecutedRoutedEventArgs e) {
            e.Handled = Model.LayoutPhase < Model.MaxPhase;
            ++Model.LayoutPhase;
        }
    }
}