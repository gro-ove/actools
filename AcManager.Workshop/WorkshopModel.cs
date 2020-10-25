using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.Helpers;
using AcManager.Workshop.Data;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AcManager.Workshop {
    public class WorkshopModel : NotifyPropertyChanged {
        private const string KeySteamId = "w/ui";
        private const string KeyUserPassword = "w/up";

        [NotNull]
        private readonly WorkshopClient _client;

        public WorkshopModel([NotNull] WorkshopClient client) {
            _client = client ?? throw new ArgumentNullException(nameof(client));

            if (ValuesStorage.GetEncrypted<string>(KeySteamId) == null) {
                ValuesStorage.SetEncrypted(KeySteamId, SteamIdHelper.Instance.Value);
            }

            var steamId = ValuesStorage.GetEncrypted<string>(KeySteamId);
            AuthorizeAsync(steamId, ValuesStorage.GetEncrypted(KeyUserPassword,
                    WorkshopClient.GetPasswordChecksum(WorkshopClient.GetUserId(steamId), string.Empty))).Ignore();
        }

        private bool _isAuthorizing;

        public bool IsAuthorizing {
            get => _isAuthorizing;
            set => Apply(value, ref _isAuthorizing, () => _authorizeCommand?.RaiseCanExecuteChanged());
        }

        private string _steamId;

        public string SteamId {
            get => _steamId ?? (_steamId = SteamIdHelper.Instance.Value);
            set => Apply(value, ref _steamId, () => _authorizeCommand?.RaiseCanExecuteChanged());
        }

        private string GetDisplayError(IEnumerable<string> message) {
            return $"• {message.NonNull().JoinToString(";\n• ")}.";
        }

        private async Task TryAsync(Func<Task> fn) {
            IsWorkshopAvailable = true;
            try {
                await fn();
            } catch (Exception e) when (e.IsCancelled()) {
                // Do nothing
            } catch (HttpRequestException e) {
                Logging.Warning(e);
                IsWorkshopAvailable = false;
                LastError = GetDisplayError(e.FlattenMessage());
            } catch (WorkshopException e) {
                Logging.Warning(e);
                LastError = GetDisplayError(new[] {
                    $"CM Workshop returned error {(int)e.Code} ({e.Message})",
                    e.RemoteException == null ? null
                            : e.RemoteStackTrace != null
                                    ? $"{e.RemoteException}\n\t{e.RemoteStackTrace.Take(4).JoinToString("\n\t")}"
                                    : $"{e.RemoteException}"
                });
            } catch (Exception e) {
                Logging.Warning(e);
                LastError = GetDisplayError(e.FlattenMessage());
            }
        }

        private bool _isWorkshopAvailable = true;

        public bool IsWorkshopAvailable {
            get => _isWorkshopAvailable;
            set => Apply(value, ref _isWorkshopAvailable);
        }

        private UserInfo _loggedInAs;

        public UserInfo LoggedInAs {
            get => _loggedInAs;
            set => Apply(value, ref _loggedInAs, () => {
                OnPropertyChanged(nameof(IsAuthorized));
                OnPropertyChanged(nameof(IsAccountVirtual));
                OnPropertyChanged(nameof(IsAbleToUploadContent));
            });
        }

        public bool IsAuthorized => _loggedInAs != null;
        public bool IsAccountVirtual => _loggedInAs?.IsVirtual == true;
        public bool IsAbleToUploadContent => _loggedInAs?.IsVirtual == false;

        private Task AuthorizeAsync(string steamId, string userPasswordChecksum) {
            return TryAsync(async () => {
                if (IsAuthorizing) {
                    throw new Exception("Already trying to authorize");
                }

                var userId = WorkshopClient.GetUserId(steamId);
                // TODO:
                //ValuesStorage.SetEncrypted(KeySteamId, steamId);
                //ValuesStorage.SetEncrypted(KeyUserPassword, userPasswordChecksum);

                IsAuthorizing = true;
                try {
                    _client.UserId = userId;
                    _client.UserPasswordChecksum = userPasswordChecksum;
                    LoggedInAs = await _client.PutAsync<object, UserInfo>($"/users/{userId}", null);
                    // Reapplying values in case there is another Authorize request happening (which shouldn’t occur, but if
                    // it does, we don’t need a broken state)
                    _client.UserPasswordChecksum = userPasswordChecksum;
                    SteamId = steamId;
                } catch {
                    _client.UserPasswordChecksum = null;
                    LoggedInAs = null;
                    throw;
                } finally {
                    _client.UserId = userId;
                    IsAuthorizing = false;
                }
            });
        }

        private string _lastError;

        public string LastError {
            get => _lastError;
            set => Apply(value, ref _lastError);
        }

        private AsyncCommand _refreshCommand;

        public AsyncCommand RefreshCommand => _refreshCommand ?? (_refreshCommand = new AsyncCommand(
                () => AuthorizeAsync(SteamId, WorkshopClient.GetPasswordChecksum(WorkshopClient.GetUserId(SteamId),
                        ValuesStorage.GetEncrypted(KeyUserPassword, string.Empty))),
                () => !string.IsNullOrEmpty(SteamId) && !IsAuthorizing));

        private AsyncCommand<string> _authorizeCommand;

        public AsyncCommand<string> AuthorizeCommand => _authorizeCommand ?? (_authorizeCommand = new AsyncCommand<string>(
                password => AuthorizeAsync(SteamId, WorkshopClient.GetPasswordChecksum(WorkshopClient.GetUserId(SteamId), password)),
                password => !string.IsNullOrEmpty(SteamId) && !IsAuthorizing && password != null));

        private DelegateCommand _logOutCommand;

        public DelegateCommand LogOutCommand => _logOutCommand ?? (_logOutCommand = new DelegateCommand(() => {
            _client.UserPasswordChecksum = null;
            LoggedInAs = null;
        }));

        private AsyncCommand<CancellationToken?> _upgradeUserCommand;
        private static int _upgradeRun;

        private async Task WaitForAuthenticationAsync(int upgradeRun, CancellationToken cancellation) {
            var waitFor = TimeSpan.FromMinutes(10d);
            var stepSize = TimeSpan.FromSeconds(1d);
            for (int i = 0, t = (int)(waitFor.TotalSeconds / stepSize.TotalSeconds); i < t; i++) {
                await Task.Delay(stepSize, cancellation);
                if (_upgradeRun != upgradeRun || cancellation.IsCancellationRequested) break;
                LoggedInAs = await _client.GetAsync<UserInfo>($"/users/{_client.UserId}");
                Logging.Debug(JsonConvert.SerializeObject(LoggedInAs));
                if (!LoggedInAs.IsVirtual) return;
            }
        }

        public AsyncCommand<CancellationToken?> UpgradeUserCommand => _upgradeUserCommand
                ?? (_upgradeUserCommand = new AsyncCommand<CancellationToken?>(c => {
                    return TryAsync(async () => {
                        var upgradeRun = ++_upgradeRun;
                        var password = Prompt.Show("Choose a new password for your CM Workshop account:", "Verify CM Workshop account",
                                comment: "After choosing a new password, you would need to verify it by passing Steam authentication.",
                                required: true, passwordMode: true);
                        if (string.IsNullOrEmpty(password)) return;
                        c?.ThrowIfCancellationRequested();

                        await UpgradeUserAsync(password);
                        c?.ThrowIfCancellationRequested();

                        await WaitForAuthenticationAsync(upgradeRun, c ?? default);
                    });
                }));

        public async Task UpgradeUserAsync(string userPassword) {
            if (_client.UserId == null || LoggedInAs == null || !LoggedInAs.IsVirtual) throw new Exception("Can’t upgrade user");
            await _client.RequestAsync<object, object>(new HttpMethod("PATCH"), $"/users/{_client.UserId}", new {
                isVirtual = false,
                passwordChecksum = WorkshopClient.GetPasswordChecksum(_client.UserId, userPassword)
            }, headers => {
                if (headers.Location != null) {
                    WindowsHelper.ViewInBrowser(headers.Location);
                }
            });
        }

        private AsyncCommand<CancellationToken?> _resetPasswordCommand;

        public AsyncCommand<CancellationToken?> ResetPasswordCommand
            => _resetPasswordCommand ?? (_resetPasswordCommand = new AsyncCommand<CancellationToken?>(c => {
                return TryAsync(async () => {
                    var upgradeRun = ++_upgradeRun;
                    var password = Prompt.Show("Choose a new password for your CM Workshop account:", "Reset CM Workshop password",
                            comment: "After choosing a new password, you would need to verify it by passing Steam authentication.",
                            required: true, passwordMode: true);
                    if (string.IsNullOrEmpty(password)) return;
                    c?.ThrowIfCancellationRequested();

                    await SetPasswordAsync(password);
                    c?.ThrowIfCancellationRequested();

                    await WaitForAuthenticationAsync(upgradeRun, c ?? default);
                });
            }));

        public async Task SetPasswordAsync([NotNull] string userPassword) {
            if (_client.UserId == null) throw new Exception("Can’t reset password");
            await _client.RequestAsync<object, object>(new HttpMethod("PATCH"), $"/users/{_client.UserId}", new {
                passwordChecksum = WorkshopClient.GetPasswordChecksum(_client.UserId, userPassword)
            }, headers => {
                if (headers.Location != null) {
                    WindowsHelper.ViewInBrowser(headers.Location);
                }
            });
        }
    }
}