using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcManager.Tools.SharedMemory;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Unosquare.Labs.EmbedIO;
using Unosquare.Labs.EmbedIO.Log;
using Unosquare.Labs.EmbedIO.Modules;

namespace AcManager.Tools.Profile {
    public class RaceWebServer : IDisposable, ILog {
        private WebServer _server;
        private PublishDataSocketsServer _statsServer, _sharedServer;

        public void Start(int port, [CanBeNull] string filename) {
#if DEBUG
            var log = this;
#else
            var log = new NullLog();
#endif
            _server = new WebServer($"http://+:{port}/", log, RoutingStrategy.Wildcard);

            // HTTP part: index webpage, static files nearby (if any)
            var pages = new PagesProvider(filename);

            _server.RegisterModule(new WebApiModule());
            _server.Module<WebApiModule>().RegisterController(() => new IndexPageController(pages));

            if (Directory.Exists(pages.StaticDirectory)) {
                _server.RegisterModule(new StaticFilesModule(new Dictionary<string, string> {
                    [@"/"] = pages.StaticDirectory
                }) {
                    UseRamCache = false,
                    UseGzip = false
                });
            }

            // Websockets part
            var module = new WebSocketsModule();
            _server.RegisterModule(module);

            _sharedServer = new PublishDataSocketsServer(@"Current Race Shared Memory Server");
            module.RegisterWebSocketsServer(@"/api/ws/shared", _sharedServer);

            _statsServer = new PublishDataSocketsServer(@"Current Race Stats Server");
            module.RegisterWebSocketsServer(@"/api/ws/stats", _statsServer);

            // Starting
            try {
                _server.RunAsync();
            } catch (HttpListenerException e) when (e.ToString().Contains("0x80004005")) {
                NonfatalError.NotifyBackground("Can’t start web server",
                        $"Don’t forget to allow port’s usage with something like “netsh http add urlacl url=\"http://+:{port}/\" user=everyone”.", e, new[] {
                            new NonfatalErrorSolution($"Use “netsh” to allow usage of port {port}", null, async token => {
                                try {
                                    var command = $"netsh http add urlacl url=\"http://+:{port}/\" user=everyone";
                                    var proc = ProcessExtension.Start("cmd", new[] {
                                        "/C", $"echo {command} & {command} & @pause"
                                    }, new ProcessStartInfo { Verb = "runas" });
                                    await proc.WaitForExitAsync(token).ConfigureAwait(false);
                                    Logging.Debug("Done: " + proc.ExitCode);
                                } catch (Win32Exception ex) when (ex.ErrorCode != -1) {
                                    Logging.Debug(ex.ErrorCode);
                                    throw new InformativeException("Access denied",
                                            "Unfortunately, HTTP.sys driver doesn’t allow to assign ports without administrator privileges.");
                                }
                            })
                        });
            } catch (Exception e) {
                Logging.Error(e);
            }
        }

        public void PublishStatsData(object data) {
            _statsServer.PublishData(data);
        }

        public void PublishSharedData(object data) {
            _sharedServer.PublishData(data);
        }

        public class PagesProvider {
            [CanBeNull]
            private readonly FileInfo _fileInfo;

            [CanBeNull]
            public string StaticDirectory { get; }

            public PagesProvider(string indexPageFilename) {
                if (indexPageFilename != null) {
                    if (Directory.Exists(indexPageFilename)) {
                        _fileInfo = new FileInfo(Path.Combine(indexPageFilename, "index.html"));
                    } else if (File.Exists(indexPageFilename)) {
                        _fileInfo = new FileInfo(indexPageFilename);
                    } else {
                        var local = FilesStorage.Instance.GetFilename(indexPageFilename);
                        if (Directory.Exists(local)) {
                            _fileInfo = new FileInfo(Path.Combine(local, "index.html"));
                        } else if (File.Exists(local)) {
                            _fileInfo = new FileInfo(local);
                        }
                    }

                    StaticDirectory = _fileInfo?.DirectoryName;
                }
            }

            private byte[] _cache;
            private DateTime? _lastLoaded;

