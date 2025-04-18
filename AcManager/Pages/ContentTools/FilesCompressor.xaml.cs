﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using AcManager.Controls.Graphs;
using AcManager.Tools;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Managers.Directories;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using AcTools.Windows;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Win32;
using FirstFloor.ModernUI.Windows.Converters;
using OxyPlot;
using OxyPlot.Series;
using OxyPlot.Wpf;

namespace AcManager.Pages.ContentTools {
    public partial class FilesCompressor {
        public static long OptionCompressThreshold = 50000;

        public static string GetContentDirectory() {
            return Path.Combine(AcRootDirectory.Instance.RequireValue, "content");
        }

        public static Func<IEnumerable<string>> NotAvailableReason { get; } = GetNonAvailableReason;

        private static IEnumerable<string> GetNonAvailableReason() {
            if (!WindowsVersionHelper.IsWindows10OrGreater) {
                yield return "Tool “compact.exe” was added in Windows 10";
            }

            var format = DriveInfo.GetDrives().FirstOrDefault(x => x.RootDirectory.FullName == Path.GetPathRoot(AcRootDirectory.Instance.Value))?.DriveFormat;
            if (format != @"NTFS" && format != null) {
                yield return $"Not supported file system: {format}";
            }
        }

        public class FileToCompress : NotifyPropertyChanged, IWithId {
            public FileInfo FileInfo { get; }
            public string RelativePath { get; }

            private long _compressedSize;

            public long CompressedSize {
                get => _compressedSize;
                set => Apply(value, ref _compressedSize);
            }

            public double Ratio => _isCompressed ? (double)CompressedSize / FileInfo.Length : 1d;

            private bool _isCompressed;

            public bool IsCompressed {
                get => _isCompressed;
                private set => Apply(value, ref _isCompressed);
            }

            public string Id { get; }

            public FileToCompress(FileInfo fileInfo) {
                FileInfo = fileInfo;
                RelativePath = FileUtils.GetPathWithin(fileInfo.FullName, GetContentDirectory());
                CompressedSize = GetFileSizeOnDisk(fileInfo, out var isCompressed);
                IsCompressed = isCompressed;
                Id = NormalizePath(fileInfo.FullName);
            }

            public static string NormalizePath(string fileName) {
                return FileUtils.NormalizePath(fileName).ToLowerInvariant();
            }

            private readonly Busy _busy = new Busy(true);

            public void Refresh() {
                _busy.Yield(() => {
                    try {
                        CompressedSize = GetFileSizeOnDisk(FileInfo, out var isCompressed);
                        IsCompressed = isCompressed;
                        OnPropertyChanged(nameof(Ratio));
                    } catch (Exception e) {
                        Logging.Error(e);
                    }
                });
            }

            private DelegateCommand _viewInExplorerCommand;

            public DelegateCommand ViewInExplorerCommand
                => _viewInExplorerCommand ?? (_viewInExplorerCommand = new DelegateCommand(() => { WindowsHelper.ViewFile(FileInfo.FullName); }));
        }

        private static readonly Dictionary<string, uint> ClusterSizes = new Dictionary<string, uint>();

        private static long GetFileSizeOnDisk(FileInfo info, out bool isCompressed) {
            var key = info.Directory?.Root.FullName ?? string.Empty;

            uint clusterSize;
            lock (ClusterSizes) {
                if (!ClusterSizes.TryGetValue(key, out clusterSize)) {
                    clusterSize = Kernel32.GetDiskFreeSpaceW(key, out var sectorsPerCluster, out var bytesPerSector, out _, out _) == 0 ? 1
                            : sectorsPerCluster * bytesPerSector;
                    ClusterSizes[key] = clusterSize;
                }
            }

            var loSize = Kernel32.GetCompressedFileSizeW(info.FullName, out var hoSize);
            var size = ((long)hoSize << 32) | loSize;
            isCompressed = info.Length != size;
            return (size + clusterSize - 1) / clusterSize * clusterSize;
        }

        private IAcManagerNew _targetManager;
        private AcCommonObject[] _targetObjects;

        protected override void InitializeOverride(Uri uri) {
            var id = uri.GetQueryParam("Type");
            if (id != null) {
                _targetManager = Superintendent.Instance.GetManagerById(id);
                if (_targetManager != null) {
                    _targetObjects = uri.GetQueryParam("Items")?.Split('\n').Select(x => _targetManager.GetObjectById(x))
                            .OfType<AcCommonObject>().ToArray();
                }
            }
        }

