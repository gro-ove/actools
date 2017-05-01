using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace AcTools.Utils.Helpers {
    public static class DisposeHelper {
        [ContractAnnotation("=> disposable:null")]
        public static void Dispose<T>([CanBeNull] ref T disposable) where T : class, IDisposable {
            if (disposable == null) return;
            disposable.Dispose();
            disposable = null;
        }

        [ContractAnnotation("=> disposable:null")]
        public static void Dispose<T>([CanBeNull] ref IEnumerable<T> disposable) where T : class, IDisposable {
            if (disposable == null) return;
            disposable.DisposeEverything();
            (disposable as IDisposable)?.Dispose();
            disposable = null;
        }

        [ContractAnnotation("=> disposable:null")]
        public static void DisposeFirst<T, TOther>([CanBeNull] ref Tuple<T, TOther>[] disposable) where T : class, IDisposable {
            if (disposable == null) return;
            foreach (var tuple in disposable) {
                tuple.Item1.Dispose();
            }
            disposable = null;
        }

        [ContractAnnotation("=> disposable:null")]
        public static void DisposeSecond<T, TOther>([CanBeNull] ref Tuple<TOther, T>[] disposable) where T : class, IDisposable {
            if (disposable == null) return;
            foreach (var tuple in disposable) {
                tuple.Item2.Dispose();
            }
            disposable = null;
        }

        [ContractAnnotation("=> disposable:null")]
        public static void DisposeBoth<T, TOther>([CanBeNull] ref Tuple<T, TOther>[] disposable) where T : class, IDisposable where TOther : class, IDisposable {
            if (disposable == null) return;
            foreach (var tuple in disposable) {
                tuple.Item1.Dispose();
                tuple.Item2.Dispose();
            }
            disposable = null;
        }

        [ContractAnnotation("a:null, b:null => null; a:notnull => notnull; b:notnull => notnull")]
        public static IDisposable Join(this IDisposable a, IDisposable b) {
            return b == null ? a : (a == null ? b : new CombinedDisposable(a, b));
        }

        private class CombinedDisposable : IDisposable {
            private IDisposable _a, _b;

            public CombinedDisposable(IDisposable a, IDisposable b) {
                _a = a;
                _b = b;
            }

            public void Dispose() {
                DisposeHelper.Dispose(ref _a);
                DisposeHelper.Dispose(ref _b);
            }
        }

        public static IDisposable AsDisposable(this Action action) {
            return new ActionAsDisposable(action);
        }

        public static IDisposable Empty => new ActionAsDisposable(() => { });
    }

    public class ActionAsDisposable : IDisposable {
        private readonly Action _action;

        public ActionAsDisposable(Action action) {
            _action = action;
        }

        public void Dispose() {
            _action.Invoke();
        }
    }
}
