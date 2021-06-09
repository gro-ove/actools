using System;
using System.Collections.Specialized;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using HtmlAgilityPack;

namespace AcManager.Tools.Helpers.Loaders {
    internal class ShareModsLoader : DirectLoader {
        public static bool Test(string url) => Regex.IsMatch(url, @"^https?://(?:www\.)?sharemods\.com/\w+/", RegexOptions.IgnoreCase);

        public ShareModsLoader(string url) : base(url) {}

        private HtmlDocument GetDocument(string html) {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            return doc;
        }

        private NameValueCollection GetFormData(string html, string formSelector) {
            var form = GetDocument(html).DocumentNode.SelectSingleNode(formSelector);
            var nv = new NameValueCollection();
            foreach (var x in form.Descendants(@"input")) {
                nv[x.GetAttributeValue(@"name", "")] = x.GetAttributeValue(@"value", "");
            }
            return nv;
        }

        protected override bool? ResumeSupported => false;

        public override async Task<bool> PrepareAsync(CookieAwareWebClient client, CancellationToken cancellation) {
            try {
                var step1 = await client.DownloadStringTaskAsync(Url);
                if (cancellation.IsCancellationRequested) return false;

                var step2 = (await client.UploadValuesTaskAsync(Url, @"POST", GetFormData(step1, @"//div[@id='content']//form[@name='F1']"))).ToUtf8String();
                if (cancellation.IsCancellationRequested) return false;

                Url = GetDocument(step2).DocumentNode.SelectSingleNode(@"//a[contains(@href, 'sharemods.com/cgi-bin')]")?.Attributes[@"href"]?.Value;
                return Url != null;
            } catch (Exception e) {
                Logging.Error(e);
                throw new InformativeException(ToolsStrings.Common_CannotDownloadFile, "Sharemods.com has changed.");
            }
        }
    }
}