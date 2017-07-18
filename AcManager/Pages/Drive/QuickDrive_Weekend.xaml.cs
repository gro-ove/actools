using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using AcManager.Tools.Helpers;
using AcManager.Tools.Objects;
using AcTools.Processes;
using AcTools.Utils;
using FirstFloor.ModernUI.Windows.Converters;

namespace AcManager.Pages.Drive {
    public partial class QuickDrive_Weekend : IQuickDriveModeControl {
        public QuickDrive_Weekend() {
            InitializeComponent();
        }

        private bool _loaded;

        private void OnLoaded(object sender, RoutedEventArgs e) {
            if (_loaded) return;
            _loaded = true;
            ActualModel.Load();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
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
            protected override bool IgnoreStartingPosition => true;

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

            protected new class OldSaveableData : QuickDrive_Race.ViewModel.OldSaveableData {
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

            protected override void Load(QuickDrive_Race.ViewModel.OldSaveableData o) {
                base.Load(o);

                var r = (OldSaveableData)o;
                PracticeDuration = r.PracticeLength ?? 15;
                QualificationDuration = r.QualificationLength ?? 30;
            }

            protected override void Reset() {
                base.Reset();
                PracticeDuration = 15;
                QualificationDuration = 30;
            }

            public override void CheckIfTrackFits(TrackObjectBase track) {
                TrackDoesNotFit = TagRequired("circuit", track);
            }

            protected override void InitializeSaveable() {
                Saveable = new SaveHelper<SaveableData>("__QuickDrive_Weekend", () => {
                    var r = new SaveableData();
                    Save(r);
                    return r;
                }, Load, Reset);
                Saveable.RegisterUpgrade<OldSaveableData>(QuickDrive_Race.ViewModel.OldSaveableData.Test, Load);
            }

            protected override Game.BaseModeProperties GetModeProperties(IEnumerable<Game.AiCar> botCars) {
                return new Game.WeekendProperties {
                    AiLevel = RaceGridViewModel.AiLevelFixed ? RaceGridViewModel.AiLevel : 100,
                    Penalties = Penalties,
                    JumpStartPenalty = JumpStartPenalty,
                    StartingPosition = RaceGridViewModel.StartingPositionLimited == 0
                            ? MathUtils.Random(1, RaceGridViewModel.OpponentsNumber + 2) : RaceGridViewModel.StartingPositionLimited,
                    RaceLaps = LapsNumber,
                    BotCars = botCars,
                    PracticeDuration = PracticeDuration,
                    QualificationDuration = QualificationDuration
                };
            }
        }

        public static IValueConverter SpecialPluralizingConverter { get; } = new SpecialPluralizingConverter_Inner();

        public static IValueConverter SpecialSessionConverter { get; } = new SpecialSessionConverter_Inner();

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
    }
}