        protected override async Task<bool> LoadAsyncOverride(IProgress<AsyncProgressEntry> progress, CancellationToken cancellation) {
            var contentDirectory = GetContentDirectory();
            var scannedFiles = new List<FileToCompress>();
            
            var index = new[] { 0 };
            var objectsProgress = progress.Subrange(0.04, 0.9);

            if (_targetObjects?.Length > 0) {
                foreach (var obj in _targetObjects) {
                    scannedFiles.AddRange(await ScanObjectFiles(obj, false));
                }

                Task<List<FileToCompress>> ScanObjectFiles(AcCommonObject obj, bool carMode) {
                    var info = new DirectoryInfo(obj.Location);
                    objectsProgress.Report($"Scanning ({obj.Name ?? obj.Id})…", index[0]++, _targetObjects.Length);
                    return ActualScan(info, carMode);
                }
            } else {
                progress.Report("Loading cars…", 0.01);
                await CarsManager.Instance.EnsureLoadedAsync();

                progress.Report("Loading tracks…", 0.02);
                await CarsManager.Instance.EnsureLoadedAsync();

                progress.Report("Loading showrooms…", 0.03);
                await ShowroomsManager.Instance.EnsureLoadedAsync();

                var cars = CarsManager.Instance.Loaded.ToList();
                var tracks = TracksManager.Instance.Loaded.ToList();
                var showrooms = ShowroomsManager.Instance.Loaded.ToList();

                foreach (var obj in cars) {
                    scannedFiles.AddRange(await ScanObjectFiles(obj, true));
                }

                foreach (var obj in tracks) {
                    scannedFiles.AddRange(await ScanObjectFiles(obj, false));
                }

                foreach (var obj in showrooms) {
                    scannedFiles.AddRange(await ScanObjectFiles(obj, false));
                }

                scannedFiles.AddRange(await ScanDirectoryFiles(AcPaths.GetWeatherDirectory(AcRootDirectory.Instance.RequireValue), 0.96));
                scannedFiles.AddRange(await ScanDirectoryFiles(Path.Combine(AcRootDirectory.Instance.RequireValue, "content", "driver"), 0.97));
                scannedFiles.AddRange(await ScanDirectoryFiles(Path.Combine(AcRootDirectory.Instance.RequireValue, "content", "texture"), 0.98));
                scannedFiles.AddRange(await ScanDirectoryFiles(Path.Combine(AcRootDirectory.Instance.RequireValue, "content", "objects3D"), 0.99));

                Task<List<FileToCompress>> ScanObjectFiles(AcCommonObject obj, bool carMode) {
                    var info = new DirectoryInfo(obj.Location);
                    objectsProgress.Report($"Scanning ({obj.Name ?? obj.Id})…", index[0]++, cars.Count + tracks.Count + showrooms.Count);
                    return ActualScan(info, carMode);
                }
            }

            FilesToCompress.ReplaceEverythingBy_Direct(scannedFiles.OrderBy(x => x.RelativePath));
            return FilesToCompress.Any();

            Task<List<FileToCompress>> ScanDirectoryFiles(string directory, double progressValue) {
                var info = new DirectoryInfo(directory);
                progress.Report($"Scanning ({FileUtils.GetPathWithin(directory, contentDirectory)})…", progressValue);
                return ActualScan(info, false);
            }

            Task<List<FileToCompress>> ActualScan(DirectoryInfo info, bool carMode) {
                return Task.Run(() => {
                    try {
                        var list = info.GetFiles("*.dds", SearchOption.AllDirectories)
                                       .Concat(info.GetFiles("*.kn5", SearchOption.AllDirectories));

                        if (carMode) {
                            list = list.Concat(info.GetFiles("*.knh"));
                            var animations = new DirectoryInfo(Path.Combine(info.FullName, "animations"));
                            if (animations.Exists) {
                                list = list.Concat(animations.GetFiles("*.ksanim"));
                            }
                        } else {
                            list = list.Concat(info.GetFiles("*.ai", SearchOption.AllDirectories));
                        }

                        return list.Where(x => x.Length > OptionCompressThreshold).Select(x => new FileToCompress(x)).ToList();
                    } catch (Exception e) {
                        Logging.Warning(e);
                        return new List<FileToCompress>();
                    }
                });
            }
        }

        public ChangeableObservableCollection<FileToCompress> FilesToCompress { get; }

        public FilesCompressor() {
            FilesToCompress = new ChangeableObservableCollection<FileToCompress>();
            FilesToCompress.ItemPropertyChanged += OnItemPropertyChanged;
            FilesToCompress.CollectionChanged += OnCollectionChanged;

            var watcher = new FileSystemWatcher(GetContentDirectory()) {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.Attributes,
                Filter = "*.*",
                EnableRaisingEvents = true
            };

            watcher.Changed += OnFileChanged;
            this.OnActualUnload(watcher);
        }

