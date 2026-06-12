using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.Helpers.Api {
    public partial class KunosApiProvider {
        private class PingingSocket {
            private class WaitingRecord {
                public readonly TaskCompletionSource<PingResponse> TaskSource = new TaskCompletionSource<PingResponse>();
                public long PingStartTime = long.MaxValue;
            }

            private readonly Socket _socket;
            private readonly ConcurrentDictionary<long, WaitingRecord> _waitingK = new ConcurrentDictionary<long, WaitingRecord>();
            private int _loopRunning;

            private static long GetAddressKey(EndPoint endPoint) {
                var ip = (IPEndPoint)endPoint;
                return ((long)ip.Address.GetHashCode() << 16) /* for IPv4, .GetHashCode() is `int` with the address */ | (ushort)ip.Port;
            }

            public PingingSocket() {
                _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp) {
                    SendTimeout = 5000,
                    ReceiveTimeout = 5000,
                    ReceiveBufferSize = 256 * 1024 // 64 KB by default, increasing to 256 KB to be able to handle bursts
                };
                _socket.Bind(new IPEndPoint(IPAddress.Any, 0));
                Receive();
            }

            private async Task Loop() {
                Logging.Warning($"[NewPing] New loop");

                var toTimeout = new List<long>();
                var emptyHits = 0;
                while (true) {
                    if (_waitingK.IsEmpty && ++emptyHits > 20) {
                        _loopRunning = 0;
                        break;
                    }

                    emptyHits = 0;
                    toTimeout.Clear();
                    var timeoutThreshold = Stopwatch.GetTimestamp() - Stopwatch.Frequency * SettingsHolder.Online.PingTimeout / 1000;
                    foreach (var p in _waitingK) {
                        if (p.Value.PingStartTime < timeoutThreshold) {
                            toTimeout.Add(p.Key);
                        }
                    }

                    foreach (var i in toTimeout) {
                        if (_waitingK.TryRemove(i, out var record)) {
                            record.TaskSource.TrySetResult(new PingResponse("Timeout"));
                        }
                    }
                    
                    await Task.Delay(500).ConfigureAwait(false);
                }
            }

            private class ReceiveState {
                public readonly byte[] Buffer = new byte[4];
                public readonly SemaphoreSlim Semaphore = new SemaphoreSlim(0, 1);
                public EndPoint EndPoint;
            }

            private async void Receive() {
                var state = new ReceiveState();
                while (true) {
                    try {
                        state.EndPoint = new IPEndPoint(IPAddress.Any, 0);
                        _socket.BeginReceiveFrom(state.Buffer, 0, 4, SocketFlags.None, ref state.EndPoint, ReceiveCallback, state);
                        await state.Semaphore.WaitAsync().ConfigureAwait(false);
                    } catch (Exception e) {
                        Logging.Warning($"Receive error: {e.Message}");
                        await Task.Delay(1000).ConfigureAwait(false);
                    }
                }
            }

            private void ReceiveCallback(IAsyncResult ar) {
                long pingEndTime = 0;
                var args = (ReceiveState)ar.AsyncState;
                try {
                    var receivedBytes = 0;
                    string receiveError = null;
                    try {
                        receivedBytes = _socket.EndReceiveFrom(ar, ref args.EndPoint);
                        pingEndTime = Stopwatch.GetTimestamp();  // ← move here
                    } catch (SocketException e) when (e.SocketErrorCode == SocketError.MessageSize) {
                        receivedBytes = -1;
                    } catch (Exception e) {
                        receiveError = e.Message;
                    }

                    if (_waitingK.TryRemove(GetAddressKey(args.EndPoint), out var record)) {
                        if (receivedBytes == 3 && args.Buffer[0] == 200) {
                            record.TaskSource.TrySetResult(new PingResponse(BitConverter.ToInt16(args.Buffer, 1),
                                    TimeSpan.FromMilliseconds((pingEndTime - record.PingStartTime) * 1e3 / Stopwatch.Frequency)));
                        } else {
                            record.TaskSource.TrySetResult(new PingResponse(receiveError
                                    ?? $"Malformed response, {(receivedBytes == -1 ? "too many" : receivedBytes.ToInvariantString())} bytes, data: {args.Buffer.JoinToString(@", ")}"));
                        }
                    } else {
                        Logging.Error($"Missing pinging task (1): {args.EndPoint}");
                    }
                } finally {
                    args.Semaphore.Release();
                }
            }

            private ConcurrentStack<SocketAsyncEventArgs> _eventArgs =  new ConcurrentStack<SocketAsyncEventArgs>();
            
            public Task<PingResponse> SendAsync(EndPoint destination) {
                var key = GetAddressKey(destination);
                var record = _waitingK.GetOrAdd(key, i => new WaitingRecord());
                
                if (Interlocked.CompareExchange(ref record.PingStartTime, Stopwatch.GetTimestamp(), long.MaxValue) == long.MaxValue) {
                    // There is a small chance ReceiveCallback will fire before OnSendCompleted will set PingStartTime. In this case,
                    // this assignment will also help to make the result somewhat sensible. In such case (LAN, etc) we don’t really need
                    // any precision anyway.
                    try {
                        if (!_eventArgs.TryPop(out var args)) {
                            args = new SocketAsyncEventArgs();
                            args.Completed += OnSendCompleted;
                            args.SetBuffer(new byte[]{ 200 }, 0, 1);
                        }
                        args.RemoteEndPoint = destination;
                        args.UserToken = record;
                        if (!_socket.SendToAsync(args)) {
                            OnSendCompleted(this, args); // completed synchronously
                        }
                        if (Interlocked.Exchange(ref _loopRunning, 1) == 0) {
                            Task.Run(Loop);
                        }
                    } catch (Exception e) {
                        record.TaskSource.TrySetResult(new PingResponse($"SendToAsync(): {e.Message}"));
                        _waitingK.TryRemove(key, out _);
                    }
                }
                
                // If the same IP is being pinged already, we’re ok with returning a stale task
                return record.TaskSource.Task;
            }

            private void OnSendCompleted(object sender, SocketAsyncEventArgs args) {
                var record = (WaitingRecord)args.UserToken;
                if (args.SocketError != SocketError.Success) {
                    if (_waitingK.TryRemove(GetAddressKey(args.RemoteEndPoint), out _)) {
                        record.TaskSource.TrySetResult(new PingResponse($"Send err: {args.SocketError}"));
                    } else {
                        Logging.Error($"Missing pinging task (2): {args.RemoteEndPoint}");
                    }
                } else {
                    record.PingStartTime = Stopwatch.GetTimestamp();
                }
                _eventArgs.Push(args);
            }
        }
    }
}