using System;

namespace AcTools.Windows.Input {
    public static class SneakyPeekyFactory {
        private static Type _type;

        public static void Register<T>() where T : ISneakyPeeky, new() {
            _type = typeof(T);
        }

        public static ISneakyPeeky Get(bool subscribe = true) {
            if (_type == null) throw new NotSupportedException();

            var result = (ISneakyPeeky)Activator.CreateInstance(_type);
            if (subscribe) {
                result.Subscribe();
            }

            return result;
        }
    }
}