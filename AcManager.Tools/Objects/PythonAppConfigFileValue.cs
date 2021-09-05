using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using AcManager.Tools.Managers;
using AcTools.Utils;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Objects {
    public class PythonAppConfigFileValue : PythonAppConfigValue {
        public enum AbsoluteMode {
            Disallow,
            Allow,
            Require
        }

        private readonly AbsoluteMode _allowAbsolute;

        public bool DirectoryMode { get; }

        [CanBeNull]
        public string RelativeTo { get; }

        [CanBeNull]
        public DialogFilterPiece Filter { get; }

        public PythonAppConfigFileValue(bool directoryMode, AbsoluteMode allowAbsolute, [CanBeNull] string filter, [CanBeNull] string relativeTo) {
            _allowAbsolute = allowAbsolute;
            DirectoryMode = directoryMode;
            RelativeTo = relativeTo;

            if (filter == null) {
                Filter = DialogFilterPiece.AllFiles;
            } else {
                var index = filter.IndexOf('|');
                Filter = index == -1 ? new DialogFilterPiece(filter, filter)
                        : new DialogFilterPiece(filter.Substring(0, index), filter.Substring(index + 1));
            }
        }

        private string GetRelativeDirectory() {
            return string.IsNullOrWhiteSpace(RelativeTo) ? FilesRelativeDirectory : ResolvePath(RelativeTo);
        }

        private string ResolvePath(string relativeTo) {
            return Environment.ExpandEnvironmentVariables(Regex.Replace(relativeTo, @"%(\w+?)%", m => {
                if (string.Equals(m.Groups[1].Value, "Documents", StringComparison.OrdinalIgnoreCase)) {
                    return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                }
                if (string.Equals(m.Groups[1].Value, "ACDocuments", StringComparison.OrdinalIgnoreCase)) {
                    return AcPaths.GetDocumentsDirectory();
                }
                if (string.Equals(m.Groups[1].Value, "ACRoot", StringComparison.OrdinalIgnoreCase)) {
                    return AcRootDirectory.Instance.Value;
                }
                if (string.Equals(m.Groups[1].Value, "LocalDir", StringComparison.OrdinalIgnoreCase)) {
                    return FilesRelativeDirectory;
                }
                return m.Value;
            }));
        }

        private DelegateCommand _changeFileCommand;

        public DelegateCommand ChangeFileCommand => _changeFileCommand ?? (_changeFileCommand = new DelegateCommand(() => {
            var relativeDirectory = GetRelativeDirectory();
            var currentValue = string.IsNullOrWhiteSpace(Value) ? null : Value;
            if (currentValue != null) {
                currentValue = Path.IsPathRooted(currentValue) ? currentValue : FileUtils.NormalizePath(Path.Combine(relativeDirectory, currentValue));
            }

            try {
                if (DirectoryMode) {
                    var dialog = new FolderBrowserDialog {
                        ShowNewFolderButton = true,
                        SelectedPath = currentValue != null && Directory.Exists(currentValue) ? currentValue : relativeDirectory,
                        Description = $"Select {DisplayName.ToSentenceMember()}"
                    };
                    if (dialog.ShowDialog() == DialogResult.OK) {
                        Value = FinalizeFilename(dialog.SelectedPath);
                    }
                } else {
                    var filename = FileRelatedDialogs.Open(new OpenDialogParams {
                        DirectorySaveKey = null,
                        Filters = { Filter },
                        Title = $"Select {DisplayName.ToSentenceMember()}",
                        InitialDirectory = relativeDirectory
                    }, currentValue);
                    if (filename != null) {
                        Value = FinalizeFilename(filename);
                    }
                }
            } catch (ArgumentException ex) {
                NonfatalError.Notify("Can’t select file", ex);
            } catch (Exception ex) {
                NonfatalError.Notify("Can’t change value", ex);
            }

            string FinalizeFilename(string value) {
                if (_allowAbsolute == AbsoluteMode.Require) {
                    return value;
                }

                var relative = FileUtils.GetRelativePath(value, GetRelativeDirectory());
                if (_allowAbsolute == AbsoluteMode.Allow) {
                    if (relative.Split(new[] { @"../", @"..\" }, StringSplitOptions.None).Length > 2) {
                        return value;
                    }
                } else if (Path.IsPathRooted(relative)) {
                    throw new InformativeException("Can’t set the absolute path",
                            "Please, pick a file within app’s directory or at least located on the same drive.");
                }
                return relative;
            }
        }));
    }
}