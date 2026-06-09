using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace AcManager.Tools.Helpers {
    public static class WebExceptionExtensions {
        public static string GetResponseBody(this WebException webException) {
            if (webException.Status == WebExceptionStatus.ProtocolError) {
                try {
                    using (var stream = webException.Response.GetResponseStream()) {
                        if (stream is null) return string.Empty; 
                        using (var reader = new StreamReader(stream)) {
                            string msg = reader.ReadToEnd();
                            if (string.IsNullOrEmpty(msg) && webException.Response is HttpWebResponse response) {
                                msg = $"{response.StatusDescription} ({(int)response.StatusCode})";
                            }

                            return msg;
                        }
                    }
                } catch (WebException) {
                    return string.Empty;
                }
            }
            return string.Empty;
        }

        public static async Task<string> GetResponseBodyAsync(this WebException webException) {
            if (webException.Status == WebExceptionStatus.ProtocolError) {
                try {
                    using (var stream = webException.Response.GetResponseStream()) {
                        if (stream is null) return string.Empty;
                        using (var reader = new StreamReader(stream)) {
                            var msg = await reader.ReadToEndAsync();
                            if (string.IsNullOrEmpty(msg) && webException.Response is HttpWebResponse response) {
                                msg = $"{response.StatusDescription} ((int){response.StatusCode})";
                            }

                            return msg;
                        }
                    }
                } catch (WebException) {
                    return string.Empty;
                }
            }
            return string.Empty;
        }
    }
}