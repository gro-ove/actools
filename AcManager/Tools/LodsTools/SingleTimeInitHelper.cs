using System;
using System.Threading;
using System.Threading.Tasks;

namespace AcManager.Tools.Helpers.LodGeneratorServices {
    public class SingleTimeInitHelper {
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);
        private string _failed;
        private bool _done;

        public async Task DoAsync(Func<Task> taskGen, CancellationToken cancel) {
            if (_done) {
                if (_failed != null) throw new Exception(_failed);
                return;
            }
            await _lock.WaitAsync(cancel).ConfigureAwait(false);
            try {
                if (_done) {
                    if (_failed != null) throw new Exception(_failed);
                    return;
                }
                await taskGen().ConfigureAwait(false);
            }  catch (Exception e) {
                _failed = e.Message;
                throw;
            } finally {
                _lock.Release();
                if (!cancel.IsCancellationRequested) {
                    _done = true;
                }
            }
        }
    }
}