        private static bool IsToBeCompressed(string filename) {
            var index = filename.LastIndexOf('.');
            if (index == -1) return false;
            switch (filename.Substring(index + 1).ToLowerInvariant()) {
                case "dds":
                case "kn5":
                case "knh":
                case "ksanim":
                case "ai":
                    return true;
                default:
                    return false;
            }
        }

        private void OnFileChanged(object o, FileSystemEventArgs e) {
            if (IsToBeCompressed(e.Name)) {
                FilesToCompress.GetByIdOrDefault(FileToCompress.NormalizePath(e.FullPath))?.Refresh();
            }
        }

        private void OnItemPropertyChanged(object sender, PropertyChangedEventArgs propertyChangedEventArgs) {
            UpdateValues();
        }

        private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e) {
            UpdateValues();
        }

        private long _totalSize;

        public long TotalSize {
            get => _totalSize;
            private set => this.Apply(value, ref _totalSize);
        }

        private long _compressedSize;

        public long CompressedSize {
            get => _compressedSize;
            private set => this.Apply(value, ref _compressedSize);
        }

        private int _compressedCount;

        public int CompressedCount {
            get => _compressedCount;
            set {
                if (Equals(value, _compressedCount)) return;
                _compressedCount = value;
                OnPropertyChanged();
                _decompressCommand?.RaiseCanExecuteChanged();
            }
        }

        private int _decompressedCount;

        public int DecompressedCount {
            get => _decompressedCount;
            set {
                if (Equals(value, _decompressedCount)) return;
                _decompressedCount = value;
                OnPropertyChanged();
                _compressCommand?.RaiseCanExecuteChanged();
            }
        }

        private double _totalRatio;

        public double TotalRatio {
            get => _totalRatio;
            private set => this.Apply(value, ref _totalRatio);
        }

        private PlotModel _plotModel;

        public PlotModel PlotModel {
            get => _plotModel;
            private set => this.Apply(value, ref _plotModel);
        }

        private readonly Busy _updateValuesBusy = new Busy();

        private PieSlice _savedSlide, _compressedSlice;

        private void UpdateValues() {
            _updateValuesBusy.Yield(() => {
                var list = FilesToCompress;

                long totalSize = 0, compressedSize = 0;
                int compressedCount = 0, decompressedCount = 0;
                for (var i = list.Count - 1; i >= 0; i--) {
                    var x = list[i];
                    var size = x.FileInfo.Length;
                    totalSize += size;
                    if (x.IsCompressed) {
                        compressedCount++;
                        compressedSize += x.CompressedSize;
                    } else {
                        decompressedCount++;
                        compressedSize += size;
                    }
                }

                TotalSize = totalSize;
                CompressedSize = compressedSize;
                CompressedCount = compressedCount;
                DecompressedCount = decompressedCount;
                TotalRatio = (double)CompressedSize / (TotalSize == 0 ? 1 : TotalSize);

                var savedLabel = string.Format(TotalRatio < 0.9 ? ColonConverter.FormatBoth : "{0}", "Saved", (TotalSize - CompressedSize).ToReadableSize());
                var compressedLabel = string.Format(TotalRatio < 0.9 ? ColonConverter.FormatBoth : "{0}", "Compressed", CompressedSize.ToReadableSize());

                if (_savedSlide == null) {
                    var color = this.ToOxyColor("WindowText");
                    _savedSlide = new PieSlice(savedLabel, TotalSize - CompressedSize) { Fill = Colors.SeaGreen.ToOxyColor() };
                    _compressedSlice = new PieSlice(compressedLabel, CompressedSize) { Fill = Colors.Peru.ToOxyColor() };
                    PlotModel = new PlotModel {
                        TextColor = color,
                        Background = Colors.Transparent.ToOxyColor(),
                        Series = {
                            new PieSeriesExt {
                                StrokeThickness = 0,
                                InsideLabelPosition = 0.45,
                                AngleSpan = 360,
                                StartAngle = 180,
                                InsideLabelColor = Colors.White.ToOxyColor(),
                                TextColor = color,
                                Diameter = 1,
                                InnerDiameter = 0.5,
                                Slices = {
                                    _savedSlide,
                                    _compressedSlice
                                }
                            }
                        }
                    };
                } else {
                    _savedSlide.Label = savedLabel;
                    _savedSlide.Value = TotalSize - CompressedSize;
                    _compressedSlice.Label = compressedLabel;
                    _compressedSlice.Value = CompressedSize;
                    PlotModel.InvalidatePlot(true);
                }
            });
        }

