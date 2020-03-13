using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers {
    public class PackedDescription {
        private readonly string _id;
        private readonly string _name;
        private readonly Dictionary<string, string> _attributes;
        private readonly string _installTo;
        private readonly bool _cmSupportsContent;

        public string FolderToMove { get; set; }

        public PackedDescription(string id, string name, [CanBeNull] Dictionary<string, string> attributes, string installTo, bool cmSupportsContent) {
            _id = id;
            FolderToMove = id;

            _name = name;
            _attributes = attributes ?? new Dictionary<string, string>(0);
            _installTo = installTo;
            _cmSupportsContent = cmSupportsContent;
        }

        public static string ToString([ItemCanBeNull] IEnumerable<PackedDescription> descriptions) {
            var list = descriptions.NonNull().ToList();
            if (list.Count == 0) return "";

            var sb = new StringBuilder();

            sb.Append(list.Select(x => x._name).JoinToReadableString());
            sb.Append("\n\n");
            var properties = list.SelectMany(x => x._attributes)
                    .GroupBy(x => x.Key)
                    .Where(x => x.Select(y => y.Value).AreIdentical())
                    .Select(x => new {
                        x.Key,
                        Value = x.Select(y => y.Value).FirstOrDefault()
                    }).Where(x => !string.IsNullOrWhiteSpace(x.Value)).ToList();

            properties.Add(new {
                Key = "Packed at",
                Value = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss \"GMT\"zzz")
            });

            if (properties.Count > 0) {
                var maxLength = properties.MaxEntry(x => x.Key.Length).Key.Length;
                foreach (var prop in properties) {
                    sb.Append($"  {prop.Key}:{" ".RepeatString(2 + maxLength - prop.Key.Length)}{prop.Value}\n");
                }

                sb.Append('\n');
            }

            var cmPostfix = SettingsHolder.Content.MentionCmInPackedContent && list[0]._cmSupportsContent
                    ? " or simply drag'n'drop archive to Content Manager." : ".";
            sb.Append($@"To install, move folders to ""...\SteamApps\common\assettocorsa""{cmPostfix}");

            /*
            var installTo = list[0]._installTo;
            if (installTo.StartsWith(AcRootDirectory.Instance.Value ?? "-")) {
                installTo = installTo.Replace(AcRootDirectory.Instance.Value ?? "-", "...\\SteamApps\\common\\assettocorsa");
            } else {
                installTo = installTo.Replace(FileUtils.GetDocumentsDirectory(), "...\\Documents\\Assetto Ccorsa");
            }

            var foldersToMove = list.Select(x => x.FolderToMove).Distinct().OrderBy(x => x).ToList();
            if (foldersToMove.Count == 1) {
                sb.Append($"To install, move folder \"{foldersToMove[0]}\" to \"{installTo}\"{cmPostfix}");
            } else {
                sb.Append($"To install, move folders {foldersToMove.Select(x => $"\"{x}\"").JoinToReadableString()} to \"{installTo}\"{cmPostfix}");
            }*/

            return sb.ToString().WordWrap(80);
        }
    }
}