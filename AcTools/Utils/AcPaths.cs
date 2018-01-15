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
                    && File.Exists(Path.Combine(directory, "acs.exe"));
        }

        [NotNull, Pure]
        public static string GetDocumentsDirectory() {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Assetto Corsa");
        }

        [NotNull, Pure]
        public static string GetSystemCfgDirectory(string acRoot) {
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
        public static string GetCarsDirectory(string acRoot) {
            return Path.Combine(acRoot, "content", "cars");
        }

        [NotNull, Pure]
        public static string GetTracksDirectory(string acRoot) {
            return Path.Combine(acRoot, "content", "tracks");
        }

        [NotNull, Pure]
        public static string GetCarDirectory(string acRoot, string carName) {
            return Path.Combine(GetCarsDirectory(acRoot), carName);
        }

        [CanBeNull, Pure]
        public static string GetMainCarFilename(string carDir) {
            return GetMainCarFilename(carDir, (DataWrapper)null);
        }

        [CanBeNull, Pure]
        public static string GetMainCarFilename(string carDir, [CanBeNull] DataWrapper data) {
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
        public static string GetMainCarFilename(string acRoot, string carName) {
            return GetMainCarFilename(GetCarDirectory(acRoot, carName));
        }

        [NotNull, Pure]
        public static string GetCarSetupsDirectory() {
            return Path.Combine(GetDocumentsDirectory(), "setups");
        }

        [NotNull, Pure]
        public static string GetCarSetupsDirectory(string carName) {
            return Path.Combine(GetDocumentsDirectory(), "setups", carName);
        }

        [NotNull, Pure]
        public static string GetCarSkinsDirectory(string carDir) {
            return Path.Combine(carDir, "skins");
        }

        [NotNull, Pure]
        public static string GetCarSkinsDirectory(string acRoot, string carName) {
            return GetCarSkinsDirectory(GetCarDirectory(acRoot, carName));
        }

        [NotNull, Pure]
        public static string GetCarSkinDirectory(string acRoot, string carName, string skinName) {
            return Path.Combine(GetCarSkinsDirectory(acRoot, carName), skinName);
        }

        [NotNull, Pure]
        public static string GetShowroomsDirectory(string acRoot) {
            return Path.Combine(acRoot, "content", "showroom");
        }

        [NotNull, Pure]
        public static string GetFontsDirectory(string acRoot) {
            return Path.Combine(acRoot, "content", "fonts");
        }

        [NotNull, Pure]
        public static string GetWeatherDirectory(string acRoot) {
            return Path.Combine(acRoot, "content", "weather");
        }

        [NotNull, Pure]
        public static string GetPpFiltersDirectory(string acRoot) {
            return Path.Combine(acRoot, "system", "cfg", "ppfilters");
        }

        [NotNull, Pure]
        public static string GetDriverModelsDirectory(string acRoot) {
            return Path.Combine(acRoot, "content", "driver");
        }

        [NotNull, Pure]
        public static string GetPythonAppsDirectory(string acRoot) {
            return Path.Combine(acRoot, "apps", "python");
        }

        [NotNull, Pure]
        public static string GetKunosCareerDirectory(string acRoot) {
            return Path.Combine(acRoot, "content", "career");
        }

        [NotNull, Pure]
        public static string GetKunosCareerProgressFilename() {
            return Path.Combine(GetDocumentsDirectory(), "launcherdata", "filestore", "career.ini");
        }

        [NotNull, Pure]
        public static string GetShowroomDirectory(string acRoot, string showroomName) {
            return Path.Combine(GetShowroomsDirectory(acRoot), showroomName);
        }

        [NotNull, Pure]
        public static string GetAcLogoFilename(string acRoot) {
            return Path.Combine(acRoot, "content", "gui", "logo_ac_app.png");
        }

        [NotNull, Pure]
        public static string GetAcLauncherFilename(string acRoot) {
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
        public static string GetSfxDirectory(string acRoot) {
            return Path.Combine(acRoot, "content", "sfx");
        }

        [NotNull, Pure]
        public static string GetSfxGuidsFilename(string acRoot) {
            return Path.Combine(GetSfxDirectory(acRoot), "GUIDs.txt");
        }

        [NotNull, Pure]
        public static string GetGuiIconsFilename(string acRoot) {
            return Path.Combine(acRoot, "content", "gui", "icons");
        }

        [NotNull, Pure]
        public static string GetResultJsonFilename() {
            return Path.Combine(GetDocumentsOutDirectory(), "race_out.json");
        }
    }
}