        public SettingEntryStored CompressionMode { get; } = new SettingEntryStored("/FilesCompression.Level") {
            new SettingEntry("XPRESS4K", "XPRESS4K"),
            new SettingEntry("XPRESS8K", "XPRESS8K"),
            new SettingEntry("XPRESS16K", "XPRESS16K"),
            new DefaultSettingEntry("LZX", string.Format(ToolsStrings.Common_Recommended, "LZX")),
        };

        private AsyncCommand _compressCommand;

        public AsyncCommand CompressCommand => _compressCommand ?? (_compressCommand = new AsyncCommand(
                () => Process("Compressing…", FilesToCompress.Where(x => !x.IsCompressed), "/C", "/EXE:" + CompressionMode.SelectedValue),
                () => DecompressedCount > 0));

        private AsyncCommand _decompressCommand;

        public AsyncCommand DecompressCommand => _decompressCommand ?? (_decompressCommand = new AsyncCommand(
                () => Process("Decompressing…", FilesToCompress.Where(x => x.IsCompressed), "/U", "/EXE"),
                () => CompressedCount > 0));

        private static async Task Process(string title, IEnumerable<FileToCompress> items, params string[] flags) {
            const int step = 20;
            var directory = GetContentDirectory();

            try {
                using (var waiting = new WaitingDialog(title)) {
                    var queue = items.ToList();
                    for (var i = 0; i < queue.Count && !waiting.CancellationToken.IsCancellationRequested; i += step) {
                        var files = queue.Skip(i).Take(step).ToList();
                        waiting.Report(files[0].RelativePath, i, queue.Count);

                        var filesOffset = i;
                        var filesIndex = 1;
                        var output = new StringBuilder();
                        using (var process = ProcessExtension.Start(@"compact", files.Select(x => x.RelativePath).Prepend(flags),
                                new ProcessStartInfo {
                                    WorkingDirectory = directory,
                                    CreateNoWindow = true,
                                    RedirectStandardInput = true,
                                    RedirectStandardOutput = true,
                                    UseShellExecute = false,
                                    RedirectStandardError = true,
                                    StandardOutputEncoding = Encoding.UTF8,
                                    StandardErrorEncoding = Encoding.UTF8,
                                    WindowStyle = ProcessWindowStyle.Hidden
                                }, true)) {
                            process.OutputDataReceived += (sender, args) => {
                                if (args.Data == null) return;
                                if (args.Data.EndsWith(@" [OK]") && filesIndex < files.Count) {
                                    waiting.Report(files[filesIndex].RelativePath, filesOffset + filesIndex++, queue.Count);
                                }
                                output.Append(args.Data).Append('\n');
                            };
                            process.ErrorDataReceived += (sender, args) => {
                                if (args.Data == null) return;
                                output.Append(args.Data).Append('\n');
                            };
                            process.BeginOutputReadLine();
                            await process.WaitForExitAsync(waiting.CancellationToken);

                            if (process.ExitCode != 0) {
                                Logging.Warning(output.ToString());
                                throw new InformativeException("Can’t compress files",
                                        $"Tool compact.exe failed to run: {process.ExitCode}. More information in CM logs.");
                            }

                            Logging.Debug(output.ToString());
                        }
                    }
                }
            } catch (Exception e) when (e.IsCancelled()) { } catch (Exception e) {
                NonfatalError.Notify("Can’t process files", e);
            }
        }

        private static List<string> _newFilesToBeCompressedLater = new List<string>();
        private static bool _newFilesAddingEnqueued;
        private static bool _compressingEnqueued;

        public static void RegisterNewFileToBeCompressedLater(string filename) {
            if (!IsToBeCompressed(filename)) return;

            filename = filename.ToLowerInvariant();
            lock (_newFilesToBeCompressedLater) {
                if (!_newFilesToBeCompressedLater.Contains(filename)) {
                    _newFilesToBeCompressedLater.Add(filename);
                }

                if (!_newFilesAddingEnqueued) {
                    _newFilesAddingEnqueued = true;
                    Task.Delay(TimeSpan.FromSeconds(5d)).ContinueWith(r => {
                        var ownList = new HashSet<string>();
                        lock (_newFilesToBeCompressedLater) {
                            foreach (var item in _newFilesToBeCompressedLater) {
                                ownList.Add(item);
                            }
                            _newFilesToBeCompressedLater.Clear();
                        }

                        var listFilename = FilesStorage.Instance.GetTemporaryFilename("To Compress.txt");
                        try {
                            foreach (var item in File.ReadLines(listFilename)) {
                                ownList.Add(item);
                            }
                        } catch {
                            // Do nothing
                        }
                        File.WriteAllLines(listFilename, ownList);
                        _newFilesAddingEnqueued = false;
                    });
                }
            }
        }

