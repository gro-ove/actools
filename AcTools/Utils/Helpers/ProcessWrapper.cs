using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using AcTools.Windows;
using Microsoft.Win32.SafeHandles;

namespace AcTools.Utils.Helpers {
    public class ProcessWrapper {
        public int ExitCode { get; private set; }

        private readonly Process _inner;
        private bool _exited, _haveProcessHandle, _signaled, _raisedOnExited, _watchForExit;
        private IntPtr _processHandle;

        public ProcessWrapper(Process inner) {
            _inner = inner;

            _processHandle = IntPtr.Zero;
            _haveProcessHandle = false;
            _watchForExit = false;
        }

        [ResourceExposure(ResourceScope.None)]
        [ResourceConsumption(ResourceScope.Machine, ResourceScope.Machine)]
        IntPtr GetProcessHandle(Kernel32.ProcessAccessFlags access, bool throwIfExited) {
            if (_haveProcessHandle) {
                if (throwIfExited) {
                    // Since haveProcessHandle is true, we know we have the process handle
                    // open with at least SYNCHRONIZE access, so we can wait on it with 
                    // zero timeout to see if the process has exited.
                    ProcessWaitHandle waitHandle = null;
                    try {
                        waitHandle = new ProcessWaitHandle(_processHandle);
                        if (waitHandle.WaitOne(0, false)) {
                            throw new InvalidOperationException("Process has exited");
                        }
                    } finally {
                        waitHandle?.Close();
                    }
                }
                return _processHandle;
            }

            var handle = Kernel32.OpenProcess(access, false, _inner.Id);
            if (throwIfExited && (access & Kernel32.ProcessAccessFlags.QueryInformation) != 0) {
                int exitCode;
                if (Kernel32.GetExitCodeProcess(handle, out exitCode) && exitCode != Kernel32.STILL_ACTIVE) {
                    throw new InvalidOperationException("Process has exited");
                }
            }

            return handle;
        }

        void ReleaseProcessHandle(IntPtr handle) {
            if (handle == IntPtr.Zero) return;
            if (_haveProcessHandle && handle == _processHandle) return;
            Kernel32.CloseHandle(handle);
        }

        void RaiseOnExited() {
            if (!_raisedOnExited) {
                lock (this) {
                    if (!_raisedOnExited) {
                        _raisedOnExited = true;
                    }
                }
            }
        }

        public bool HasExitedSafe {
            get {
                if (!_exited) {
                    IntPtr handle = IntPtr.Zero;
                    try {
                        handle = GetProcessHandle(Kernel32.ProcessAccessFlags.QueryLimitedInformation | Kernel32.ProcessAccessFlags.Synchronize, false);
                        if (handle == IntPtr.Zero || handle == new IntPtr(-1)) {
                            _exited = true;
                        } else {
                            int exitCode;

                            // Although this is the wrong way to check whether the process has exited,
                            // it was historically the way we checked for it, and a lot of code then took a dependency on
                            // the fact that this would always be set before the pipes were closed, so they would read
                            // the exit code out after calling ReadToEnd() or standard output or standard error. In order
                            // to allow 259 to function as a valid exit code and to break as few people as possible that
                            // took the ReadToEnd dependency, we check for an exit code before doing the more correct
                            // check to see if we have been signalled.
                            if (Kernel32.GetExitCodeProcess(handle, out exitCode) && exitCode != Kernel32.STILL_ACTIVE) {
                                _exited = true;
                                ExitCode = exitCode;
                            } else {

                                // The best check for exit is that the kernel process object handle is invalid, 
                                // or that it is valid and signaled.  Checking if the exit code != STILL_ACTIVE 
                                // does not guarantee the process is closed,
                                // since some process could return an actual STILL_ACTIVE exit code (259).
                                if (!_signaled) // if we just came from WaitForExit, don't repeat
                                {
                                    ProcessWaitHandle wh = null;
                                    try {
                                        wh = new ProcessWaitHandle(handle);
                                        _signaled = wh.WaitOne(0, false);
                                    } finally {
                                        wh?.Close();
                                    }
                                }
                                if (_signaled) {
                                    if (!Kernel32.GetExitCodeProcess(handle, out exitCode)) {
                                        throw new Win32Exception();
                                    }

                                    _exited = true;
                                    ExitCode = exitCode;
                                }
                            }
                        }
                    } finally {
                        ReleaseProcessHandle(handle);
                    }

                    if (_exited) {
                        RaiseOnExited();
                    }
                }
                return _exited;
            }
        }

        public bool WaitForExitSafe(int milliseconds) {
            var handle = IntPtr.Zero;
            bool exited;
            ProcessWaitHandle processWaitHandle = null;

            try {
                handle = GetProcessHandle(Kernel32.ProcessAccessFlags.Synchronize, false);
                if (handle == IntPtr.Zero || handle == new IntPtr(-1)) {
                    exited = true;
                } else {
                    processWaitHandle = new ProcessWaitHandle(handle);
                    if (processWaitHandle.WaitOne(milliseconds, false)) {
                        exited = true;
                        _signaled = true;
                    } else {
                        exited = false;
                        _signaled = false;
                    }
                }
            } finally {
                processWaitHandle?.Close();
                ReleaseProcessHandle(handle);
            }

            if (exited && _watchForExit) {
                RaiseOnExited();
            }

            return exited;
        }

        public void WaitForExitSafe() {
            WaitForExitSafe(-1);
        }

        public async Task WaitForExitSafeAsync(CancellationToken cancellation = default) {
            while (!HasExitedSafe) {
                await Task.Delay(300, cancellation);
                if (cancellation.IsCancellationRequested) return;
            }
        }

        internal class ProcessWaitHandle : WaitHandle {
            [ResourceExposure(ResourceScope.None)]
            [ResourceConsumption(ResourceScope.Machine, ResourceScope.Machine)]
            internal ProcessWaitHandle(IntPtr processHandle) { 
                SafeWaitHandle waitHandle;
                if (!Kernel32.DuplicateHandle(new HandleRef(this, Kernel32.GetCurrentProcess()), processHandle,
                        new HandleRef(this, Kernel32.GetCurrentProcess()),
                        out waitHandle, 0, false, Kernel32.DUPLICATE_SAME_ACCESS)) {
                    Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
                }

                SafeWaitHandle = waitHandle;
            }
        }
    }
}