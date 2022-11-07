using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using AcManager.Tools.Managers;
using AcManager.Tools.Miscellaneous;
using AcTools.DataFile;
using AcTools.Processes;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AcManager.Tools.Helpers {
    public class ReplayDetails {
        [CanBeNull]
        public string ParseError;

        public string WeatherId;
        public string TrackId;
        public string CarId;
        public string DriverName;
        public string NationCode;
        public string DriverTeam;
        public string CarSkinId;
        public string TrackConfiguration;
        public string RaceIniConfig;
        public double RecordingIntervalMs;
        public float? SunAngleFrom;
        public float? SunAngleTo;
        public int? CustomTime;
        public int Version;
        public int CarsNumber;
        public int NumberOfFrames;
        public bool AllowToOverrideTime;

        [JsonConstructor]
        public ReplayDetails() { }

        private ReplayDetails(string filename) {
            try {
                using (var reader = new ReplayReader(filename)) {
                    var version = reader.ReadInt32();
                    Version = version;

                    if (version == 16) {
                        ParseV16(reader);
                    } else {
                        ParseGeneric(version, reader);
                    }
                }

                ParseError = null;
            } catch (Exception e) {
                Logging.Warning(e);
                ParseError = e.Message;
            }
        }

        private void ParseV16(ReplayReader reader) {
            RecordingIntervalMs = reader.ReadDouble();

            WeatherId = reader.ReadString();
            TrackId = reader.ReadString();
            TrackConfiguration = reader.ReadString();

            CarsNumber = reader.ReadInt32();
            reader.ReadInt32(); // current recording index
            var frames = reader.ReadInt32();
            NumberOfFrames = frames;

            var trackObjectsNumber = reader.ReadInt32();
            var minSunAngle = default(float?);
            var maxSunAngle = default(float?);
            for (var i = 0; i < frames; i++) {
                float sunAngle = reader.ReadHalf();
                reader.Skip(2 + trackObjectsNumber * 12);
                if (!minSunAngle.HasValue) minSunAngle = sunAngle;
                maxSunAngle = sunAngle;
            }

            if (minSunAngle.HasValue
                    && Game.ConditionProperties.GetSeconds(minSunAngle.Value) > Game.ConditionProperties.GetSeconds(maxSunAngle.Value)) {
                SunAngleFrom = maxSunAngle;
                SunAngleTo = minSunAngle;
            } else {
                SunAngleFrom = minSunAngle;
                SunAngleTo = maxSunAngle;
            }

            CarId = reader.ReadString();
            DriverName = reader.ReadString();
            NationCode = reader.ReadString();
            DriverTeam = reader.ReadString();
            CarSkinId = reader.ReadString();

            const string postfix = "__AC_SHADERS_PATCH_v1__";
            reader.Seek(-postfix.Length - 8, SeekOrigin.End);
            if (Encoding.ASCII.GetString(reader.ReadBytes(postfix.Length)) == postfix) {
                var start = reader.ReadUInt32();
                var version = reader.ReadUInt32();
                if (version == 1) {
                    reader.Seek(start, SeekOrigin.Begin);

                    while (true) {
                        var nameLength = reader.ReadInt32();
                        if (nameLength > 255) break;

                        var name = Encoding.ASCII.GetString(reader.ReadBytes(nameLength));
                        // Logging.Debug("Extra section: " + name);

                        var sectionLength = reader.ReadInt32();
                        if (!ReadExtendedSection(reader, name, sectionLength)) {
                            reader.Skip(sectionLength);
                        }
                    }
                }
            }

            AllowToOverrideTime = CustomTime == null && WeatherManager.Instance.GetById(WeatherId)?.IsWeatherTimeUnusual() == true;
        }

        private bool ReadExtendedSection(ReplayReader reader, string name, int length) {
            if (name == @"CONFIG_RACE") {
                RaceIniConfig = Encoding.ASCII.GetString(reader.ReadBytes(length));
                CustomTime = Game.ConditionProperties.GetSeconds(
                        IniFile.Parse(RaceIniConfig)["LIGHTING"].GetDoubleNullable("__CM_UNCLAMPED_SUN_ANGLE")
                                ?? IniFile.Parse(RaceIniConfig)["LIGHTING"].GetDouble("SUN_ANGLE", 80d)).RoundToInt();
                return true;
            }

            return false;
        }

        private void ParseGeneric(int version, ReplayReader reader) {
            AllowToOverrideTime = false;

            if (version >= 14) {
                reader.Skip(8);

                WeatherId = reader.ReadString();
                /*if (!string.IsNullOrWhiteSpace(WeatherId)) {
                    ErrorIf(WeatherManager.Instance.GetWrapperById(WeatherId) == null,
                            AcErrorType.Replay_WeatherIsMissing, WeatherId);
                }*/

                TrackId = reader.ReadString();
                TrackConfiguration = reader.ReadString();
            } else {
                TrackId = reader.ReadString();
            }

            CarId = reader.TryToReadNextString();
            try {
                DriverName = reader.ReadString();
                reader.ReadInt64();
                CarSkinId = reader.ReadString();
            } catch (Exception) {
                // ignored
            }
        }

        private static readonly Dictionary<string, ReplayDetails> _cache = new Dictionary<string, ReplayDetails>();
        private static readonly List<Tuple<string, ReplayDetails>> _cacheToSave = new List<Tuple<string, ReplayDetails>>();

        [CanBeNull]
        public static ReplayDetails Load(string filename) {
            try {
                string cacheKey;
                using (var sha1 = SHA1.Create()) {
                    cacheKey = sha1.ComputeHash(Encoding.UTF8.GetBytes(filename)).ToHexString().ToLowerInvariant();
                }

                lock (_cache) {
                    if (_cache.TryGetValue(cacheKey, out var cached)) {
                        return cached;
                    }
                }

                var cacheFilename = FilesStorage.Instance.GetTemporaryFilename("Replay Details", cacheKey);
                if (File.Exists(cacheFilename)) {
                    try {
                        using (var file = File.Open(cacheFilename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) {
                            return JsonConvert.DeserializeObject<ReplayDetails>(
                                    new DeflateStream(file, CompressionMode.Decompress).ReadAsStringAndDispose());
                        }
                    } catch (Exception e) {
                        Logging.Warning(e);
                    }
                }

                var result = new ReplayDetails(filename);
                lock (_cache) {
                    if (_cache.Count == 0) {
                        RunCacheSavingQueueAsync().Ignore();
                    }
                    _cache[cacheKey] = result;
                }
                lock (_cacheToSave) {
                    _cacheToSave.Add(Tuple.Create(cacheFilename, result));
                }
                return result;
            } catch (Exception e) {
                Logging.Error(e);
                return null;
            }
        }

        private static async Task RunCacheSavingQueueAsync() {
            await Task.Delay(TimeSpan.FromSeconds(30d));
            while (_cache.Count > 0) {
                Tuple<string, ReplayDetails> itemToSave = null;
                lock (_cacheToSave) {
                    var count = _cacheToSave.Count;
                    if (count > 0) {
                        itemToSave = _cacheToSave[count - 1];
                        _cacheToSave.RemoveAt(count - 1);
                    }
                }

                if (itemToSave != null) {
                    using (var output = File.Create(itemToSave.Item1))
                    using (var gzip = new DeflateStream(output, CompressionMode.Compress)) {
                        gzip.WriteBytes(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(itemToSave.Item2)));
                    }
                    await Task.Delay(TimeSpan.FromSeconds(1d));
                } else {
                    await Task.Delay(TimeSpan.FromSeconds(10d));
                }
            }
        }
    }
}