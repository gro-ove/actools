using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Helpers.Api.TheSetupMarket;
using AcManager.Tools.Managers.Directories;
using AcManager.Tools.Objects;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Managers {
    public class CarSetupsManager : AcManagerNew<CarSetupObject> {
        public string CarId { get; }

        public override IAcDirectories Directories { get; }

        private CarSetupsManager(string carId, IAcDirectories directories) {
            CarId = carId;
            Directories = directories;
        }

        public static CarSetupsManager Create(CarObject car) {
            return new CarSetupsManager(car.Id, new CarSetupsDirectories(car));
        }

        protected override string CheckIfIdValid(string id) {
            if (!id.EndsWith(CarSetupObject.FileExtension, StringComparison.OrdinalIgnoreCase)) {
                return $"ID should end with “{CarSetupObject.FileExtension}”.";
            }

            if (id.IndexOf(Path.DirectorySeparatorChar) == -1) {
                return "ID should be in “track”\\“name” form.";
            }

            return base.CheckIfIdValid(id);
        }

        protected override string GetLocationByFilename(string filename, out bool inner) {
            if (Directories == null) {
                inner = false;
                return null;
            }
            return Directories.GetLocationByFilename(filename, out inner);
        }

        public virtual string SearchPattern => @"*.ini";

        private Regex _regex;

        protected override bool Filter(string id, string filename) => SearchPattern == @"*" || (_regex ?? (_regex = new Regex(
                SearchPattern.Replace(@".", @"[.]").Replace(@"*", @".*").Replace(@"?", @"."))))
                .IsMatch(id);

        protected override IEnumerable<AcPlaceholderNew> ScanOverride() {
            if (Directories == null) {
                return new List<AcPlaceholderNew>();
            }
            return Directories.GetContentFiles(SearchPattern).Select(dir => {
                var id = Directories.GetId(dir);
                return Filter(id, dir) ? CreateAcPlaceholder(id, Directories.CheckIfEnabled(dir)) : null;
            }).NonNull();
        }

        protected override CarSetupObject CreateAcObject(string id, bool enabled) {
            return new CarSetupObject(CarId, this, id, enabled);
        }
    }

    public enum CarSetupsRemoteSource {
        None = 0,
        TheSetupMarket
    }

    public class RemoteSetupsManager : BaseAcManager<RemoteCarSetupObject> {
        private static readonly List<Tuple<CarSetupsRemoteSource, string, WeakReference<RemoteSetupsManager>>> Instances =
                new List<Tuple<CarSetupsRemoteSource, string, WeakReference<RemoteSetupsManager>>>();

        private static void Purge() {
            for (var i = Instances.Count - 1; i >= 0; i--) {
                if (!Instances[i].Item3.TryGetTarget(out RemoteSetupsManager _)) {
                    Instances.RemoveAt(i);
                }
            }
        }

        [CanBeNull]
        private readonly List<RemoteSetupInformation> _data;

        public CarSetupsRemoteSource Source { get; }
        public string CarId { get; }

        protected RemoteSetupsManager(CarSetupsRemoteSource source, string carId, [CanBeNull] List<RemoteSetupInformation> data) {
            _data = data;
            Source = source;
            CarId = carId;

            Purge();
            Instances.Add(Tuple.Create(source, carId, new WeakReference<RemoteSetupsManager>(this)));
        }

        [CanBeNull]
        public static RemoteSetupsManager GetManager(CarSetupsRemoteSource source, string carId) {
            Purge();
            var reference = Instances.FirstOrDefault(x => x?.Item1 == source && x.Item2 == carId)?.Item3;
            return reference == null ? null : reference.TryGetTarget(out RemoteSetupsManager result) ? result : null;
        }

        protected override IEnumerable<AcPlaceholderNew> ScanOverride() {
            if (_data == null) yield break;
            foreach (var d in _data) {
                var o = new RemoteCarSetupObject(this, d);
                o.Load();
                o.PastLoad();
                yield return o;
            }
        }
    }

    public class TheSetupMarketAsManager : RemoteSetupsManager {
        private TheSetupMarketAsManager(string carId, List<RemoteSetupInformation> data) : base(CarSetupsRemoteSource.TheSetupMarket, carId, data) { }

        [ItemNotNull]
        public static async Task<TheSetupMarketAsManager> CreateAsync(CarObject car) {
            return new TheSetupMarketAsManager(car.Id, await TheSetupMarketApiProvider.GetAvailableSetups(car.Id).ConfigureAwait(false));
        }
    }
}