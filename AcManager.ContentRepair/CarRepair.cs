using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.Objects;
using AcTools.DataFile;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Dialogs;
using JetBrains.Annotations;

namespace AcManager.ContentRepair {
    public static class CarRepair {
        private static readonly List<Type> Types = new List<Type>();

        public static void AddType<T>() where T: CarRepairBase {
            Types.Add(typeof(T));
        }

        public static IEnumerable<ObsoletableAspect> GetObsoletableAspects([NotNull] CarObject car, bool checkResources) {
            var repairs = Assembly.GetExecutingAssembly().GetTypes()
                                  .Concat(Types)
                                  .Where(x => !x.IsAbstract && x.IsSubclassOf(typeof(CarRepairBase)))
                                  .Select(x => ((CarRepairBase)Activator.CreateInstance(x)));

            if (!checkResources) {
                repairs = repairs.Where(x => x.AffectsData);
            }

            return repairs.SelectMany(x => x.GetObsoletableAspects(car)).NonNull().OrderBy(x => x.DisplayName);
        }
    }

    public abstract class CarRepairBase {
        [NotNull]
        public abstract IEnumerable<ObsoletableAspect> GetObsoletableAspects([NotNull] CarObject car);

        public abstract bool AffectsData { get; }
    }

    public abstract class CarSimpleRepairBase : CarRepairBase {
        protected virtual Task<bool> FixAsync([NotNull] CarObject car, IProgress<AsyncProgressEntry> progress = null,
                CancellationToken cancellation = default(CancellationToken)) {
            progress?.Report(AsyncProgressEntry.FromStringIndetermitate("Fixing car…"));
            return Task.Run(() => {
                var data = car.AcdData;
                if (data == null || data.IsEmpty) return false;
                Fix(car, data);
                return true;
            });
        }

        protected abstract void Fix([NotNull] CarObject car, [NotNull] DataWrapper data);
        
        public override IEnumerable<ObsoletableAspect> GetObsoletableAspects(CarObject car) {
            var data = car.AcdData;
            if (data == null || data.IsEmpty) return new ObsoletableAspect[0];

            var aspect = GetObsoletableAspect(car, data);
            return aspect == null ? new ObsoletableAspect[0] : new[] { aspect };
        }

        [CanBeNull]
        protected abstract ObsoletableAspect GetObsoletableAspect([NotNull] CarObject car, [NotNull] DataWrapper data);

        public override bool AffectsData => true;
    }
}