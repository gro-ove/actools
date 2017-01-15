using System;
using System.Threading;
using System.Threading.Tasks;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;

namespace AcTools.Processes {
    public partial class Showroom {
        public enum ShotMode {
            Classic,
            ClassicManual,
            Fixed
        }

        public class ShotProperties {
            public string AcRoot;
            public string ShowroomId, CarId;
            public string[] SkinIds;

            public ShotMode Mode;
            public double ClassicCameraDx, ClassicCameraDy, ClassicCameraDistance;
            public string FixedCameraPosition, FixedCameraLookAt;
            public double FixedCameraFov, FixedCameraExposure;

            public string Filter;
            public bool? Fxaa;

            public bool UseBmp = true,
                    DisableWatermark = true,
                    DisableSweetFx = false,
                    SpecialResolution = false,
                    MaximizeVideoSettings = false;
        }

        private static BaseShotter CreateShooter(ShotProperties properties) {
            BaseShotter shooter;

            switch (properties.Mode) {
                case ShotMode.Classic: {
                        var classicShooter = new ClassicShooter();
                        classicShooter.SetRotate(properties.ClassicCameraDx, properties.ClassicCameraDy);
                        classicShooter.SetDistance(properties.ClassicCameraDistance);
                        shooter = classicShooter;
                        break;
                    }

                case ShotMode.ClassicManual: {
                        var classicShooter = new ClassisManualShooter();
                        classicShooter.SetRotate(properties.ClassicCameraDx, properties.ClassicCameraDy);
                        classicShooter.SetDistance(properties.ClassicCameraDistance);
                        shooter = classicShooter;
                        break;
                    }

                case ShotMode.Fixed: {
                        var kunosShooter = new KunosShotter();
                        kunosShooter.SetCamera(properties.FixedCameraPosition, properties.FixedCameraLookAt,
                                               properties.FixedCameraFov, properties.FixedCameraExposure);
                        shooter = kunosShooter;
                        break;
                    }

                default:
                    throw new ArgumentOutOfRangeException();
            }

            shooter.AcRoot = properties.AcRoot;
            shooter.CarId = properties.CarId;
            shooter.SkinIds = properties.SkinIds;
            shooter.ShowroomId = properties.ShowroomId;
            shooter.DisableWatermark = properties.DisableWatermark;
            shooter.DisableSweetFx = properties.DisableSweetFx;
            shooter.Filter = properties.Filter;
            shooter.Fxaa = properties.Fxaa;
            shooter.SpecialResolution = properties.SpecialResolution;
            shooter.MaximizeVideoSettings = properties.MaximizeVideoSettings;

            return shooter;
        }

        [CanBeNull]
        public static string Shot(ShotProperties properties) {
            BaseShotter shooter = null;

            try {
                shooter = CreateShooter(properties);
                shooter.ShotAll();
                var result = shooter.OutputDirectory;
                shooter.Dispose();
                return result;
            } catch (Exception) {
                try {
                    shooter?.Dispose();
                } catch (Exception) {
                    // ignored
                }

                throw;
            }
        }

        public class ShootingProgress {
            public string SkinId;
            public int SkinNumber, TotalSkins;

            public double Progress => (double)SkinNumber / TotalSkins;
        }

        [ItemCanBeNull]
        public static async Task<string> ShotAsync(ShotProperties properties, IProgress<ShootingProgress> progress, CancellationToken cancellation) {
            var shooter = CreateShooter(properties);

            var iterableShooter = shooter as BaseIterableShooter;
            if (iterableShooter == null) {
                return await Task.Run(() => {
                    try {
                        shooter.ShotAll();
                        return shooter.OutputDirectory;
                    } finally {
                        shooter.Dispose();
                    }
                }, cancellation);
            }

            try {
                var skins = iterableShooter.CarSkins.ToIReadOnlyListIfItIsNot();
                var position = 0;
                foreach (var carSkin in skins) {
                    if (cancellation.IsCancellationRequested) return null;

                    progress.Report(new ShootingProgress {
                        SkinId = carSkin,
                        SkinNumber = position++,
                        TotalSkins = skins.Count
                    });

                    await iterableShooter.ShotAsync(carSkin);
                }

                return iterableShooter.OutputDirectory;
            } finally {
                iterableShooter.Dispose();
            }
        }
    }
}

