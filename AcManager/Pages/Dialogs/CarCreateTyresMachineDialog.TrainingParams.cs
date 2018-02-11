using System;
using System.Linq;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers.Presets;
using AcTools.NeuralTyres.Data;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Pages.Dialogs {
    public partial class CarCreateTyresMachineDialog {
        public static readonly string DefaultNeuralLayers = NeuralTyresOptions.Default.Layers.JoinToString(@"; ");
        public static readonly bool DefaultSeparateNetworks = NeuralTyresOptions.Default.SeparateNetworks;
        public static readonly bool DefaultHighPrecision = NeuralTyresOptions.Default.HighPrecision;
        public static readonly int DefaultTrainingRuns = NeuralTyresOptions.Default.TrainingRuns;
        public static readonly int DefaultAverageAmount = NeuralTyresOptions.Default.AverageAmount;
        public static readonly FannTrainingAlgorithm DefaultFannAlgorithm = NeuralTyresOptions.Default.FannAlgorithm;
        public static readonly double DefaultLearningMomentum = NeuralTyresOptions.Default.LearningMomentum;
        public static readonly double DefaultLearningRate = NeuralTyresOptions.Default.LearningRate;
        public static readonly double DefaultRandomBounds = NeuralTyresOptions.Default.RandomBounds;

        public static SettingEntry[] FannAlgorithms { get; } = EnumExtension.GetValues<FannTrainingAlgorithm>().Select(x => {
            var n = x.GetDescription().Split(new[] { ':' }, 2);
            return new SettingEntry((int)x, n[0]) { Tag = n[1] };
        }).ToArray();

        public class TrainingViewModel : NotifyPropertyChanged, IUserPresetable {
            private class Data {
                public string Layers = DefaultNeuralLayers;
                public bool SeparateNetworks = DefaultSeparateNetworks, HighPrecision = DefaultHighPrecision;
                public int TrainingRuns = DefaultTrainingRuns, AverageAmount = DefaultAverageAmount;
                public FannTrainingAlgorithm FannAlgorithm = DefaultFannAlgorithm;
                public double LearningMomentum = DefaultLearningMomentum, LearningRate = DefaultLearningRate, RandomBounds = DefaultRandomBounds;
            }

            private readonly ISaveHelper _saveable;

            public TrainingViewModel() {
                (_saveable = new SaveHelper<Data>("CreateTyres.TrainingParams", () => new Data {
                    Layers = Layers,
                    SeparateNetworks = SeparateNetworks,
                    TrainingRuns = TrainingRuns,
                    AverageAmount = AverageAmount,
                    LearningMomentum = LearningMomentum,
                    LearningRate = LearningRate,
                    HighPrecision = HighPrecision,
                    RandomBounds = RandomBounds,
                    FannAlgorithm = (FannTrainingAlgorithm?)FannAlgorithm.IntValue ?? DefaultFannAlgorithm,
                }, o => {
                    Layers = o.Layers;
                    SeparateNetworks = o.SeparateNetworks;
                    TrainingRuns = o.TrainingRuns;
                    AverageAmount = o.AverageAmount;
                    LearningMomentum = o.LearningMomentum;
                    LearningRate = o.LearningRate;
                    HighPrecision = o.HighPrecision;
                    RandomBounds = o.RandomBounds;
                    FannAlgorithm = FannAlgorithms.GetByIdOrDefault((int?)o.FannAlgorithm) ?? FannAlgorithms.GetById((int?)DefaultFannAlgorithm);
                })).Initialize();
            }

            #region Utils
            bool IUserPresetable.CanBeSaved => true;
            string IUserPresetable.PresetableKey => TrainingParamsKey;
            PresetsCategory IUserPresetable.PresetableCategory => TrainingParamsCategory;

            public event EventHandler Changed;

            public void ImportFromPresetData(string data) {
                _saveable.FromSerializedString(data);
            }

            public string ExportToPresetData() {
                return _saveable.ToSerializedString();
            }

            private void SaveLater() {
                _saveable.SaveLater();
                Changed?.Invoke(this, EventArgs.Empty);
            }
            #endregion

            #region Actual params
            private SettingEntry _fannAlgorithm = FannAlgorithms.GetById((int?)DefaultFannAlgorithm);

            public SettingEntry FannAlgorithm {
                get => _fannAlgorithm;
                set => Apply(
                        FannAlgorithms.ArrayContains(value) == false ? FannAlgorithms.GetById((int?)DefaultFannAlgorithm) : value,
                        ref _fannAlgorithm, SaveLater);
            }

            private string _layers = DefaultNeuralLayers;

            public string Layers {
                get => _layers;
                set => Apply(value, ref _layers, SaveLater);
            }

            private bool _separateNetworks = DefaultSeparateNetworks;

            public bool SeparateNetworks {
                get => _separateNetworks;
                set => Apply(value, ref _separateNetworks, SaveLater);
            }

            private bool _highPrecision = DefaultHighPrecision;

            public bool HighPrecision {
                get => _highPrecision;
                set => Apply(value, ref _highPrecision, SaveLater);
            }

            private int _trainingRuns = DefaultTrainingRuns;

            public int TrainingRuns {
                get => _trainingRuns;
                set => Apply(value, ref _trainingRuns, SaveLater);
            }

            private int _averageAmount = DefaultAverageAmount;

            public int AverageAmount {
                get => _averageAmount;
                set => Apply(value, ref _averageAmount, SaveLater);
            }

            private double _learningMomentum = DefaultLearningMomentum;

            public double LearningMomentum {
                get => _learningMomentum;
                set => Apply(value, ref _learningMomentum, SaveLater);
            }

            private double _learningRate = DefaultLearningRate;

            public double LearningRate {
                get => _learningRate;
                set => Apply(value, ref _learningRate, SaveLater);
            }

            private double _randomBounds = DefaultRandomBounds;

            public double RandomBounds {
                get => _randomBounds;
                set => Apply(value, ref _randomBounds, SaveLater);
            }
            #endregion
        }
    }
}