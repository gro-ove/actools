using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.Managers.Plugins;
using AcTools;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Pages.ContentTools {
    public static class Bc7Encoder {
        private static PluginsRequirement _requirement;
        public static PluginsRequirement Requirement => _requirement ?? (_requirement = new PluginsRequirement(KnownPlugins.Bc7Tool));
        
        public enum ResizeMode {
            [Description("No resize")]
            None,

            [Description("Linear")]
            Linear,

            [Description("Bicubic")]
            Bicubic,

            [Description("Point")]
            Point,

            [Description("Crop")]
            Crop
        }

        public class EncodeParams {
            public ResizeMode ResizeMode { get; set; }
            public bool ConvertBc { get; set; }
            public bool ConvertAny { get; set; }
        }

        public class StreamSpan {
            public StreamSpan(ReadAheadBinaryReader reader, int length) {
                _stream = reader.BaseStream;
                _position = reader.Position;
                Length = length;
                reader.Skip(length);
            }

            public StreamSpan(byte[] data) {
                _data = data;
                Length = data.Length;
            }

            private readonly Stream _stream;
            private readonly long _position;
            private readonly byte[] _data;

            public int Length { get; }

            public void WriteTo(Stream destination) {
                if (_data != null) {
                    destination.Write(_data, 0, _data.Length);
                } else {
                    var oldPosition = _stream.Position;
                    try {
                        _stream.Position = _position;
                        _stream.CopyToLimited(destination, Length);
                    } finally {
                        _stream.Position = oldPosition;
                    }
                }
            }
        }

        public static async Task<StreamSpan> EncodeTextureAsync(StreamSpan data, EncodeParams encodeParams, CancellationToken token) {
            var plugin = PluginsManager.Instance.GetById(KnownPlugins.Bc7Tool);
            var tool = plugin?.GetFilename("bc7.exe");
            if (tool == null || !File.Exists(tool)) {
                throw new Exception("Required tool is missing");
            }
            var args = new List<string>();
            if (encodeParams.ConvertAny) args.Add(@"--process-any");
            if (encodeParams.ConvertBc) args.Add(@"--upgrade-bc");
            switch (encodeParams.ResizeMode) {
                case ResizeMode.None:
                    break;
                case ResizeMode.Linear:
                    args.Add(@"--linear");
                    break;
                case ResizeMode.Bicubic:
                    args.Add(@"--bicubic");
                    break;
                case ResizeMode.Point:
                    args.Add(@"--point");
                    break;
                case ResizeMode.Crop:
                    args.Add(@"--crop");
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            using (var process = new Process()) {
                process.StartInfo.FileName = tool;
                process.StartInfo.Arguments = args.JoinToString(' ');
                process.StartInfo.RedirectStandardInput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.UseShellExecute = false;

                process.Start();
                ChildProcessTracker.AddProcess(process);
                var errMsg = "";
                process.ErrorDataReceived += (sender, e) => errMsg += e.Data + '\n';
                process.BeginErrorReadLine();
                data.WriteTo(process.StandardInput.BaseStream);
                process.StandardInput.BaseStream.Close();
                var response = await process.StandardOutput.BaseStream.ReadAsBytesAsync().ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                await process.WaitForExitAsync(token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                if (process.ExitCode != 0) {
                    throw new Exception("Encoding error: " + errMsg);
                }
                return new StreamSpan(response);
            }
        }

        public static async Task<int> EncodeKn5Async(string kn5Filename, string tmpFilename, EncodeParams encodeParams,
                IProgress<AsyncProgressEntry> progress, CancellationToken token) {
            return await Task.Run(async () => {
                var counter = 0;
                var refreshMode = File.Exists(tmpFilename);
                using (var reader = new ReadAheadBinaryReader(refreshMode ? tmpFilename : kn5Filename)) {
                    if (new string(reader.ReadChars(6)) != "sc6969") throw new Exception("Invalid header");
                    var version = reader.ReadInt32();
                    if (version < 5 || version > 6 || version == 6 && reader.ReadInt32() != 0) throw new Exception("Invalid version");
                    var textureCount = reader.ReadInt32();
                    var texturesToKeep = new Dictionary<string, StreamSpan>();
                    for (var i = 0; i < textureCount; ++i) {
                        var activeFlag = reader.ReadInt32();
                        var name = reader.ReadString();
                        var length = (int)reader.ReadUInt32();
                        if (activeFlag != 1) throw new Exception("Disabled texture");
                        texturesToKeep[name] = new StreamSpan(reader, length);
                    }
                    var converted = (await texturesToKeep.Select(async (x, i) => {
                        token.ThrowIfCancellationRequested();
                        progress.Report(x.Key, i, texturesToKeep.Count);
                        try {
                            x = new KeyValuePair<string, StreamSpan>(x.Key, await EncodeTextureAsync(x.Value, encodeParams, token).ConfigureAwait(false));
                            token.ThrowIfCancellationRequested();
                            ++counter;
                        } catch (Exception e) {
                            Logging.Warning("Failed to encode: " + e);
                        }
                        return x;
                    }).WhenAll(4)).ToList();
                    token.ThrowIfCancellationRequested();
                    if (counter == 0) return 0;
                    using (var data = new ExtendedBinaryWriter(refreshMode ? kn5Filename : $@"{kn5Filename}.tmp")) {
                        data.Write(Encoding.ASCII.GetBytes("sc6969"));
                        data.Write(5);
                        data.Write(texturesToKeep.Count);
                        foreach (var p in converted) {
                            data.Write(1);
                            data.Write(p.Key);
                            data.Write(p.Value.Length);
                            p.Value.WriteTo(data.BaseStream);
                            token.ThrowIfCancellationRequested();
                        }
                        await reader.CopyToAsync(data.BaseStream);
                    }
                }
                if (!refreshMode) {
                    FileUtils.HardLinkOrCopy(kn5Filename, tmpFilename);
                    FileUtils.Recycle(kn5Filename);
                    FileUtils.TryToDelete(kn5Filename);
                    File.Move($@"{kn5Filename}.tmp", kn5Filename);
                }
                return counter;
            });
        }
    }
}