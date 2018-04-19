using System;
using System.IO;
using AcTools.DataFile;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;

namespace AcTools.Utils {
    public static class AcPaths {
        public static bool OptionEaseAcRootCheck;

        public static bool IsAcRoot([NotNull] string directory) {
            return Directory.Exists(Path.Combine(directory, "content", "cars")) && Directory.Exists(Path.Combine(directory, "apps"))
                    && (File.Exists(Path.Combine(directory, "acs.exe")) || File.Exists(Path.Combine(directory, "acs_pro.exe")));
        }

        [NotNull, Pure]
        public static string GetDocumentsDirectory() {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Assetto Corsa");
        }

        [NotNull, Pure]
        public static string GetSystemCfgDirectory([NotNull] string acRoot) {
            return Path.Combine(acRoot, "system", "cfg");
        }

        [NotNull, Pure]
        public static string GetDocumentsCfgDirectory() {
            return Path.Combine(GetDocumentsDirectory(), "cfg");
        }

        [NotNull, Pure]
        public static string GetReplaysDirectory() {
            return Path.Combine(GetDocumentsDirectory(), "replay");
        }

        [NotNull, Pure]
        public static string GetDocumentsOutDirectory() {
            return Path.Combine(GetDocumentsDirectory(), "out");
        }

        [NotNull, Pure]
        public static string GetCfgShowroomFilename() {
            return Path.Combine(GetDocumentsCfgDirectory(), "showroom_start.ini");
        }

        [NotNull, Pure]
        public static string GetCfgVideoFilename() {
            return Path.Combine(GetDocumentsCfgDirectory(), "video.ini");
        }

        [NotNull, Pure]
        public static string GetCfgAppsFilename() {
            return Path.Combine(GetDocumentsCfgDirectory(), "python.ini");
        }

        [NotNull, Pure]
        public static string GetCfgControlsFilename() {
            return Path.Combine(GetDocumentsCfgDirectory(), "controls.ini");
        }

        [NotNull, Pure]
        public static string GetDocumentsScreensDirectory() {
            return Path.Combine(GetDocumentsDirectory(), "screens");
        }

        [NotNull, Pure]
        public static string GetCarsDirectory([NotNull] string acRoot) {
            return Path.Combine(acRoot, "content", "cars");
        }

        [NotNull, Pure]
        public static string GetTracksDirectory([NotNull] string acRoot) {
            return Path.Combine(acRoot, "content", "tracks");
        }

        [NotNull, Pure]
        public static string GetCarDirectory([NotNull] string acRoot, [NotNull] string carName) {
            return Path.Combine(GetCarsDirectory(acRoot), carName);
        }

        [CanBeNull, Pure]
        public static string GetMainCarFilename([NotNull] string carDir) {
            return GetMainCarFilename(carDir, (DataWrapper)null);
        }

        [CanBeNull, Pure]
        public static string GetMainCarFilename([NotNull] string carDir, [CanBeNull] DataWrapper data) {
            var iniFile = (data ?? DataWrapper.FromCarDirectory(carDir)).GetIniFile("lods.ini");
            if (!iniFile.IsEmptyOrDamaged()) {
                var fromData = iniFile["LOD_0"].GetNonEmpty("FILE");
                if (fromData != null) {
                    return Path.Combine(carDir, fromData);
                }
            }

            return Directory.GetFiles(carDir, "*.kn5").MaxEntryOrDefault(x => new FileInfo(x).Length);
        }

        [CanBeNull, Pure]
        public static string GetMainCarFilename([NotNull] string acRoot, [NotNull] string carName) {
            return GetMainCarFilename(GetCarDirectory(acRoot, carName));
        }

        [NotNull, Pure]
        public static string GetCarSetupsDirectory() {
            return Path.Combine(GetDocumentsDirectory(), "setups");
        }

        [NotNull, Pure]
        public static string GetCarSetupsDirectory([NotNull] string carName) {
            return Path.Combine(GetDocumentsDirectory(), "setups", carName);
        }

