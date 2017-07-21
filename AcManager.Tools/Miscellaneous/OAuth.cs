using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using AcManager.Tools.Helpers;
using AcTools.Windows;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using Unosquare.Labs.EmbedIO;
using Unosquare.Labs.EmbedIO.Log;
using Unosquare.Labs.EmbedIO.Modules;

namespace AcManager.Tools.Miscellaneous {
    public class OAuthCode {
        public OAuthCode(bool manualMode, string code, string redirectUri) {
            ManualMode = manualMode;
            Code = code;
            RedirectUri = redirectUri;
        }

        public bool ManualMode { get; }

        [CanBeNull]
        public string Code { get; }

        [CanBeNull]
        public string RedirectUri { get; }
    }

    public static class OAuth {
        private const string SubUrl = "Temporary_Listen_Addresses/Cm_Auth";

        [ItemNotNull]
        public static Task<OAuthCode> GetCode(
                string name, string requestUrl, string noRedirectUrl,
                string successCodeRegex = @"Success code=(\S+)", string redirectUrlKey = "redirect_uri",
                string responseError = "error", string responseCode = "code",
                string description = null, string title = null,
                CancellationToken cancellation = default(CancellationToken)) {
            var tcs = new TaskCompletionSource<OAuthCode>();

            // Try to use web server and localhost to redirect
            WebServer server = null;
            var redirectUri = "http://localhost/" + SubUrl;

            async void DisposeLater() {
                await Task.Delay(2000);

                Logging.Debug("Stopping server…");
                // ReSharper disable once AccessToModifiedClosure
                server?.Dispose();
                server = null;
            }

            server = new WebServer("http://+:80/" + SubUrl, new Log(e => {
                tcs.TrySetException(e);
                DisposeLater();
            }), Unosquare.Labs.EmbedIO.RoutingStrategy.Wildcard);

            if (server != null) {
                server.RegisterModule(new WebApiModule());
                server.Module<WebApiModule>().RegisterController(() => new IndexPageController(responseError, responseCode, (e, s) => {
                    if (e != null || s == null) {
                        tcs.TrySetException(new Exception(e == null ? "Code is missing" : "Authentication went wrong: " + e));
                    } else {
                        tcs.TrySetResult(new OAuthCode(false, s, redirectUri));
                    }
                    DisposeLater();
                }));

                cancellation.Register(() => {
                    tcs.TrySetCanceled();
                    DisposeLater();
                });

                try {
                    Logging.Debug($"Launching web-server on {SubUrl}…");
                    server.RunAsync();

                    var url = requestUrl + $"&{redirectUrlKey}={Uri.EscapeDataString(redirectUri)}";
#if DEBUG
                    Logging.Debug(url);
#endif
                    WindowsHelper.ViewInBrowser(url);
                    return tcs.Task;
                } catch (Exception e) {
                    Logging.Warning(e);
                }
            }

            DisposeLater();

            description = description ?? ToolsStrings.Uploader_EnterGoogleDriveAuthenticationCode.Replace("Google Drive", name);
            title = title ?? ToolsStrings.Uploader_GoogleDrive.Replace("Google Drive", name);

            return PromptCodeFromBrowser.Show(noRedirectUrl != null ?
                    requestUrl + $"&{redirectUrlKey}={Uri.EscapeDataString(noRedirectUrl)}" :
                    requestUrl, new Regex(successCodeRegex, RegexOptions.Compiled),
                    description, title, cancellation: cancellation).ContinueWith(x => new OAuthCode(true, x.Result, noRedirectUrl));
        }

        private static class PromptCodeFromBrowser {
            public static async Task<string> Show([Localizable(false)] string url, Regex titleMatch,
                    string description, string title, string watermark = null, string toolTip = null,
                    int maxLength = -1, Window window = null,
                    CancellationToken cancellation = default(CancellationToken)) {
                if (window == null) {
                    window = Application.Current?.Windows.OfType<Window>().SingleOrDefault(x => x.IsActive) ?? Application.Current?.MainWindow;
                    if (window == null) {
                        Logging.Warning("Main window is missing");
                        return null;
                    }
                }

                WindowsHelper.ViewInBrowser(url);

                string code = null;
                var waiting = true;
                var ready = false;

                async void Handler(object sender, EventArgs args) {
                    if (!waiting) return;
                    waiting = false;

                    // ReSharper disable once AccessToModifiedClosure
                    code = await Prompt.ShowAsync(title, description, code, watermark, toolTip, false, false, false, maxLength, null, false, cancellation);
                    ready = true;
                }

                window.Activated += Handler;
                for (var i = 0; i < 500 && waiting; i++) {
                    if (i > 0 && window.IsFocused) {
                        Handler(null, null);
                        break;
                    }

                    if (code == null) {
                        code = GetOpenWindows()
                                .Select(x => titleMatch.Match(x.Value))
                                .FirstOrDefault(x => x.Success)?
                                .Groups.OfType<Group>().ElementAtOrDefault(1)?
                                .Value.Trim();
                    }

                    await Task.Delay(200, cancellation);
                    if (cancellation.IsCancellationRequested) {
                        window.Activated -= Handler;
                        return null;
                    }
                }

                window.Activated -= Handler;

                for (var i = 0; i < 500 && !waiting && !ready; i++) {
                    await Task.Delay(200, cancellation);
                    if (cancellation.IsCancellationRequested) return null;
                }

                return string.IsNullOrWhiteSpace(code) ? null : code.Trim();
            }

