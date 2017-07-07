using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI {
    public static class ActionExtension {
        public static void InvokeInMainThread(this Action action) {
            (Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher).Invoke(action);
        }

        public static Task InvokeInMainThreadAsync(this Func<Task> action) {
            return (Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher).InvokeAsync(action).Task;
        }

        public static T InvokeInMainThread<T>(this Func<T> action) {
            return (Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher).Invoke(action);
        }

        public static void InvokeInMainThreadAsync(this Action action) {
            (Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher).InvokeAsync(action);
        }

        [ContractAnnotation("baseFunc:null, extendingFunc:null => null; baseFunc:notnull => notnull; extendingFunc:notnull => notnull")]
        public static Func<T, bool> Or<T>([CanBeNull] this Func<T, bool> baseFunc, [CanBeNull] Func<T, bool> extendingFunc) {
            if (baseFunc == null) return extendingFunc;
            if (extendingFunc == null) return baseFunc;
            return arg => baseFunc(arg) || extendingFunc(arg);
        }

        [ContractAnnotation("baseFunc:null, extendingFunc:null => null; baseFunc:notnull => notnull; extendingFunc:notnull => notnull")]
        public static Func<T, bool> And<T>([CanBeNull] this Func<T, bool> baseFunc, [CanBeNull] Func<T, bool> extendingFunc) {
            if (baseFunc == null) return extendingFunc;
            if (extendingFunc == null) return baseFunc;
            return arg => baseFunc(arg) && extendingFunc(arg);
        }
    }
}