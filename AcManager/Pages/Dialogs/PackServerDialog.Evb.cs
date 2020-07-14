using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using AcManager.Tools.Helpers;
using AcManager.Tools.Objects;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using AcTools.Windows;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Pages.Dialogs {
    public partial class PackServerDialog {
        public partial class ViewModel {
            [ItemCanBeNull]
            private async Task<string> PackIntoSingleAsync(string destination, IProgress<AsyncProgressEntry> progress, CancellationToken cancellation) {
                var list = await Server.PackServerData(IncludeExecutable, ServerPresetPackMode.Windows, true, cancellation);
                if (list == null || cancellation.IsCancellationRequested) return null;

                var temporary = FilesStorage.Instance.GetTemporaryFilename("EVB Package");
                Directory.CreateDirectory(temporary);

                try {
                    var executable = list.First(x => x.Key.EndsWith(@".exe"));

                    // Building XML with params
                    var doc = new XmlDocument();
                    doc.AppendChild(doc.CreateXmlDeclaration("1.0", "utf-16", null));

                    var uniqueName = $@"__ROOT_TAG_UNIQUE_NAME_{StringExtension.RandomString(16)}";
                    var rootTag = (XmlElement)doc.AppendChild(doc.CreateElement(uniqueName));

                    // <InputFile>, <OutputFile>
                    rootTag.AppendChild(doc.CreateElement("InputFile")).InnerText = executable.GetFilename(temporary) ??
                            throw new Exception("Main executable not in the list");
                    rootTag.AppendChild(doc.CreateElement("OutputFile")).InnerText = destination;

                    // <Files>
                    var filesTag = (XmlElement)rootTag.AppendChild(doc.CreateElement("Files"));
                    filesTag.AppendChild(doc.CreateElement("Enabled")).InnerText = "true";
                    filesTag.AppendChild(doc.CreateElement("DeleteExtractedOnExit")).InnerText = "true";
                    filesTag.AppendChild(doc.CreateElement("CompressFiles")).InnerText = "true";

                    // A bit of mess for directories
                    XmlElement CreateDirectory(XmlElement parent, string name) {
                        var element = (XmlElement)parent.AppendChild(doc.CreateElement("File"));
                        element.AppendChild(doc.CreateElement("Type")).InnerText = "3";
                        element.AppendChild(doc.CreateElement("Name")).InnerText = name;
                        element.AppendChild(doc.CreateElement("Action")).InnerText = "0";
                        element.AppendChild(doc.CreateElement("OverwriteDateTime")).InnerText = "false";
                        element.AppendChild(doc.CreateElement("OverwriteAttributes")).InnerText = "false";
                        return (XmlElement)element.AppendChild(doc.CreateElement("Files"));
                    }

                    void CreateFile(XmlElement parent, string name, string filename) {
                        var element = (XmlElement)parent.AppendChild(doc.CreateElement("File"));
                        element.AppendChild(doc.CreateElement("Type")).InnerText = "2";
                        element.AppendChild(doc.CreateElement("Name")).InnerText = name;
                        element.AppendChild(doc.CreateElement("File")).InnerText = filename;
                        element.AppendChild(doc.CreateElement("ActiveX")).InnerText = "false";
                        element.AppendChild(doc.CreateElement("ActiveXInstall")).InnerText = "false";
                        element.AppendChild(doc.CreateElement("Action")).InnerText = "0";
                        element.AppendChild(doc.CreateElement("OverwriteDateTime")).InnerText = "false";
                        element.AppendChild(doc.CreateElement("OverwriteAttributes")).InnerText = "false";
                        element.AppendChild(doc.CreateElement("PassCommandLine")).InnerText = "false";
                    }

                    var directories = new Dictionary<string, XmlElement>();
                    var directoriesRoot = (XmlElement)filesTag.AppendChild(doc.CreateElement("Files"));
                    directories[""] = CreateDirectory(directoriesRoot, "%DEFAULT FOLDER%");

                    XmlElement GetDirectoryOf(string name) {
                        var directoryName = Path.GetDirectoryName(name) ?? "";
                        if (!directories.TryGetValue(directoryName, out XmlElement directory)) {
                            directory = CreateDirectory(GetDirectoryOf(directoryName), Path.GetFileName(directoryName));
                            directories[directoryName] = directory;
                        }

                        return directory;
                    }

                    foreach (var entry in list.ApartFrom(executable)) {
                        CreateFile(GetDirectoryOf(entry.Key), Path.GetFileName(entry.Key), entry.GetFilename(temporary));
                    }

                    // <Registries>
                    var registriesTag = (XmlElement)rootTag.AppendChild(doc.CreateElement("Registries"));
                    registriesTag.AppendChild(doc.CreateElement("Enabled")).InnerText = "false";

                    // <Packaging>
                    var packagingTag = (XmlElement)rootTag.AppendChild(doc.CreateElement("Packaging"));
                    packagingTag.AppendChild(doc.CreateElement("Enabled")).InnerText = "false";

                    // <Options>
                    var optionsTag = (XmlElement)rootTag.AppendChild(doc.CreateElement("Options"));
                    optionsTag.AppendChild(doc.CreateElement("ShareVirtualSystem")).InnerText = "true";
                    optionsTag.AppendChild(doc.CreateElement("MapExecutableWithTemporaryFile")).InnerText = "false";
                    optionsTag.AppendChild(doc.CreateElement("AllowRunningOfVirtualExeFiles")).InnerText = "true";

                    // EVB-file
                    var manifest = FileUtils.GetTempFileName(temporary, ".evb");
                    await FileUtils.WriteAllBytesAsync(manifest, Encoding.Unicode.GetBytes(doc.OuterXml.Replace(uniqueName, "")));

                    // Processing
                    var evb = Shell32.FindExecutable(manifest);

                    if (Path.GetFileName(evb) == "enigmavb.exe") {
                        evb = Path.Combine(Path.GetDirectoryName(evb) ?? "", "enigmavbconsole.exe");
                    }

                    if (Path.GetFileName(evb)?.Contains("enigmavbconsole", StringComparison.OrdinalIgnoreCase) != true || !File.Exists(evb)) {
                        throw new InformativeException("Enigma Virtual Box not found",
                                "Please, make sure it’s installed and .EVB-files are associated with its “enigmavbconsole.exe” executable.");
                    }

                    var process = ProcessExtension.Start(evb, new[] { manifest }, new ProcessStartInfo {
                        CreateNoWindow = true,
                        RedirectStandardError = true,
                        RedirectStandardOutput = true,
                        UseShellExecute = false
                    });

                    process.Start();

                    var error = new StringBuilder();
                    var output = new StringBuilder();
                    process.ErrorDataReceived += (sender, args) => {
                        if (args.Data != null) {
                            error.Append(args.Data);
                            error.Append('\n');
                        }
                    };
                    process.OutputDataReceived += (sender, args) => {
                        if (args.Data != null) {
                            output.Append(args.Data);
                            error.Append('\n');
                        }
                    };
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    await process.WaitForExitAsync(cancellation);
                    if (!process.HasExitedSafe()) {
                        process.Kill();
                    }

                    Logging.Debug("STDOUT: " + output);
                    Logging.Debug("STDERR: " + error);

                    if (process.ExitCode != 0 && !File.Exists(destination)) {
                        throw new Exception($@"Exit code={process.ExitCode}");
                    }

                    return destination;
                } finally {
                    try {
                        //list.DisposeEverything();
                        //Directory.Delete(temporary, true);
                    } catch (Exception e) {
                        Logging.Warning(e);
                    }
                }
            }
        }
    }
}