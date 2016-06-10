using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using AcManager.Tools.Helpers;
using AcTools.Processes;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Windows.Converters;

namespace AcManager.Pages.Drive {
    public partial class QuickDrive_Weekend : IQuickDriveModeControl {
        public QuickDrive_Weekend() {
            InitializeComponent();
            // DataContext = new QuickDrive_WeekendViewModel();
        }

        private bool _loaded;

        private void QuickDrive_Weekend_OnLoaded(object sender, RoutedEventArgs e) {
            if (_loaded) return;
            _loaded = true;
            ActualModel.Load();
        }

        private void QuickDrive_Weekend_OnUnloaded(object sender, RoutedEventArgs e) {
            if (!_loaded) return;
            _loaded = false;
            ActualModel.Unload();
        }

        public QuickDriveModeViewModel Model {
            get { return (QuickDriveModeViewModel)DataContext; }
            set { DataContext = value; }
        }

        public QuickDrive_WeekendViewModel ActualModel => (QuickDrive_WeekendViewModel)DataContext;

        public class QuickDrive_WeekendViewModel : QuickDrive_Race.QuickDrive_RaceViewModel {
            private int _practiceDuration;

            public int PracticeDuration {
                get { return _practiceDuration; }
                set {
                    value = value.Clamp(0, 90);
                    if (Equals(value, _practiceDuration)) return;
                    _practiceDuration = value;
                    OnPropertyChanged();
                }
            }

            private int _qualificationDuration;

            public int QualificationDuration {
                get { return _qualificationDuration; }
                set {
                    value = MathUtils.Clamp(value, 5, 90);
                    if (Equals(value, _qualificationDuration)) return;
                    _qualificationDuration = value;
                    OnPropertyChanged();
                }
            }

            public QuickDrive_WeekendViewModel(bool initialize = true) : base(initialize) {}

            private new class SaveableData : QuickDrive_Race.QuickDrive_RaceViewModel.SaveableData {
                public int? PracticeLength, QualificationLength;
            }

            protected override void Save(QuickDrive_Race.QuickDrive_RaceViewModel.SaveableData result) {
                base.Save(result);

                var r = (SaveableData)result;
                r.PracticeLength = PracticeDuration;
                r.QualificationLength = QualificationDuration;
            }

            protected override void Load(QuickDrive_Race.QuickDrive_RaceViewModel.SaveableData o) {
                base.Load(o);

                var r = (SaveableData)o;
                PracticeDuration = r.PracticeLength ?? 15;
                QualificationDuration = r.QualificationLength ?? 30;
            }

            protected override void Reset() {
                base.Reset();
                PracticeDuration = 15;
                QualificationDuration = 30;
            }

            protected override void InitializeSaveable() {
                Saveable = new SaveHelper<SaveableData>("__QuickDrive_Weekend", () => {
                    var r = new SaveableData();
                    Save(r);
                    return r;
                }, Load, Reset);
            }

            protected override Game.BaseModeProperties GetModeProperties(IEnumerable<Game.AiCar> botCars) {
                return new Game.WeekendProperties {
                    AiLevel = AiLevelFixed ? AiLevel : 100,
                    Penalties = Penalties,
                    StartingPosition = StartingPosition == 0 ? MathUtils.Random(1, OpponentsNumber + 2) : StartingPosition,
                    RaceLaps = LapsNumber,
                    BotCars = botCars,
                    PracticeDuration = PracticeDuration,
                    QualificationDuration = QualificationDuration
                };
            }
        }

        public static IValueConverter SpecialPluralizingConverter { get; }

        public static IValueConverter SpecialSessionConverter { get; }

        static QuickDrive_Weekend () {
            SpecialPluralizingConverter = new SpecialPluralizingConverter_Inner();
            SpecialSessionConverter = new SpecialSessionConverter_Inner();
        }

        private class SpecialPluralizingConverter_Inner : IValueConverter {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
                if (value == null || parameter == null) return null;
                var number = value.AsInt();
                return number != 0 ? PluralizingConverter.PluralizeExt(number, parameter.ToString()) : "";
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
                throw new NotSupportedException();
            }
        }

        private class SpecialSessionConverter_Inner : IValueConverter {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
                if (value == null) return null;
                var number = value.AsInt();
                return number == 0 ? "Skip" : number.ToString();
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
                if (value == null) return null;
                return value as string == "Skip" ? 0 : value.AsInt();
            }
        }
    }
}
