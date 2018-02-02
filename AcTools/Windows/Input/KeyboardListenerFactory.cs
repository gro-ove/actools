using System;

namespace AcTools.Windows.Input {
    public static class KeyboardListenerFactory {
        private static Type _type;

        public static void Register<T>() where T : IKeyboardListener, new() {
            _type = typeof(T);
        }

        public static IKeyboardListener Get(bool subscribe = true) {
            if (_type == null) throw new NotSupportedException();

            var result = (IKeyboardListener)Activator.CreateInstance(_type);
            if (subscribe) {
                result.Subscribe();
            }

            return result;
        }
    }
}