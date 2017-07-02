using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using AcManager.Controls;
using AcManager.Controls.ViewModels;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Filters;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Converters;
using StringBasedFilter;

namespace AcManager.Pages.Lists {
    public partial class WeatherListPage : IParametrizedUriContent {
        public void OnUri(Uri uri) {
            var filter = uri.GetQueryParam("Filter");
            DataContext = new ViewModel(string.IsNullOrEmpty(filter) ? null : Filter.Create(WeatherObjectTester.Instance, filter));
            InitializeComponent();
        }

        private void OnLoaded(object sender, RoutedEventArgs e) {
            ((ViewModel)DataContext).Load();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            ((ViewModel)DataContext).Unload();
        }

        private class ViewModel : AcListPageViewModel<WeatherObject> {
            public ViewModel(IFilter<WeatherObject> listFilter)
                : base(WeatherManager.Instance, listFilter) {
            }

            protected override string GetSubject() {
                return AppStrings.List_Weather;
            }
        }

        #region Batch actions
        public class BatchAction_PackWeather : CommonBatchActions.BatchAction_Pack<WeatherObject> {
            public static readonly BatchAction_PackWeather Instance = new BatchAction_PackWeather();

            public BatchAction_PackWeather() : base(null) {}

            /*#region Properies
            private bool _packData = ValuesStorage.GetBool("_ba.packCars.data", true);
            public bool PackData {
                get => _packData;
                set {
                    if (Equals(value, _packData)) return;
                    _packData = value;
                    ValuesStorage.Set("_ba.packCars.data", value);
                    OnPropertyChanged();
                }
            }

            private bool _includeTemplates = ValuesStorage.GetBool("_ba.packCars.templates", true);
            public bool IncludeTemplates {
                get => _includeTemplates;
                set {
                    if (Equals(value, _includeTemplates)) return;
                    _includeTemplates = value;
                    ValuesStorage.Set("_ba.packCars.data", value);
                    OnPropertyChanged();
                }
            }
            #endregion*/

            protected override AcCommonObject.AcCommonObjectPackerParams GetParams() {
                return new AcCommonObject.AcCommonObjectPackerParams();
            }
        }

        protected override IEnumerable<BatchAction> GetBatchActions() {
            return CommonBatchActions.DefaultSet.Concat(new BatchAction[] {
                BatchAction_PackWeather.Instance,
            });
        }
        #endregion
    }
}
