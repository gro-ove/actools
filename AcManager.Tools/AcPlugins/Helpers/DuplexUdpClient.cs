using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace AcManager.Tools.AcPlugins.Helpers {
    public class DuplexUdpClient {
        private UdpClient _plugin;

        private readonly Queue<TimestampedBytes> _messageQueue = new Queue<TimestampedBytes>();
        private readonly Queue<TimestampedBytes> _sendMessageQueue = new Queue<TimestampedBytes>();

        public delegate void MessageReceivedDelegate(TimestampedBytes data);

        public delegate void ErrorHandlerDelegate(Exception ex);

        private MessageReceivedDelegate _messageReceived;
        private ErrorHandlerDelegate _errorHandler;
        private IPEndPoint _remoteIpEndPoint = null;
        private Thread _processMessagesThread;
        private Thread _receiveMessagesThread;
        private Thread _sendMessagesThread;

        public int MinWaitMsBetweenSend { get; set; }

        public bool Opened { get; private set; }

        public void Open(int listeningPort, string remoteHostname, int remotePort, MessageReceivedDelegate callback, ErrorHandlerDelegate errorHandler,
                int minWaitMsBetweenSend = 20) {
            if (_plugin != null) {
                throw new Exception("UdpServer was already started.");
            }

            MinWaitMsBetweenSend = minWaitMsBetweenSend;
            _messageReceived = callback;
            _errorHandler = errorHandler;

            _plugin = new UdpClient(listeningPort);
            _plugin.Connect(remoteHostname, remotePort);
            _remoteIpEndPoint = new IPEndPoint(IPAddress.Any, remotePort);
            Opened = true;

            _processMessagesThread = new Thread(ProcessMessages) { Name = "ProcessMessages", IsBackground = true };
            _processMessagesThread.Start();

            _receiveMessagesThread = new Thread(ReceiveMessages) { Name = "ReceiveMessages", IsBackground = true  };
            _receiveMessagesThread.Start();

            _sendMessagesThread = new Thread(SendMessages) { Name = "SendMessages", IsBackground = true  };
            _sendMessagesThread.Start();
        }

        public void Close() {
            if (_plugin != null) {
                lock (_sendMessageQueue) {
                    lock (_messageQueue) {
                        Opened = false;
                        Monitor.Pulse(_messageQueue); // if the ProcessMessages thread is waiting, wake it up
                        Monitor.Pulse(_sendMessageQueue); // if the SendMessages thread is waiting, wake it up
                    }
                }

                _plugin.Close(); // _plugin.Receive in ReceiveMessages thread should return at this point

                if (Thread.CurrentThread != _processMessagesThread) {
                    if (!_processMessagesThread.Join(1000)) {
                        _processMessagesThread.Abort(); // make sure thread has terminated
                    }
                }

                if (!_receiveMessagesThread.Join(1000)) {
                    _receiveMessagesThread.Abort(); // make sure thread has terminated
                }

                if (!_sendMessagesThread.Join(1000)) {
                    _sendMessagesThread.Abort(); // make sure thread has terminated
                }

                _plugin = null;
                _messageQueue.Clear();
                _sendMessageQueue.Clear();
            }
        }

        private void ProcessMessages() {
            while (Opened) {
                TimestampedBytes msgData;
                lock (_messageQueue) {
                    if (_messageQueue.Count == 0) {
                        if (!Opened) break; // don't start waiting and exit loop if closed
                        Monitor.Wait(_messageQueue);
                        if (!Opened) break; // exit loop if closed
                    }

                    msgData = _messageQueue.Dequeue();
                }

                try {
                    _messageReceived(msgData);
                } catch (Exception ex) {
                    _errorHandler(ex);
                }
            }
        }

        private void ReceiveMessages() {
            while (Opened) {
                try {
                    var bytesReceived = _plugin.Receive(ref _remoteIpEndPoint);
                    var tsb = new TimestampedBytes(bytesReceived);
                    lock (_messageQueue) {
                        _messageQueue.Enqueue(tsb);
                        Monitor.Pulse(_messageQueue);
                    }
                } catch (Exception ex) {
                    if (Opened) {
                        // it seems the acServer is not running/ready yet
                        _errorHandler(ex);
                    }
                }
            }
        }

        private void SendMessages() {
            while (Opened) {
                byte[] msgData;
                lock (_sendMessageQueue) {
                    if (_sendMessageQueue.Count == 0) {
                        if (!Opened) break; // don't start waiting and exit loop if closed
                        Monitor.Wait(_sendMessageQueue);
                        if (!Opened) break; // exit loop if closed
                    }

                    msgData = _sendMessageQueue.Dequeue().RawData;
                }

                try {
                    _plugin.Send(msgData, msgData.Length);
                    if (MinWaitMsBetweenSend > 0) {
                        Thread.Sleep(MinWaitMsBetweenSend);
                    }
                } catch (Exception ex) {
                    _errorHandler(ex);
                }
            }
        }

        public void Send(TimestampedBytes dgram) {
            if (_plugin == null)
                throw new Exception("TrySend: UdpClient missing, please open first");

            lock (_sendMessageQueue) {
                _sendMessageQueue.Enqueue(dgram);
                Monitor.Pulse(_sendMessageQueue);
            }
        }

        public bool TrySend(TimestampedBytes dgram) {
            try {
                Send(dgram);
                return true; // we don't really know if it worked
            } catch (Exception ex) {
                _errorHandler(ex);
                return false;
            }
        }
    }
}