using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using AcManager.Tools.Helpers;
using AcTools.Processes;
using AcTools.Utils;
using FirstFloor.ModernUI.Windows.Converters;

namespace AcManager.Pages.Drive {
    public partial class QuickDrive_Weekend : IQuickDriveModeControl {
        public QuickDrive_Weekend() {
            InitializeComponent();
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
            get { return ActualModel; }
            set { DataContext = value; }
        }

        public ViewModel ActualModel => (ViewModel)DataContext;

        public class ViewModel : QuickDrive_Race.ViewModel {
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
                    value = value.Clamp(5, 90);
                    if (Equals(value, _qualificationDuration)) return;
                    _qualificationDuration = value;
                    OnPropertyChanged();
                }
            }

            public ViewModel(bool initialize = true) : base(initialize) {}

            private new class SaveableData : QuickDrive_Race.ViewModel.SaveableData {
                public int? PracticeLength, QualificationLength;
            }

            protected override void Save(QuickDrive_Race.ViewModel.SaveableData result) {
                base.Save(result);

                var r = (SaveableData)result;
                r.PracticeLength = PracticeDuration;
                r.QualificationLength = QualificationDuration;
            }

            protected override void Load(QuickDrive_Race.ViewModel.SaveableData o) {
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
                    JumpStartPenalty = JumpStartPenalty,
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

        [ValueConversion(typeof(int), typeof(string))]
        private class SpecialSessionConverter_Inner : IValueConverter {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
                if (value == null) return null;
                var number = value.AsInt();
                return number == 0 ? AppStrings.Drive_SkipSession : number.ToString();
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
                if (value == null) return null;
                return value as string == AppStrings.Drive_SkipSession ? 0 : value.AsInt();
            }
        }

        private void OpponentsCarsFilterTextBox_OnLostFocus(object sender, RoutedEventArgs e) {
            ((ViewModel)Model).AddOpponentsCarsFilter();
        }
    }
}