            internal byte[] GetIndexPage() {
                if (_fileInfo != null) {
                    _fileInfo.Refresh();

                    var current = _fileInfo.Exists ? _fileInfo.LastWriteTime : (DateTime?)null;
                    if (current != _lastLoaded) {
                        _lastLoaded = current;
                        _cache = _fileInfo.Exists ? File.ReadAllBytes(_fileInfo.FullName) : null;
                    }
                }

                return _cache ?? (_cache = Encoding.UTF8.GetBytes(@"<pre></pre><script>
var pre = document.querySelector('pre');
new WebSocket('ws://' + location.host + '/api/shared').onmessage = function(e){{ pre.textContent = JSON.stringify(JSON.parse(e.data), null, 4) }};
</script>"));
            }
        }

        public class IndexPageController : WebApiController {
            private PagesProvider _pages;

            public IndexPageController(PagesProvider pages) {
                _pages = pages;
            }

            [WebApiHandler(HttpVerbs.Get, @"/"), UsedImplicitly]
            public bool GetIndex(WebServer server, HttpListenerContext context) {
                try {
                    var buffer = _pages.GetIndexPage();
                    context.Response.ContentType = "text/html; charset=utf-8";
                    context.Response.OutputStream.Write(buffer, 0, buffer.Length);

                    return true;
                } catch (Exception ex) {
                    return HandleError(context, ex);
                }
            }

            [WebApiHandler(HttpVerbs.Get, @"/api/car/information"), UsedImplicitly]
            public bool GetCarInformation(WebServer server, HttpListenerContext context) {
                try {
                    var carId = AcSharedMemory.Instance.Shared?.StaticInfo.CarModel;
                    var car = carId == null ? null : CarsManager.Instance.GetById(carId);

                    if (car != null) {
                        if (File.Exists(car.JsonFilename)) {
                            var buffer = File.ReadAllBytes(car.JsonFilename);
                            context.Response.ContentType = "application/json";
                            context.Response.OutputStream.Write(buffer, 0, buffer.Length);
                        } else {
                            context.Response.StatusCode = 404;
                        }
                    } else {
                        context.Response.StatusCode = 404;
                    }

                    return true;
                } catch (Exception ex) {
                    return HandleError(context, ex);
                }
            }

            [WebApiHandler(HttpVerbs.Get, @"/api/track/information"), UsedImplicitly]
            public bool GetTrackInformation(WebServer server, HttpListenerContext context) {
                try {
                    var info = AcSharedMemory.Instance.Shared?.StaticInfo;
                    var trackId = info?.Track;
                    TrackObjectBase track = trackId == null ? null : TracksManager.Instance.GetById(trackId);
                    if (track != null && !string.IsNullOrEmpty(info.TrackConfiguration)) {
                        track = ((TrackObject)track).GetLayoutByLayoutId(info.TrackConfiguration);
                    }


                    if (track != null) {
                        if (File.Exists(track.JsonFilename)) {
                            var buffer = File.ReadAllBytes(track.JsonFilename);
                            context.Response.ContentType = "application/json";
                            context.Response.OutputStream.Write(buffer, 0, buffer.Length);
                        } else {
                            context.Response.StatusCode = 404;
                        }
                    } else {
                        context.Response.StatusCode = 404;
                    }

                    return true;
                } catch (Exception ex) {
                    return HandleError(context, ex);
                }
            }

            [WebApiHandler(HttpVerbs.Get, @"/api/car/badge"), UsedImplicitly]
            public bool GetCarBadge(WebServer server, HttpListenerContext context) {
                try {
                    var currentCar = AcSharedMemory.Instance.Shared?.StaticInfo.CarModel;
                    var car = currentCar == null ? null : CarsManager.Instance.GetById(currentCar);

                    if (car != null) {
                        if (File.Exists(car.BrandBadge)) {
                            var buffer = File.ReadAllBytes(car.BrandBadge);
                            context.Response.ContentType = "image/png";
                            context.Response.OutputStream.Write(buffer, 0, buffer.Length);
                        } else {
                            context.Response.StatusCode = 404;
                        }
                    } else {
                        context.Response.StatusCode = 404;
                    }

                    return true;
                } catch (Exception ex) {
                    return HandleError(context, ex);
                }
            }

            protected bool HandleError(HttpListenerContext context, Exception ex, int statusCode = 500) {
                context.Response.StatusCode = statusCode;
                context.Response.ContentType = "text/html; charset=utf-8";
                var buffer = Encoding.UTF8.GetBytes(string.Format("<html><head><title>{0}</title></head><body><h1>{0}</h1><hr><pre>{1}</pre></body></html>",
                        "Unexpected Error", ex));
                context.Response.OutputStream.Write(buffer, 0, buffer.Length);
                return true;
            }
        }

        public class PublishDataSocketsServer : WebSocketsServer {
            private int _connected;
            private string _data;
            private object _dataRef;

            public PublishDataSocketsServer(string name) : base(true, 0) {
                ServerName = name;
            }

            private static string Serialize(object data) {
                return data is IJsonSerializable jsonSerializable ? jsonSerializable.ToJson()
                        : JsonConvert.SerializeObject(data, Formatting.None);
            }

            public void PublishData(object data) {
                if (_connected > 0) {
                    _data = Serialize(data);
                    Broadcast(_data);
                } else {
                    _dataRef = data;
                }
            }

            protected override void OnMessageReceived(WebSocketContext context, byte[] rxBuffer, WebSocketReceiveResult rxResult) {}

            public override string ServerName { get; }

            protected override void OnClientConnected(WebSocketContext context) {
                if (_data != null) {
                    Send(context, _data);
                } else if (_dataRef != null) {
                    _data = Serialize(_dataRef);
                    Send(context, _data);
                    _dataRef = null;
                } else {
                    Send(context, @"null");
                }

                _connected++;
            }

            protected override void OnFrameReceived(WebSocketContext context, byte[] rxBuffer, WebSocketReceiveResult rxResult) {}

            protected override void OnClientDisconnected(WebSocketContext context) {
                _connected--;
            }
        }

        public void Dispose() {
            DisposeHelper.Dispose(ref _server);
        }

        public void Info(object message) {
            Logging.Write(message);
        }

        public void Error(object message) {
            Logging.Error(message);
        }

        public void Error(object message, Exception exception) {
            Logging.Error($"{message}; {exception}");
        }

        public void InfoFormat(string format, params object[] args) {
            Logging.Write(string.Format(format, args));
        }

        public void WarnFormat(string format, params object[] args) {
            Logging.Warning(string.Format(format, args));
        }

        public void ErrorFormat(string format, params object[] args) {
            Logging.Error(string.Format(format, args));
        }

        public void DebugFormat(string format, params object[] args) {
            Logging.Debug(string.Format(format, args));
        }
    }
}