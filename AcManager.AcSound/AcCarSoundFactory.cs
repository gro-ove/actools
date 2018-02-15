using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using AcManager.Tools.Managers;
using AcTools.Render.Kn5Specific;
using AcTools.SoundbankPlayer;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.AcSound {
    public class AcCarSoundFactory : IAcCarSoundFactory {
        private static bool _initialized;

        public async Task<IAcCarSound> CreateAsync(string carDirectory) {
            if (!FmodResolverService.Resolver.IsInitialized) {
                Logging.Warning("FmodResolverService is not initialized!");
                return null;
            }

            try {
                return await CreateAsyncInner(carDirectory);
            } catch (Exception e) {
                Logging.Warning(e);
                return null;
            }
        }

        private static async Task<IAcCarSound> CreateAsyncInner(string carDirectory) {
            var directory = FmodResolverService.Resolver.Directory;
            if (directory == null || !Directory.Exists(directory)) {
                Logging.Warning("Fmod plugin directory not found");
                return null;
            }

            if (!_initialized) {
                _initialized = true;

                if (File.Exists(Path.Combine(directory, "fmod.dll"))) {
                    Logging.Write("Fmod libraries are in plugin folder");
                    AcCarPlayer.Initialize(directory);
                } else {
                    Logging.Write("Fmod libraries not in plugin folder, let’s use AC libs instead");
                    AcCarPlayer.Initialize(AcRootDirectory.Instance.RequireValue);
                }

                foreach (var file in Directory.GetFiles(directory, "Plugin*.dll")) {
                    AcCarPlayer.AddPlugin(file);
                }
            }

            var s = Stopwatch.StartNew();
            try {
                var player = AcCarPlayer.Create(AcRootDirectory.Instance.RequireValue, Path.GetFileName(carDirectory), carDirectory);
                await player.Initialize();
                return new AcCarSound(player);
            } catch (Exception e) {
                NonfatalError.NotifyBackground("Can’t load soundbank", e);
                return null;
            } finally {
                Logging.Debug($"Time taken: {s.Elapsed.TotalMilliseconds:F1} ms");
            }
        }
    }
}