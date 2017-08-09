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

        private static bool IsCritical(this Type t) {
            return t.Namespace?.EndsWith(".Critical") == true;
        }

        [NotNull]
        public static IEnumerable<ContentRepairSuggestion> GetRepairSuggestions([NotNull] CarObject car, bool checkResources, bool criticalOnly = false) {
            var list = Assembly.GetExecutingAssembly().GetTypes().Concat(Types)
                               .Where(x => !x.IsAbstract && x.IsSubclassOf(typeof(CarRepairBase)))
                               .IfWhere(criticalOnly, x => x.IsCritical())
                               .Select(x => (CarRepairBase)Activator.CreateInstance(x))
                               .IfWhere(!checkResources, x => x.AffectsData).OrderByDescending(x => x.Priority)
                               .ToList();

            for (var i = list.Count - 1; i >= 0; i--) {
                var repair = list[i];
                if (!repair.IsAvailable(list)) {
                    list.RemoveAt(i);
                }
            }

            return list.SelectMany(x => x.GetSuggestions(car).IfSelect(x.GetType().IsCritical(), y => {
                y.IsCritical = true;
                return y;
            })).NonNull().OrderBy(x => x.DisplayName);
        }
    }

    public abstract class CarRepairBase {
        [NotNull]
        public abstract IEnumerable<ContentRepairSuggestion> GetSuggestions([NotNull] CarObject car);

        public abstract bool AffectsData { get; }

        public virtual double Priority => 0;

        public virtual bool IsAvailable(IEnumerable<CarRepairBase> repairs) {
            return true;
        }
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

        public override IEnumerable<ContentRepairSuggestion> GetSuggestions(CarObject car) {
            var data = car.AcdData;
            if (data == null || data.IsEmpty) return new ContentRepairSuggestion[0];

            var aspect = GetObsoletableAspect(car, data);
            return aspect == null ? new ContentRepairSuggestion[0] : new[] { aspect };
        }

        [CanBeNull]
        protected abstract ContentRepairSuggestion GetObsoletableAspect([NotNull] CarObject car, [NotNull] DataWrapper data);

        public override bool AffectsData => true;
    }
}