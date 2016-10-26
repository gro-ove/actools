using System;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using Newtonsoft.Json;
using Unosquare.Labs.EmbedIO;
using Unosquare.Labs.EmbedIO.Log;
using Unosquare.Labs.EmbedIO.Modules;

namespace AcManager.Tools.Profile {
    public class PlayerStatsWebServer : IDisposable, ILog {
        private WebServer _server;
        private PublishDataSocketsServer _currentServer;

        public void Start(int port) {
            _server = new WebServer($"http://+:{port}/", new NullLog(), RoutingStrategy.Wildcard);
            _server.RegisterModule(new WebApiModule());
            _server.Module<WebApiModule>().RegisterController<PlayerStatsController>();

            var module = new WebSocketsModule();
            _server.RegisterModule(module);
            _currentServer = new PublishDataSocketsServer(@"Current Race Stats Server");
            module.RegisterWebSocketsServer("/api/current", _currentServer);

            try {
                _server.RunAsync();
            } catch (HttpListenerException e) {
                Logging.Warning(e.Message + $"\nDon’t forget to reserve url using something like “netsh http add urlacl url=\"http://+:{port}/\" user=everyone”.");
            } catch (Exception e) {
                Logging.Error(e);
            }
        }

        public void PublishNewCurrentData(object data) {
            _currentServer.PublishData(data);
        }

        private static string PeriodicUpdate(string path) {
            return $@"<pre></pre><script>
var pre = document.querySelector('pre');
function update(){{
    var x = new XMLHttpRequest();
    x.onreadystatechange = function() {{ 
            if (this.readyState == XMLHttpRequest.DONE) pre.textContent = this.responseText }}
    x.open('GET', '{path}', true);
    x.send(null);
}}
update();
setInterval(update, 50);
</script>";
        }

        private static string WebsocketsUpdate(string path) {
            return $@"<pre></pre><script>
var pre = document.querySelector('pre');
new WebSocket('ws://' + location.host + '{path}').onmessage = function(e){{ pre.textContent = JSON.stringify(JSON.parse(e.data), null, 4) }};
</script>";
        }

        public class PlayerStatsController : WebApiController {
            [WebApiHandler(HttpVerbs.Get, @"/")]
            public bool GetIndex(WebServer server, HttpListenerContext context) {
                try {
                    var buffer = Encoding.UTF8.GetBytes(WebsocketsUpdate("/api/current"));
                    context.Response.ContentType = "text/html";
                    context.Response.OutputStream.Write(buffer, 0, buffer.Length);

                    return true;
                } catch (Exception ex) {
                    return HandleError(context, ex);
                }
            }

            protected bool HandleError(HttpListenerContext context, Exception ex, int statusCode = 500) {
                var errorResponse = new {
                    Title = "Unexpected Error",
                    ErrorCode = ex.GetType().Name,
                    Description = ex.ExceptionMessage(),
                };

                context.Response.StatusCode = statusCode;
                return context.JsonResponse(errorResponse);
            }
        }

        public class PublishDataSocketsServer : WebSocketsServer {
            private string _data;

            public PublishDataSocketsServer(string name) : base(true, 0) {
                ServerName = name;
            }

            public void PublishData(object data) {
                _data = JsonConvert.SerializeObject(data);
                Broadcast(_data);
            }

            protected override void OnMessageReceived(WebSocketContext context, byte[] rxBuffer, WebSocketReceiveResult rxResult) {}
            
            public override string ServerName { get; }

            protected override void OnClientConnected(WebSocketContext context) {
                if (_data != null) {
                    Send(context, _data);
                }
            }
            
            protected override void OnFrameReceived(WebSocketContext context, byte[] rxBuffer, WebSocketReceiveResult rxResult) {}
            
            protected override void OnClientDisconnected(WebSocketContext context) {}
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