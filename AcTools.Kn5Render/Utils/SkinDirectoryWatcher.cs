using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AcTools.Kn5Render.Utils {
    public class SkinDirectoryWatcher : IDisposable {
        public readonly string DirectoryName;
        public event SkinUpdatedEventHandler Update;

        private FileSystemWatcher _watcher;

        public SkinDirectoryWatcher(string directory) {
            DirectoryName = directory;

            _watcher = new FileSystemWatcher {
                Filter = "*.*",
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
                Path = directory
            };
            
            _watcher.Changed += OnChanged;
            _watcher.Created += OnChanged;
            _watcher.Deleted += OnChanged;
            _watcher.Renamed += OnRenamed;
            _watcher.EnableRaisingEvents = true;
        }

        private void OnChanged(object sender, FileSystemEventArgs e) {
            var handler = Update;
            if (handler != null) {
                handler(this, new SkinUpdatedEventHandlerArgs(e.FullPath));
            }
        }

        private void OnRenamed(object sender, RenamedEventArgs e) {
            var handler = Update;
            if (handler == null) return;

            if (DirectoryName == Path.GetDirectoryName(e.OldFullPath)) {
                handler(this, new SkinUpdatedEventHandlerArgs(e.OldFullPath));
            }

            if (DirectoryName == Path.GetDirectoryName(e.FullPath)) {
                handler(this, new SkinUpdatedEventHandlerArgs(e.FullPath));
            }
        }

        public void Dispose() {
            if (_watcher == null) return;
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
            _watcher = null;
        }
    }

    public delegate void SkinUpdatedEventHandler(object sender, SkinUpdatedEventHandlerArgs args);

    public class SkinUpdatedEventHandlerArgs {
        public readonly string TextureName, TextureFilename;

        internal SkinUpdatedEventHandlerArgs(string filename) {
            TextureFilename = filename;
            TextureName = Path.GetFileName(filename);
        }
    }
}
