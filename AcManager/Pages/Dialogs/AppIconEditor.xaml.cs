using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using AcManager.Controls.Helpers;
using AcManager.Tools.Helpers;
using AcManager.Tools.Objects;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;
using Microsoft.Win32;

namespace AcManager.Pages.Dialogs {
    public partial class AppIconEditor {
        private ViewModel Model => (ViewModel)DataContext;

        private AppIconEditor(PythonAppObject app) {
            DataContext = new ViewModel(app);
            InitializeComponent();
            Buttons = new[] {
                OkButton,
                CancelButton
            };

            Closing += OnClosing;
        }

        public static async Task RunAsync([NotNull] PythonAppObject app) {
            IReadOnlyList<PythonAppWindow> list;
            using (WaitingDialog.Create("Searching for app windows…")) {
                list = await app.Windows.GetValueAsync();
            }

            if (list == null || list.Count == 0) {
                ShowMessage(
                        "No app windows found. You can add lines like “# app window: Window Name” to your code to let CM figure out their names ",
                        "No app windows found", MessageBoxButton.OK);
                return;
            }

            await new AppIconEditor(app).ShowDialogAsync();
        }

        private void OnClosing(object sender, CancelEventArgs e) {
            if (IsResultOk) {
                Model.Apply();
            }
        }

        public sealed class AppWindowItem : NotifyPropertyChanged {
            public PythonAppWindow Window { get; }

            public AppWindowItem(PythonAppWindow window) {
                Window = window;
            }

            private string _iconOriginal;

            public string IconOriginal {
                get => _iconOriginal;
                set {
                    if (Equals(value, _iconOriginal)) return;
                    _iconOriginal = value;
                    OnPropertyChanged();
                }
            }

            public void Update(bool showEnabled) {
                IconOriginal = showEnabled ? Window.IconOn : Window.IconOff;
            }

        }

        private class ViewModel : NotifyPropertyChanged {
            public PythonAppObject App { get; }
            public List<AppWindowItem> Windows { get; }

            public StoredValue<bool> ShowEnabled { get; }

            public ViewModel(PythonAppObject app) {
                ShowEnabled = Stored.Get("AppIconEditor.ShowEnabled", false)
                                    .SubscribeWeak((s, e) => UpdateWindowsShowEnabled());

                App = app;
                Windows = app.Windows.GetValueAsync().Result?.Select(x =>new AppWindowItem(x)).ToList() ?? new List<AppWindowItem>();
                UpdateWindowsShowEnabled();
            }

            private void UpdateWindowsShowEnabled() {
                Windows.ForEach(x => x.Update(ShowEnabled.Value));
            }

            public void Apply() {}
        }
    }
}
