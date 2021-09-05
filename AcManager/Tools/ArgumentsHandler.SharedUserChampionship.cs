using System;
using System.Collections.Specialized;
using System.IO;
using System.Windows;
using AcManager.Controls.Dialogs;
using AcManager.Pages.Drive;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Managers;
using AcManager.Tools.Managers.Directories;
using AcManager.Tools.Miscellaneous;
using AcManager.Tools.Objects;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using Newtonsoft.Json.Linq;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace AcManager.Tools {
    public static partial class ArgumentsHandler {
        private class UserChampionshipInformation : IUserChampionshipInformation {
            public string Name { get; set; }

            public string PreviewImage { get; set; }

            public string Description { get; set; }

            public string Author { get; set; }

            public string Difficulty { get; set; }
        }

        private static ArgumentHandleResult ProcessSharedUserChampionshipExt(SharedEntry shared, byte[] data, bool justGo) {
            byte[] mainData = null, extraData = null, previewImage = null;

            string sharedId = null;
            using (var stream = new MemoryStream(data)) {
                var reader = ReaderFactory.Open(stream);

                var written = 0;
                try {
                    while (reader.MoveToNextEntry()) {
                        if (!reader.Entry.IsDirectory) {
                            var name = reader.Entry.Key;
                            if (name.EndsWith(UserChampionshipObject.FileExtension)) {
                                sharedId = name;
                                mainData = reader.OpenEntryStream().ReadAsBytesAndDispose();
                            } else if (name.EndsWith(UserChampionshipObject.FileDataExtension)) {
                                extraData = reader.OpenEntryStream().ReadAsBytesAndDispose();
                            } else if (name.EndsWith(UserChampionshipObject.FilePreviewExtension)) {
                                previewImage = reader.OpenEntryStream().ReadAsBytesAndDispose();
                            }

                            written++;
                        }
                    }
                } catch (EndOfStreamException) {
                    if (written < 1) {
                        throw;
                    }
                }
            }

            if (sharedId == null || mainData == null) {
                throw new InformativeException("Can’t install championship", "Main file is missing.");
            }

            var mainDataJson = JObject.Parse(mainData.ToUtf8String());
            var extraDataJson = extraData == null ? null : JObject.Parse(extraData.ToUtf8String());

            var information = new UserChampionshipInformation {
                Name = mainDataJson.GetStringValueOnly("name"),
                Description = extraDataJson?.GetStringValueOnly("description"),
                Author = extraDataJson?.GetStringValueOnly("author"),
                Difficulty = extraDataJson?.GetStringValueOnly("difficulty"),
            };

            if (previewImage != null) {
                var temp = FileUtils.GetTempFileName(Path.GetTempPath(), @".jpg");
                File.WriteAllBytes(temp, previewImage);
                information.PreviewImage = temp;
            }

            var existing = UserChampionshipsManager.Instance.GetById(sharedId);
            var dialog = new UserChampionshipIntro(information, existing == null ? UserChampionshipIntroMode.InstallationPreview :
                UserChampionshipIntroMode.InstallationAlreadyExistingPreview, existing?.Name);
            dialog.ShowDialog();

            switch (dialog.MessageBoxResult) {
                case MessageBoxResult.OK:
                case MessageBoxResult.Yes:
                    string replacementId = null;
                    if (existing != null && dialog.MessageBoxResult == MessageBoxResult.OK) {
                        for (var i = 0; i < 999; i++) {
                            var candidate = Guid.NewGuid() + UserChampionshipObject.FileExtension;
                            if (UserChampionshipsManager.Instance.GetById(candidate) == null) {
                                replacementId = candidate;
                                break;
                            }
                        }

                        if (replacementId == null) {
                            throw new InformativeException("Can’t install championship", "Can’t find a new ID.");
                        }
                    }

                    var directory = UserChampionshipsManager.Instance.Directories.GetMainDirectory();
                    Directory.CreateDirectory(directory);

                    if (existing == null || replacementId != null) {
                        _openId = replacementId ?? sharedId;
                        UserChampionshipsManager.Instance.WrappersList.CollectionChanged += OnUserChampionshipsManagerCollectionChanged;
                    } else {
                        UserChampionships.NavigateToChampionshipPage(UserChampionshipsManager.Instance.GetById(sharedId));
                    }

                    using (var stream = new MemoryStream(data)) {
                        var reader = ReaderFactory.Open(stream);

                        var written = 0;
                        try {
                            while (reader.MoveToNextEntry()) {
                                if (!reader.Entry.IsDirectory) {
                                    var name = reader.Entry.Key;
                                    if (replacementId != null) {
                                        name = name.Replace(sharedId, replacementId);
                                    }

                                    reader.WriteEntryToFile(Path.Combine(directory, name), new ExtractionOptions {
                                        Overwrite = true
                                    });
                                    written++;
                                }
                            }
                        } catch (EndOfStreamException) {
                            if (written < 1) {
                                throw;
                            }
                        }
                    }
                    return ArgumentHandleResult.SuccessfulShow;
                default:
                    return ArgumentHandleResult.Failed;
            }
        }

        private static string _openId;

        private static void OnUserChampionshipsManagerCollectionChanged(object sender, NotifyCollectionChangedEventArgs e) {
            if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems.Count == 1 && (e.NewItems[0] as AcItemWrapper)?.Id == _openId) {
                UserChampionshipsManager.Instance.WrappersList.CollectionChanged -= OnUserChampionshipsManagerCollectionChanged;

                if (_openId != null) {
                    UserChampionships.NavigateToChampionshipPage(UserChampionshipsManager.Instance.GetById(_openId));
                    UserChampionships_SelectedPage.IgnoreIntro(_openId);
                }
            }
        }

        private static ArgumentHandleResult ProcessSharedUserChampionship(SharedEntry shared, byte[] data, bool justGo) {
            string sharedId = null;
            using (var stream = new MemoryStream(data)) {
                var reader = ReaderFactory.Open(stream);

                var written = 0;
                try {
                    while (reader.MoveToNextEntry()) {
                        if (!reader.Entry.IsDirectory) {
                            var name = reader.Entry.Key;
                            if (name.EndsWith(UserChampionshipObject.FileExtension)) {
                                sharedId = name;
                            }

                            written++;
                        }
                    }
                } catch (EndOfStreamException) {
                    if (written < 1) {
                        throw;
                    }
                }
            }

            if (sharedId == null) {
                throw new InformativeException("Can’t install championship", "Main file is missing.");
            }

            var existing = UserChampionshipsManager.Instance.GetById(sharedId);
            var result = ShowDialog(shared, justGo, applyable: false, additionalButton: existing == null ? null : $"Overwrite “{existing.Name}”");
            switch (result) {
                case Choise.Save:
                case Choise.Extra:
                    string replacementId = null;
                    if (existing != null && result == Choise.Save) {
                        for (var i = 0; i < 999; i++) {
                            var candidate = Guid.NewGuid() + UserChampionshipObject.FileExtension;
                            if (UserChampionshipsManager.Instance.GetById(candidate) == null) {
                                replacementId = candidate;
                                break;
                            }
                        }

                        if (replacementId == null) {
                            throw new InformativeException("Can’t install championship", "Can’t find a new ID.");
                        }
                    }

                    var directory = UserChampionshipsManager.Instance.Directories.GetMainDirectory();
                    Directory.CreateDirectory(directory);

                    using (var stream = new MemoryStream(data)) {
                        var reader = ReaderFactory.Open(stream);

                        var written = 0;
                        try {
                            while (reader.MoveToNextEntry()) {
                                if (!reader.Entry.IsDirectory) {
                                    var name = reader.Entry.Key;
                                    if (replacementId != null) {
                                        name = name.Replace(sharedId, replacementId);
                                    }

                                    reader.WriteEntryToFile(Path.Combine(directory, name), new ExtractionOptions {
                                        Overwrite = true
                                    });
                                    written++;
                                }
                            }
                        } catch (EndOfStreamException) {
                            if (written < 1) {
                                throw;
                            }
                        }
                    }

                    return ArgumentHandleResult.SuccessfulShow;
                default:
                    return ArgumentHandleResult.Failed;
            }
        }
    }
}