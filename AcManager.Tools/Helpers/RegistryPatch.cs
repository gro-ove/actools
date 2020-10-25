using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using Newtonsoft.Json;

namespace AcManager.Tools.Helpers {
    [Localizable(false)]
    public class RegistryPatch : Dictionary<string, Dictionary<string, object>> {
        public new Dictionary<string, object> this[string key] {
            get {
                if (TryGetValue(key, out var result)) return result;
                result = new Dictionary<string, object>();
                this[key] = result;
                return result;
            }
            set => base[key] = value;
        }

        private static string ToRegValue(object v) {
            switch (v) {
                case bool b:
                    return b ? @"dword:00000001" : @"dword:00000000";
                case string s:
                    return JsonConvert.SerializeObject(s);
                case IEnumerable<string> s:
                    var data = new StringBuilder();
                    var commasLeft = 18;
                    data.Append(@"hex(7):");

                    void AddValue(int b) {
                        data.Append($@"{b:x2},");
                        if (--commasLeft == 0) {
                            data.Append("\\\r\n  ");
                            commasLeft = 25;
                        }
                    }

                    foreach (var i in s) {
                        foreach (var b in Encoding.Unicode.GetBytes(i)) {
                            AddValue(b);
                        }
                        AddValue(0);
                        AddValue(0);
                    }
                    AddValue(0);
                    data.Append(@"00");
                    return data.ToString();
                case null:
                    return @"-";
                default:
                    return JsonConvert.SerializeObject(v.ToString());
            }
        }

        private static string ToCommentValue(object v) {
            switch (v) {
                case IEnumerable<string> s:
                    return string.Join(", ", s).WordWrap(80).Trim();
                default:
                    return null;
            }
        }

        public async Task<bool> ApplyAsync(string title, string message, string fileName = "Changes.reg") {
            var response = ActionExtension.InvokeInMainThread(() => MessageDialog.Show(message, title,
                    new MessageDialogButton(MessageBoxButton.YesNoCancel, MessageBoxResult.Yes) {
                        [MessageBoxResult.Yes] = "Apply changes automatically",
                        [MessageBoxResult.No] = "Prepare .reg-file only"
                    }));

            if (response == MessageBoxResult.Cancel) {
                return false;
            }

            var data = new StringBuilder();
            data.Append(@"Windows Registry Editor Version 5.00").Append(Environment.NewLine).Append(Environment.NewLine);
            foreach (var pair in this) {
                data.Append($@"[{pair.Key}]").Append(Environment.NewLine);
                foreach (var p in pair.Value) {
                    var commentValue = ToCommentValue(p.Value);
                    if (commentValue != null) {
                        data.Append("; ").Append(commentValue.Replace("\n", "\n; ")).Append(Environment.NewLine);
                    }

                    // ReSharper disable once MethodHasAsyncOverload
                    data.Append(JsonConvert.SerializeObject(p.Key)).Append(@"=").Append(ToRegValue(p.Value)).Append(Environment.NewLine);
                }
                data.Append(Environment.NewLine);
            }

            var filename = FilesStorage.Instance.GetTemporaryFilename("RunElevated", fileName);
            File.WriteAllText(filename, data.ToString());

            if (response == MessageBoxResult.No) {
                WindowsHelper.ViewFile(filename);
                return true;
            }

            try {
                var procRegEdit = ProcessExtension.Start("explorer.exe", new[] { filename });
                await procRegEdit.WaitForExitAsync().ConfigureAwait(false);
                Logging.Debug("Done: " + procRegEdit.ExitCode);
                return procRegEdit.ExitCode == 0;
            } catch (Win32Exception ex) when (ex.ErrorCode != -1) {
                Logging.Debug(ex.ErrorCode);
                throw new InformativeException("Access denied", "Administrator privilegies are required to modify these registry values.");
            }
        }
    }
}