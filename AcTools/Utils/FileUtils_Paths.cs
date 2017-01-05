using System;
using System.IO;
using AcTools.DataFile;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;

namespace AcTools.Utils {
    public partial class FileUtils {
        public static string GetDocumentsDirectory() {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Assetto Corsa");
        }

        public static string GetSystemCfgDirectory(string acRoot) {
            return Path.Combine(acRoot, "system", "cfg");
        }

        public static string GetDocumentsCfgDirectory() {
            return Path.Combine(GetDocumentsDirectory(), "cfg");
        }

        public static string GetReplaysDirectory() {
            return Path.Combine(GetDocumentsDirectory(), "replay");
        }

        public static string GetDocumentsOutDirectory() {
            return Path.Combine(GetDocumentsDirectory(), "out");
        }

        public static string GetCfgShowroomFilename() {
            return Path.Combine(GetDocumentsCfgDirectory(), "showroom_start.ini");
        }

        public static string GetCfgVideoFilename() {
            return Path.Combine(GetDocumentsCfgDirectory(), "video.ini");
        }

        [Obsolete]
        public static string GetDocumentsFiltersDirectory() {
            return Path.Combine(GetDocumentsCfgDirectory(), "filters");
        }

        public static string GetDocumentsScreensDirectory() {
            return Path.Combine(GetDocumentsDirectory(), "screens");
        }

        public static string GetCarsDirectory(string acRoot) {
            return Path.Combine(acRoot, "content", "cars");
        }

        public static string GetTracksDirectory(string acRoot) {
            return Path.Combine(acRoot, "content", "tracks");
        }

        public static string GetCarDirectory(string acRoot, string carName) {
            return Path.Combine(GetCarsDirectory(acRoot), carName);
        }

        public static string GetMainCarFilename(string carDir) {
            var iniFile = new IniFile(carDir, "lods.ini");
            if (!iniFile.IsEmptyOrDamaged()) {
                var fromData = iniFile["LOD_0"].GetNonEmpty("FILE");
                if (fromData != null) {
                    return Path.Combine(carDir, fromData);
                }
            }
            
            return Directory.GetFiles(carDir, "*.kn5").MaxEntryOrDefault(x => new FileInfo(x).Length);
        }

        public static string GetMainCarFilename(string carDir, DataWrapper data) {
            var iniFile = data.GetIniFile("lods.ini");
            if (!iniFile.IsEmptyOrDamaged()) {
                var fromData = iniFile["LOD_0"].GetNonEmpty("FILE");
                if (fromData != null) {
                    return Path.Combine(carDir, fromData);
                }
            }
            
            return Directory.GetFiles(carDir, "*.kn5").MaxEntryOrDefault(x => new FileInfo(x).Length);
        }

        public static string GetMainCarFilename(string acRoot, string carName) {
            return GetMainCarFilename(GetCarDirectory(acRoot, carName));
        }

        public static string GetCarSetupsDirectory() {
            return Path.Combine(GetDocumentsDirectory(), "setups");
        }

        public static string GetCarSetupsDirectory(string carName) {
            return Path.Combine(GetDocumentsDirectory(), "setups", carName);
        }

        public static string GetCarSkinsDirectory(string carDir) {
            return Path.Combine(carDir, "skins");
        }

        public static string GetCarSkinsDirectory(string acRoot, string carName) {
            return GetCarSkinsDirectory(GetCarDirectory(acRoot, carName));
        }
        
        public static string GetCarSkinDirectory(string acRoot, string carName, string skinName) {
            return Path.Combine(GetCarSkinsDirectory(acRoot, carName), skinName);
        }

        public static string GetShowroomsDirectory(string acRoot) {
            return Path.Combine(acRoot, "content", "showroom");
        }

        public static string GetFontsDirectory(string acRoot) {
            return Path.Combine(acRoot, "content", "fonts");
        }

        public static string GetWeatherDirectory(string acRoot) {
            return Path.Combine(acRoot, "content", "weather");
        }

        public static string GetPpFiltersDirectory(string acRoot) {
            return Path.Combine(acRoot, "system", "cfg", "ppfilters");
        }