        public static void BackgroundCompressStep() {
            if (_compressingEnqueued) return;
            _compressingEnqueued = true;

            Task.Delay(TimeSpan.FromSeconds(5d)).ContinueWith(r => {
                try {
                    // Logging.Debug("[BgCompression] Launching…");
                
                    var contentDirectory = GetContentDirectory();
                    var listFilename = FilesStorage.Instance.GetTemporaryFilename("To Compress.txt");
                    string[] lines;
                    try {
                        lines = File.ReadAllLines(listFilename);
                    } catch {
                        lines = new string[0];
                    }
                    if (lines.Length > 0) {
                        Logging.Debug("[BgCompression] In the queue: " + lines.Length);
                    }
                    var candidates = lines.Take(20).Where(x => {
                        var fileInfo = new FileInfo(x);
                        if (!fileInfo.Exists || fileInfo.Length < OptionCompressThreshold) return false;
                        GetFileSizeOnDisk(fileInfo, out var isCompressed);
                        return !isCompressed;
                    }).Select(x => FileUtils.GetPathWithin(x, contentDirectory)).ToList();
                    lines = lines.Skip(20).ToArray();

                    if (candidates.Count == 0 && MathUtils.Random() > 0.95) {
                        var carMode = MathUtils.Random() > 0.5;
                        var randomFolder = Path.Combine(contentDirectory, carMode ? @"cars" : @"tracks");
                        var randomEntity = Directory.GetDirectories(randomFolder).RandomElementOrDefault();
                        if (randomEntity == null) return;
                    
                        Logging.Debug("[BgCompression] Queue is empty, randomly checking: " + randomEntity);
                        var info = new DirectoryInfo(randomEntity);
                        IEnumerable<FileInfo> list;
                        var firstScan = info.GetFiles("*.dds", SearchOption.AllDirectories);
                        if (firstScan.Length == 0) {
                            firstScan = info.GetFiles("*.kn5", SearchOption.AllDirectories);
                            if (firstScan.Length == 0) return;
                            list = firstScan;
                        } else {
                            list = firstScan.Append(info.GetFiles("*.kn5", SearchOption.AllDirectories));
                        }
                        if (carMode) {
                            list = list.Concat(info.GetFiles("*.knh"));
                            var animations = new DirectoryInfo(Path.Combine(info.FullName, "animations"));
                            if (animations.Exists) {
                                list = list.Concat(animations.GetFiles("*.ksanim"));
                            }
                        } else {
                            list = list.Concat(info.GetFiles("*.ai", SearchOption.AllDirectories));
                        }
                    
                        var filtered = list.Where(fileInfo => {
                            if (!fileInfo.Exists || fileInfo.Length < OptionCompressThreshold) return false;
                            GetFileSizeOnDisk(fileInfo, out var isCompressed);
                            return !isCompressed;
                        }).ToList();
                        if (filtered.Count > 20) {
                            lines = lines.Concat(filtered.Skip(20).Select(x => x.FullName)).ToArray();
                            filtered = filtered.Take(20).ToList();
                        }
                        candidates = filtered.Select(x => FileUtils.GetPathWithin(x.FullName, contentDirectory)).ToList();
                    }
                
                    if (candidates.Count == 0) return;
                    Logging.Debug("[BgCompression] Compression candidates: " + candidates.JoinToString("; "));
                    using (var process = ProcessExtension.Start(@"compact", candidates.Prepend("/C", "/EXE:LZX"),
                            new ProcessStartInfo {
                                WorkingDirectory = contentDirectory,
                                CreateNoWindow = true,
                                RedirectStandardInput = true,
                                RedirectStandardOutput = true,
                                UseShellExecute = false,
                                RedirectStandardError = true,
                                StandardOutputEncoding = Encoding.UTF8,
                                StandardErrorEncoding = Encoding.UTF8,
                                WindowStyle = ProcessWindowStyle.Hidden
                            }, true)) {
                        process.PriorityClass = ProcessPriorityClass.Idle;
                        process.WaitForExit();
                        Logging.Debug($"[BgCompression] Done: {process.ExitCode} ({candidates.JoinToString(@"; ")})");
                    }
                
                    File.WriteAllLines(listFilename, lines);
                } catch (Exception e) {
                    Logging.Debug($"[BgCompression] Exception: {e}");
                } finally {
                    _compressingEnqueued = false;
                }
            });
        }
    }
}