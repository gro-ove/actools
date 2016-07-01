using System;
using System.Linq;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers.Directories;
using AcManager.Tools.Objects;

namespace AcManager.Tools.Managers {
    public class ReplaysManager : AcManagerFileSpecific<ReplayObject> {
        public static ReplaysManager Instance { get; private set; }

        public static ReplaysManager Initialize() {
            if (Instance != null) throw new Exception("already initialized");
            return Instance = new ReplaysManager();
        }

        private ReplaysManager() {
            SettingsHolder.Drive.PropertyChanged += Drive_PropertyChanged;
        }

        private static readonly string[] Ignored = {
            ".7z", ".accdb", ".acd", ".ai", ".aif", ".apk", ".app", ".asf", ".asp", ".aspx", ".avi", ".bak", ".bat", ".bin", ".bmp", ".cab", ".cbr", ".cer",
            ".cfg", ".cfm", ".cgi", ".class", ".com", ".cpl", ".cpp", ".crdownload", ".crx", ".cs", ".csr", ".css", ".csv", ".cue", ".cur", ".dat", ".db",
            ".dbf", ".dds", ".deb", ".dem", ".deskthemepack", ".dll", ".dmg", ".dmp", ".doc", ".docx", ".drv", ".dtd", ".dwg", ".dxf", ".eps", ".exe", ".fla",
            ".flv", ".fnt", ".fon", ".gadget", ".gam", ".ged", ".gif", ".gpx", ".gz", ".hqx", ".htm", ".html", ".icns", ".ico", ".ics", ".iff", ".indd", ".ini",
            ".iso", ".jar", ".java", ".jpeg", ".jpg", ".js", ".jsp", ".key", ".keychain", ".kml", ".kmz", ".kn5", ".lnk", ".log", ".lua", ".max", ".mdb", ".mdf",
            ".mid", ".mim", ".mov", ".mp", ".mp3", ".mp4", ".mpa", ".mpg", ".msg", ".msi", ".nes", ".obj", ".odt", ".otf", ".pages", ".part", ".pct", ".pdb",
            ".pdf", ".php", ".pkg", ".pl", ".plugin", ".png", ".pps", ".ppt", ".pptx", ".prf", ".ps", ".psd", ".pspimage", ".py", ".rar", ".rm", ".rom", ".rpm",
            ".rss", ".rtf", ".sav", ".sdf", ".sh", ".sitx", ".sln", ".sql", ".srt", ".svg", ".swf", ".swift", ".sys", ".tar", ".tax", ".tex", ".tga", ".thm",
            ".tif", ".tiff", ".tmp", ".toast", ".torrent", ".ttf", ".txt", ".uue", ".vb", ".vcd", ".vcf", ".vcxproj", ".vob", ".wav", ".wma", ".wmv", ".wpd",
            ".wps", ".wsf", ".xcodeproj", ".xhtml", ".xlr", ".xls", ".xlsx", ".xml", ".yuv", ".zip", ".zipx"
        };

        protected override bool Filter(string filename) => !Ignored.Any(x => filename.EndsWith(x, StringComparison.OrdinalIgnoreCase));

        private void Drive_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
            if (e.PropertyName != nameof(SettingsHolder.DriveSettings.TryToLoadReplays)) return;
            Rescan();
        }

        public override IAcDirectories Directories => AcRootDirectory.Instance.ReplaysDirectories;

        protected override ReplayObject CreateAcObject(string id, bool enabled) {
            return new ReplayObject(this, id, enabled);
        }
    }
}