        public static string GetPythonAppsDirectory(string acRoot) {
            return Path.Combine(acRoot, "apps", "python");
        }

        public static string GetKunosCareerDirectory(string acRoot) {
            return Path.Combine(acRoot, "content", "career");
        }

        public static string GetKunosCareerProgressFilename() {
            return Path.Combine(GetDocumentsDirectory(), "launcherdata", "filestore", "career.ini");
        }

        public static string GetShowroomDirectory(string acRoot, string showroomName) {
            return Path.Combine(GetShowroomsDirectory(acRoot), showroomName);
        }

        public static string GetAcLogoFilename(string acRoot) {
            return Path.Combine(acRoot, "content", "gui", "logo_ac_app.png");
        }

        public static string GetAcLauncherFilename(string acRoot) {
            return Path.Combine(acRoot, "AssettoCorsa.exe");
        }

        public static string GetLogFilename() {
            return Path.Combine(GetDocumentsDirectory(), "logs", "log.txt");
        }

        public static string GetLogFilename(string logFileName) {
            return Path.Combine(GetDocumentsDirectory(), "logs", logFileName);
        }

        public static string GetRaceIniFilename() {
            return Path.Combine(GetDocumentsCfgDirectory(), "race.ini");
        }

        public static string GetAssistsIniFilename() {
            return Path.Combine(GetDocumentsCfgDirectory(), "assists.ini");
        }

        public static string GetSfxDirectory(string acRoot) {
            return Path.Combine(acRoot, "content", "sfx");
        }

        public static string GetSfxGuidsFilename(string acRoot) {
            return Path.Combine(GetSfxDirectory(acRoot), "GUIDs.txt");
        }

        public static string GetResultJsonFilename() {
            return Path.Combine(GetDocumentsOutDirectory(), "race_out.json");
        }

        public static string[] Split(string s) {
            return s.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        public static bool ArePathsEqual(string pathA, string pathB) {
            return Path.GetFullPath(pathA).Equals(Path.GetFullPath(pathB), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// </summary>
        /// <param name="filename">Ex.: C:\Windows\System32\explorer.exe</param>
        /// <param name="directory">Ex.: C:\Windows</param>
        /// <returns>System32\explorer.exe</returns>
        [NotNull]
        public static string GetRelativePath([NotNull] string filename, [NotNull] string directory) {
            if (filename == null) throw new ArgumentNullException(nameof(filename));
            if (directory == null) throw new ArgumentNullException(nameof(directory));

            filename = filename.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            directory = directory.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

            if (!filename.StartsWith(directory, StringComparison.OrdinalIgnoreCase) || directory.Length == 0) return filename;

            var result = filename.Substring(directory[directory.Length - 1].IsDirectorySeparator() ? directory.Length - 1 : directory.Length);
            return result.Length == 0 ? string.Empty : !result[0].IsDirectorySeparator() ? filename : result.Substring(1);
        }

        /// <summary>
        /// </summary>
        /// <param name="filename">Ex.: C:\Windows\System32\explorer.exe</param>
        /// <param name="directory">Ex.: C:\Windows</param>
        /// <returns>System32\explorer.exe</returns>
        [CanBeNull]
        public static string TryToGetRelativePath([NotNull] string filename, [NotNull] string directory) {
            if (filename == null) throw new ArgumentNullException(nameof(filename));
            if (directory == null) throw new ArgumentNullException(nameof(directory));

            filename = filename.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            directory = directory.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

            if (directory.Length == 0) return filename;
            if (!filename.StartsWith(directory, StringComparison.OrdinalIgnoreCase)) return null;

            var result = filename.Substring(directory[directory.Length - 1].IsDirectorySeparator() ? directory.Length - 1 : directory.Length);
            return result.Length == 0 ? string.Empty : !result[0].IsDirectorySeparator() ? null : result.Substring(1);
        }
    }

    public static class CharExtension {
        public static bool IsDirectorySeparator(this char c) {
            return c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar;
        }
    }
}
