using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using AcManager.Controls;
using AcManager.Tools.Objects;
using AcTools.DataFile;
using AcTools.Render.Kn5SpecificForward;
using AcTools.Render.Kn5SpecificSpecial;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;

namespace AcManager.CustomShowroom {
    public class AmbientShadowViewModel : AmbientShadowParams {
        [NotNull]
        private readonly CarObject _car;

        [NotNull]
        private readonly ToolsKn5ObjectRenderer _renderer;

        public AmbientShadowViewModel([NotNull] ToolsKn5ObjectRenderer renderer, [NotNull] CarObject car) {
            _car = car ?? throw new ArgumentNullException(nameof(car));
            _renderer = renderer;
            renderer.SubscribeWeak(OnPropertyChanged);
        }

        private void OnPropertyChanged(object sender, PropertyChangedEventArgs e) {
            switch (e.PropertyName) {
                case nameof(_renderer.AmbientShadowSizeChanged):
                    ActionExtension.InvokeInMainThread(() => {
                        _sizeSaveCommand?.RaiseCanExecuteChanged();
                        _sizeResetCommand?.RaiseCanExecuteChanged();
                    });
                    break;
            }
        }

        /*private static string ToString(Vector3 vec) {
            return $"{-vec.X:F3}, {vec.Y:F3}, {vec.Z:F3}";
        }*/

        private AsyncCommand _updateAmbientShadowCommand;

        public AsyncCommand UpdateCommand => _updateAmbientShadowCommand ?? (_updateAmbientShadowCommand = new AsyncCommand(async () => {
            if (_renderer.AmbientShadowSizeChanged) {
                SizeSaveCommand.Execute();
                if (_renderer.AmbientShadowSizeChanged) return;
            }

            try {
                using (var waiting = WaitingDialog.Create(ControlsStrings.CustomShowroom_AmbientShadows_Updating)) {
                    var cancellation = waiting.CancellationToken;
                    var progress = (IProgress<double>)waiting;

                    await Task.Run(() => {
                        var kn5 = _renderer.MainSlot.Kn5;
                        if (kn5 == null) return;

                        using (var renderer = new AmbientShadowRenderer(kn5, _car.AcdData) {
                            DiffusionLevel = (float)Diffusion / 100f,
                            SkyBrightnessLevel = (float)Brightness / 100f,
                            Iterations = Iterations,
                            HideWheels = HideWheels,
                            Fade = Fade,
                            CorrectLighting = CorrectLighting,
                            PoissonSampling = PoissonSampling,
                            ExtraBlur = ExtraBlur,
                            UpDelta = UpDelta,
                            BodyMultiplier = BodyMultiplier,
                            WheelMultiplier = WheelMultiplier,
                        }) {
                            renderer.CopyStateFrom(_renderer);
                            renderer.Initialize();
                            renderer.Shot(progress, cancellation);
                        }
                    });

                    waiting.Report(ControlsStrings.CustomShowroom_AmbientShadows_Reloading);
                }
            } catch (Exception e) {
                NonfatalError.Notify(ControlsStrings.CustomShowroom_AmbientShadows_CannotUpdate, e);
            }
        }));

        private DelegateCommand _sizeSaveCommand;

        public DelegateCommand SizeSaveCommand => _sizeSaveCommand ?? (_sizeSaveCommand = new DelegateCommand(() => {
            if (File.Exists(Path.Combine(_car.Location, "data.acd")) && ModernDialog.ShowMessage(
                    ControlsStrings.CustomShowroom_AmbientShadowsSize_EncryptedDataMessage,
                    ControlsStrings.CustomShowroom_AmbientShadowsSize_EncryptedData, MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;

            var data = Path.Combine(_car.Location, "data");
            Directory.CreateDirectory(data);
            new IniFile {
                ["SETTINGS"] = {
                    ["WIDTH"] = _renderer.AmbientShadowWidth,
                    ["LENGTH"] = _renderer.AmbientShadowLength,
                }
            }.Save(Path.Combine(data, "ambient_shadows.ini"));

            _renderer.AmbientShadowSizeChanged = false;
        }, () => _renderer.AmbientShadowSizeChanged));

        private DelegateCommand _sizeFitCommand;

        public DelegateCommand SizeFitCommand => _sizeFitCommand ?? (_sizeFitCommand = new DelegateCommand(() => { _renderer.FitAmbientShadowSize(); }));

        private DelegateCommand _sizeResetCommand;

        public DelegateCommand SizeResetCommand
            =>
                    _sizeResetCommand
                            ?? (_sizeResetCommand = new DelegateCommand(() => { _renderer.ResetAmbientShadowSize(); }, () => _renderer.AmbientShadowSizeChanged))
            ;
    }
}