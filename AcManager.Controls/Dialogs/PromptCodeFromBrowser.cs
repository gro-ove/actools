using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using AcManager.Tools.Helpers;
using AcTools.Windows;
using JetBrains.Annotations;

namespace AcManager.Controls.Dialogs {
    public class PromptCodeFromBrowser {
        /// <summary>
        /// Hard to describe, but that thing will show a prompt, but also load a web-page and wait for a response code which will
        /// appear in page title. This weird system isn’t actually that weird, Google uses it.
        /// </summary>
        /// <param name="url">Page which will be loaded</param>
        /// <param name="titleMatch">Regular expression for title with result; result should be in first group.</param>
        /// <param name="description">Some description, could ends with “:”.</param>
        /// <param name="title">Title, in title casing</param>
        /// <param name="watermark">Some semi-transparent hint in the input area</param>
        /// <param name="toolTip">Tooltip for the input area</param>
        /// <param name="multiline">Is the input area should be multilined.</param>
        /// <param name="passwordMode">Hide inputting value.</param>
        /// <param name="maxLength">Length limitation.</param>
        /// <param name="suggestions">Suggestions if needed.</param>
        /// <param name="window">Window for activation event capturing.</param>
        /// <param name="cancellation">Cancellation token.</param>
        /// <returns>Result string or null if user cancelled input.</returns>
        [ItemCanBeNull]
        public static async Task<string> Show([Localizable(false)] string url, Regex titleMatch,
                string description, string title, string watermark = null, string toolTip = null,
                bool multiline = false, bool passwordMode = false, int maxLength = -1, IEnumerable<string> suggestions = null, Window window = null,
                CancellationToken cancellation = default(CancellationToken)) {
            if (window == null) {
                window = Application.Current?.Windows.OfType<Window>().SingleOrDefault(x => x.IsActive) ?? Application.Current?.MainWindow;
                if (window == null) return null;
            }

            WindowsHelper.ViewInBrowser(url);

            string code = null;
            var waiting = true;
            var ready = false;

            EventHandler handler = async (sender, args) => {
                if (!waiting) return;
                waiting = false;

                // ReSharper disable once AccessToModifiedClosure
                code = await Prompt.ShowAsync(title, description, code, watermark, toolTip, multiline, passwordMode, false, maxLength, suggestions, cancellation);
                ready = true;
            };

            window.Activated += handler;
            for (var i = 0; i < 500 && waiting; i++) {
                if (i > 0 && window.IsFocused) {
                    handler(null, null);
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
                    window.Activated -= handler;
                    return null;
                }
            }

            window.Activated -= handler;

            for (var i = 0; i < 500 && !waiting && !ready; i++) {
                await Task.Delay(200, cancellation);
                if (cancellation.IsCancellationRequested) return null;
            }

            return string.IsNullOrWhiteSpace(code) ? null : code.Trim();
        }

        /// <summary>Returns a dictionary that contains the handle and title of all the open windows.</summary>
        /// <returns>A dictionary that contains the handle and title of all the open windows.</returns>
        public static IDictionary<IntPtr, string> GetOpenWindows() {
            var shellWindow = User32.GetShellWindow();
            var windows = new Dictionary<IntPtr, string>();

            User32.EnumWindows(delegate (IntPtr hWnd, int lParam) {
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
}