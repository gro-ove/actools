using System;
using System.Runtime;

namespace AcTools.Utils {
    public class MemoryChunk {
        private readonly int _sizeInMegabytes;

        private MemoryChunk(int sizeInMegabytes) {
            _sizeInMegabytes = sizeInMegabytes;
        }

        public static MemoryChunk Bytes(int bytes) {
            return new MemoryChunk(10 + bytes / 1024 / 1024);
        }

        public static MemoryChunk Megabytes(int megabytes) {
            return new MemoryChunk(megabytes);
        }

        private T ExecuteInner<T>(Func<T> action) {
            if (_sizeInMegabytes < 20) return action();

            // AcToolsLogging.Write($"Going to allocate {_sizeInMegabytes} MB…");
            using (new MemoryFailPoint(_sizeInMegabytes)) return action();
        }

        public T Execute<T>(Func<T> action) {
            try {
                return ExecuteInner(action);
            } catch (InsufficientMemoryException) {
                GCHelper.CleanUp();
                return ExecuteInner(action);
            } catch (OutOfMemoryException) {
                GCHelper.CleanUp();
                return ExecuteInner(action);
            }
        }

        public void Execute(Action action) {
            Execute(() => {
                action();
                return 0;
            });
        }
    }
}