using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using AcManager.Tools.Profile;
using AcTools.LapTimes;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Controls {
    public enum BestLapsOrder {
        MostDrivenFirst, FastestFirst
    }

    public class BestLapsList : Control {
        static BestLapsList() {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(BestLapsList), new FrameworkPropertyMetadata(typeof(BestLapsList)));
        }

        private readonly BetterObservableCollection<LapTimeWrapped> _lapTimes;

        public BestLapsList() {
            _lapTimes = new BetterObservableCollection<LapTimeWrapped>();
            SetValue(EntriesPropertyKey, _lapTimes);
            AddHandler(FrameworkContentElement.LoadedEvent, new RoutedEventHandler(OnLoaded));
            AddHandler(FrameworkContentElement.UnloadedEvent, new RoutedEventHandler(OnUnloaded));
        }

        public static readonly DependencyPropertyKey EntriesPropertyKey = DependencyProperty.RegisterReadOnly(nameof(Entries), typeof(IList<LapTimeWrapped>),
                typeof(BestLapsList), new PropertyMetadata(null));

        public static readonly DependencyProperty EntriesProperty = EntriesPropertyKey.DependencyProperty;

        public IList<LapTimeWrapped> Entries => (IList<LapTimeWrapped>)GetValue(EntriesProperty);

        public static readonly DependencyPropertyKey LoadingPropertyKey = DependencyProperty.RegisterReadOnly(nameof(Loading), typeof(bool),
                typeof(BestLapsList), new PropertyMetadata(true));

        public static readonly DependencyProperty LoadingProperty = LoadingPropertyKey.DependencyProperty;

        public bool Loading => (bool)GetValue(LoadingProperty);

        public static readonly DependencyPropertyKey SingleEntryModePropertyKey = DependencyProperty.RegisterReadOnly(nameof(SingleEntryMode), typeof(bool),
                typeof(BestLapsList), new PropertyMetadata(false));

        public static readonly DependencyProperty SingleEntryModeProperty = SingleEntryModePropertyKey.DependencyProperty;

        public bool SingleEntryMode => (bool)GetValue(SingleEntryModeProperty);

        public static readonly DependencyProperty CarIdProperty = DependencyProperty.Register(nameof(CarId), typeof(string),
                typeof(BestLapsList), new PropertyMetadata(null, (o, e) => {
                    ((BestLapsList)o)._carId = (string)e.NewValue;
                    ((BestLapsList)o).UpdateDirty();
                }));

        private string _carId;

        public string CarId {
            get { return _carId; }
            set { SetValue(CarIdProperty, value); }
        }

        public static readonly DependencyProperty TrackIdProperty = DependencyProperty.Register(nameof(TrackId), typeof(string),
                typeof(BestLapsList), new PropertyMetadata(null, (o, e) => {
                    ((BestLapsList)o)._trackId = (string)e.NewValue;
                    ((BestLapsList)o).UpdateDirty();
                }));

        private string _trackId;

        public string TrackId {
            get { return _trackId; }
            set { SetValue(TrackIdProperty, value); }
        }

        public static readonly DependencyProperty LimitProperty = DependencyProperty.Register(nameof(Limit), typeof(int),
                typeof(BestLapsList), new PropertyMetadata(10, (o, e) => {
                    ((BestLapsList)o)._limit = (int)e.NewValue;
                    ((BestLapsList)o).UpdateDirty();
                }));

        private int _limit = 10;

        public int Limit {
            get { return _limit; }
            set { SetValue(LimitProperty, value); }
        }

        public static readonly DependencyProperty OrderProperty = DependencyProperty.Register(nameof(Order), typeof(BestLapsOrder),
                typeof(BestLapsList), new PropertyMetadata(BestLapsOrder.FastestFirst, (o, e) => {
                    ((BestLapsList)o)._order = (BestLapsOrder)e.NewValue;
                }));

        private BestLapsOrder _order = BestLapsOrder.FastestFirst;

        public BestLapsOrder Order {
            get { return _order; }
            set { SetValue(OrderProperty, value); }
        }

        private bool _dirty;

        private void OnLoaded(object o, EventArgs e) {
            Update();
            LapTimesManager.Instance.LapTimesChanged += OnLapTimesChanged;
        }

        private void OnUnloaded(object o, EventArgs e) {
            Update();
            LapTimesManager.Instance.LapTimesChanged -= OnLapTimesChanged;
        }

        private void OnLapTimesChanged(object sender, EventArgs e) {
            UpdateDirty();
        }

        private void UpdateDirty() {
            _dirty = true;
            Update();
        }

        private bool _busy;

        private async void Update() {
            if (!IsLoaded || !_dirty || _busy) return;
            _busy = true;

            try {
                var carId = CarId;
                var trackAcId = TrackId?.Replace('/', '-');

                var singleEntryMode = carId != null && TrackId != null;
                SetValue(SingleEntryModePropertyKey, singleEntryMode);

                SetValue(LoadingPropertyKey, true);
                await LapTimesManager.Instance.UpdateAsync();
                
                if (singleEntryMode) {
                    var entry = LapTimesManager.Instance.Entries.FirstOrDefault(x => x.CarId == carId && x.TrackAcId == trackAcId);
                    _lapTimes.ReplaceEverythingBy_Direct(entry == null ? new LapTimeWrapped[0] : new[] { new LapTimeWrapped(entry) });
                } else {
                    var enumerable = LapTimesManager.Instance.Entries.Where(x =>
                            (carId == null || x.CarId == carId) && (trackAcId == null || x.TrackAcId == trackAcId));

                    switch (Order) {
                        case BestLapsOrder.MostDrivenFirst:
                            enumerable = enumerable.OrderByDescending(x => PlayerStatsManager.Instance.GetDistanceDrivenAtTrackAcId(x.TrackAcId));
                            break;
                        case BestLapsOrder.FastestFirst:
                            enumerable = enumerable.OrderBy(x => x.LapTime);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    _lapTimes.ReplaceEverythingBy_Direct(enumerable.Take(Limit).Select(x => new LapTimeWrapped(x)));
                }
            } catch (Exception e) {
                Logging.Error(e);
            } finally {
                _dirty = false;
                _busy = false;
                ActionExtension.InvokeInMainThread(() => SetValue(LoadingPropertyKey, false));
            }
        }

        // visual only properties
        public static readonly DependencyProperty ShowTitleProperty = DependencyProperty.Register(nameof(ShowTitle), typeof(bool),
                typeof(BestLapsList), new FrameworkPropertyMetadata(true));

        public bool ShowTitle {
            get { return (bool)GetValue(ShowTitleProperty); }
            set { SetValue(ShowTitleProperty, value); }
        }

        public static readonly DependencyProperty EntryPaddingProperty = DependencyProperty.Register(nameof(EntryPadding), typeof(Thickness),
                typeof(BestLapsList), new FrameworkPropertyMetadata(new Thickness(20d, 0d, 0d, 0d)));

        public Thickness EntryPadding {
            get { return (Thickness)GetValue(EntryPaddingProperty); }
            set { SetValue(EntryPaddingProperty, value); }
        }
    }
}