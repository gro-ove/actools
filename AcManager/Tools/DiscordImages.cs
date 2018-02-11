using System;
using System.IO;
using AcManager.DiscordRpc;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AcManager.Tools {
    public static class DiscordImages {
        private static string[] _knownCarBrands;
        private static string[] _knownMiscellaneous;
        private static string[] _knownTracks;

        private static void InitializeKnownIds() {
            if (_knownCarBrands != null) return;
            _knownCarBrands = ReadIds("CarBrands");
            _knownMiscellaneous = ReadIds("Miscellaneous");
            _knownTracks = ReadIds("Tracks");

            string[] ReadIds(string name) {
                var file = FilesStorage.Instance.GetContentFile("Discord", $"{name}.json");
                if (file.Exists) {
                    try {
                        return JsonConvert.DeserializeObject<string[]>(File.ReadAllText(file.Filename));
                    } catch (Exception e) {
                        Logging.Error(e);
                    }
                }

                return new string[0];
            }
        }

        public static DiscordRichPresence Default([NotNull] this DiscordRichPresence presence, string comment = null) {
            InitializeKnownIds();
            var random = _knownMiscellaneous.RandomElementOrDefault() ?? "0";
            presence.LargeImage = new DiscordImage($@"misc_{random}", comment ?? "");
            return presence;
        }

        public static DiscordRichPresence Car([NotNull] this DiscordRichPresence presence, [CanBeNull] CarObject car) {
            InitializeKnownIds();

            if (car != null) {
                var carBrand = car.Brand?.ToLowerInvariant().Replace(" ", "_");
                if (!_knownCarBrands.ArrayContains(carBrand)) {
                    carBrand = "various";
                }

                presence.SmallImage = new DiscordImage($@"car_{carBrand}", car.Name ?? car.Id);
            }

            return presence;
        }

        public static DiscordRichPresence Track([NotNull] this DiscordRichPresence presence, [CanBeNull] TrackObjectBase track) {
            InitializeKnownIds();

            if (track != null) {
                var trackId = track.MainTrackObject.Id.ToLowerInvariant();
                if (!_knownTracks.ArrayContains(trackId)) {
                    return presence.Default(track.Name ?? track.Id);
                }

                presence.LargeImage = new DiscordImage($@"track_{trackId}", track.Name ?? track.Id);
            }

            return presence;
        }

        public static DiscordRichPresence Car([NotNull] this DiscordRichPresence presence, [CanBeNull] string carId) {
            return presence.Car(CarsManager.Instance.GetById(carId ?? ""));
        }

        public static DiscordRichPresence Track([NotNull] this DiscordRichPresence presence, [CanBeNull] string trackId, [CanBeNull] string trackLayoutId) {
            return presence.Track(TracksManager.Instance.GetLayoutById(trackId ?? "", trackLayoutId));
        }

        public static DiscordRichPresence Track([NotNull] this DiscordRichPresence presence, [CanBeNull] string trackId) {
            return presence.Track(TracksManager.Instance.GetLayoutById(trackId ?? ""));
        }

        public static DiscordRichPresence Now([NotNull] this DiscordRichPresence presence) {
            presence.Start = DateTime.Now;
            return presence;
        }

        public static DiscordRichPresence Details([NotNull] this DiscordRichPresence presence, string details, string detailsParam = null) {
            presence.Details = detailsParam == null ? details : $"{details} | {detailsParam}";
            return presence;
        }
    }
}