        [NotNull, Pure]
        public static string GetCarSkinsDirectory([NotNull] string carDir) {
            return Path.Combine(carDir, "skins");
        }

        [NotNull, Pure]
        public static string GetCarSkinsDirectory([NotNull] string acRoot, [NotNull] string carName) {
            return GetCarSkinsDirectory(GetCarDirectory(acRoot, carName));
        }

        [NotNull, Pure]
        public static string GetCarSkinDirectory([NotNull] string acRoot, [NotNull] string carName, [NotNull] string skinName) {
            return Path.Combine(GetCarSkinsDirectory(acRoot, carName), skinName);
        }

        [NotNull, Pure]
        public static string GetShowroomsDirectory([NotNull] string acRoot) {
            return Path.Combine(acRoot, "content", "showroom");
        }

        [NotNull, Pure]
        public static string GetFontsDirectory([NotNull] string acRoot) {
            return Path.Combine(acRoot, "content", "fonts");
        }

        [NotNull, Pure]
        public static string GetWeatherDirectory([NotNull] string acRoot) {
            return Path.Combine(acRoot, "content", "weather");
        }

        [NotNull, Pure]
        public static string GetPpFiltersDirectory([NotNull] string acRoot) {
            return Path.Combine(acRoot, "system", "cfg", "ppfilters");
        }

        [NotNull, Pure]
        public static string GetDriverModelsDirectory([NotNull] string acRoot) {
            return Path.Combine(acRoot, "content", "driver");
        }

        [NotNull, Pure]
        public static string GetPythonAppsDirectory([NotNull] string acRoot) {
            return Path.Combine(acRoot, "apps", "python");
        }

        [NotNull, Pure]
        public static string GetKunosCareerDirectory([NotNull] string acRoot) {
            return Path.Combine(acRoot, "content", "career");
        }

        [NotNull, Pure]
        public static string GetKunosCareerProgressFilename() {
            return Path.Combine(GetDocumentsDirectory(), "launcherdata", "filestore", "career.ini");
        }

        [NotNull, Pure]
        public static string GetShowroomDirectory([NotNull] string acRoot, [NotNull] string showroomName) {
            return Path.Combine(GetShowroomsDirectory(acRoot), showroomName);
        }

        [NotNull, Pure]
        public static string GetAcLogoFilename([NotNull] string acRoot) {
            return Path.Combine(acRoot, "content", "gui", "logo_ac_app.png");
        }

        [NotNull, Pure]
        public static string GetAcLauncherFilename([NotNull] string acRoot) {
            return Path.Combine(acRoot, "AssettoCorsa.exe");
        }

        [NotNull, Pure]
        public static string GetLogFilename() {
            return Path.Combine(GetDocumentsDirectory(), "logs", "log.txt");
        }

        [NotNull, Pure]
        public static string GetLogFilename(string logFileName) {
            return Path.Combine(GetDocumentsDirectory(), "logs", logFileName);
        }

        [NotNull, Pure]
        public static string GetRaceIniFilename() {
            return Path.Combine(GetDocumentsCfgDirectory(), "race.ini");
        }

        [NotNull, Pure]
        public static string GetAssistsIniFilename() {
            return Path.Combine(GetDocumentsCfgDirectory(), "assists.ini");
        }

        [NotNull, Pure]
        public static string GetSfxDirectory([NotNull] string acRoot) {
            return Path.Combine(acRoot, "content", "sfx");
        }

        [NotNull, Pure]
        public static string GetSfxGuidsFilename([NotNull] string acRoot) {
            return Path.Combine(GetSfxDirectory(acRoot), "GUIDs.txt");
        }

        [NotNull, Pure]
        public static string GetGuiIconsFilename([NotNull] string acRoot) {
            return Path.Combine(acRoot, "content", "gui", "icons");
        }

        [NotNull, Pure]
        public static string GetResultJsonFilename() {
            return Path.Combine(GetDocumentsOutDirectory(), "race_out.json");
        }
    }
}