            /// <summary>Returns a dictionary that contains the handle and title of all the open windows.</summary>
            /// <returns>A dictionary that contains the handle and title of all the open windows.</returns>
            private static IDictionary<IntPtr, string> GetOpenWindows() {
                var shellWindow = User32.GetShellWindow();
                var windows = new Dictionary<IntPtr, string>();

                User32.EnumWindows(delegate(IntPtr hWnd, int lParam) {
                    if (hWnd == shellWindow) return true;
                    if (!User32.IsWindowVisible(hWnd)) return true;

                    var length = User32.GetWindowTextLength(hWnd);
                    if (length == 0) return true;

                    var builder = new StringBuilder(length);
                    User32.GetWindowText(hWnd, builder, length + 1);

                    windows[hWnd] = builder.ToString();
                    return true;
                }, 0);

                return windows;
            }
        }

        private class Log : ILog {
            private Action<Exception> _errorCallback;

            public Log(Action<Exception> errorCallback) {
                _errorCallback = errorCallback;
            }

            public void Error(object message) {
                var exception = message as Exception;
                if (exception != null) {
                    _errorCallback(exception);
                }
            }

            public void Error(object message, Exception exception) {
                _errorCallback(exception);
            }

            public void Info(object message) { }
            public void InfoFormat(string format, params object[] args) { }
            public void WarnFormat(string format, params object[] args) { }
            public void ErrorFormat(string format, params object[] args) { }
            public void DebugFormat(string format, params object[] args) { }
        }

        private class IndexPageController : WebApiController {
            private readonly string _errorKey, _codeKey;
            private readonly Action<string, string> _callback;

            public IndexPageController(string errorKey, string codeKey, Action<string, string> callback) {
                _errorKey = errorKey;
                _codeKey = codeKey;
                _callback = callback;
            }

            [WebApiHandler(HttpVerbs.Get, @"/Temporary_Listen_Addresses/*")]
            public bool GetIndex(WebServer server, HttpListenerContext context) {
                var error = context.Request.QueryString[_errorKey];
                var code = context.Request.QueryString[_codeKey];
                var success = error == null && code != null;
                _callback(error, code);

                try {
                    var buffer =
                            Encoding.UTF8.GetBytes(string.Format(@"<!DOCTYPE html><html><head><title>Content Manager</title><base href=""http://acstuff.ru/"">
<meta http-equiv=""content-type"" content=""text/html; charset=UTF-8""><link rel=""stylesheet"" href=""/s/style.css"" />
<link rel=""shortcut icon"" type=""image/x-icon"" href=""/app/icon.ico"" sizes=""16x16"" />
<link rel=""icon"" type=""image/x-icon"" href=""/app/icon_48.png"" sizes=""16x16"" />
<link rel=""apple-touch-icon-precomposed"" href=""/app/icon_96.png"" /></head><body><div class=""background""></div>
<div class=""header""><a href=""/""><img src=""/app/icon_48.png"">Content Manager</a></div><div class=""body_main"">
<h1>{0}</h1><p>{1}</p></div></body></html>",
                                    success ? "Authorized" : "Failed to authorize",
                                    success
                                            ? "Authorized! Now, please, close this page and go back to  CM."
                                            : "Authorization failed. Please, close this page and go back to CM."));
                    context.Response.ContentType = "text/html; charset=utf-8";
                    context.Response.OutputStream.Write(buffer, 0, buffer.Length);
                    return true;
                } catch (Exception ex) {
                    return HandleError(context, ex);
                }
            }

            private bool HandleError(HttpListenerContext context, Exception ex, int statusCode = 500) {
                context.Response.StatusCode = statusCode;
                context.Response.ContentType = "text/html; charset=utf-8";
                var buffer =
                        Encoding.UTF8.GetBytes(string.Format("<html><head><title>{0}</title></head><body><h1>{0}</h1><hr><pre>{1}</pre></body></html>",
                                "Unexpected Error", ex));
                context.Response.OutputStream.Write(buffer, 0, buffer.Length);
                return true;
            }
        }
    }
}