using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Serialization;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers {
    // Based on https://www.codeproject.com/Tips/443588/Simple-Csharp-FTP-Class, but changed quite a bit
    public class FtpClient {
        [NotNull]
        private readonly string _hostName;

        [CanBeNull]
        private readonly string _userName;

        [CanBeNull]
        private readonly string _password;

        public int BufferSize { get; set; } = 8192;
        public bool UseBinary { get; set; } = true;
        public bool UsePassive { get; set; } = true;
        public bool KeepAlive { get; set; } = true;
        public TimeSpan Timeout { get; set; } = TimeSpan.MaxValue;

        public FtpClient([NotNull] string hostName, [CanBeNull] string userNameName, [CanBeNull] string password) {
            _hostName = hostName;
            _userName = userNameName;
            _password = password;
        }

        private KillerOrder<FtpWebRequest> Request([NotNull] string remoteFile, [NotNull] string method, CancellationToken cancellation) {
            cancellation.ThrowIfCancellationRequested();
            var killer = KillerOrder.Create((FtpWebRequest)WebRequest.Create($"{_hostName}/{remoteFile}"), Timeout, cancellation);
            var request = killer.Victim;
            request.Credentials = new NetworkCredential(_userName, _password);
            request.UseBinary = UseBinary;
            request.UsePassive = UsePassive;
            request.KeepAlive = KeepAlive;
            request.Method = method;
            return killer;
        }

        public async Task DownloadAsync([NotNull] string remoteFile, [NotNull] string localFile, CancellationToken cancellation = default(CancellationToken)) {
            using (var killer = Request(remoteFile, WebRequestMethods.Ftp.DownloadFile, cancellation))
            using (var response = (FtpWebResponse)await killer.Victim.GetResponseAsync().ConfigureAwait(false))
            using (var stream = response.GetResponseStream()) {
                cancellation.ThrowIfCancellationRequested();
                if (stream == null) throw new Exception("No response");
                using (var local = new FileStream(localFile, FileMode.Create)) {
                    await stream.CopyToAsync(local, BufferSize, progress: killer.DelayingProgress<long>()).ConfigureAwait(false);
                }
            }
        }

        public async Task UploadAsync([NotNull] string remoteFile, [NotNull] string localFile, CancellationToken cancellation = default(CancellationToken)) {
            using (var killer = Request(remoteFile, WebRequestMethods.Ftp.UploadFile, cancellation))
            using (var local = new FileStream(localFile, FileMode.Open))
            using (var stream = await killer.Victim.GetRequestStreamAsync()) {
                cancellation.ThrowIfCancellationRequested();
                killer.Pause();
                await Task.Run(() => local.CopyTo(stream, BufferSize)).ConfigureAwait(false);
            }
        }

        public async Task DeleteAsync([NotNull] string remoteFile, CancellationToken cancellation = default(CancellationToken)) {
            using (var killer = Request(remoteFile, WebRequestMethods.Ftp.DeleteFile, cancellation)) {
                await killer.Victim.GetResponseAsync().ConfigureAwait(false);
            }
        }

        public async Task RenameAsync([NotNull] string remoteFile, [NotNull] string newFileName, CancellationToken cancellation = default(CancellationToken)) {
            using (var killer = Request(remoteFile, WebRequestMethods.Ftp.Rename, cancellation)) {
                killer.Victim.RenameTo = newFileName;
                await killer.Victim.GetResponseAsync().ConfigureAwait(false);
            }
        }

        public async Task DeleteDirectoryAsync([NotNull] string newDirectory, CancellationToken cancellation = default(CancellationToken)) {
            using (var killer = Request(newDirectory, WebRequestMethods.Ftp.RemoveDirectory, cancellation)) {
                await killer.Victim.GetResponseAsync().ConfigureAwait(false);
            }
        }

        public async Task CreateDirectoryAsync([NotNull] string newDirectory, CancellationToken cancellation = default(CancellationToken)) {
            using (var killer = Request(newDirectory, WebRequestMethods.Ftp.MakeDirectory, cancellation)) {
                await killer.Victim.GetResponseAsync().ConfigureAwait(false);
            }
        }

        public async Task CleanDirectoryAsync([NotNull] string directory, CancellationToken cancellation = default(CancellationToken)){
            var files = await DirectoryListDetailedAsync(directory, cancellation).ConfigureAwait(false);
            foreach (var file in files){
                var filePath = $@"{directory}/{file.FileName}";
                if (file.IsDirectory){
                    await CleanDirectoryAsync(filePath, cancellation).ConfigureAwait(false);
                    await DeleteDirectoryAsync(filePath, cancellation).ConfigureAwait(false);
                } else {
                    await DeleteAsync(filePath, cancellation).ConfigureAwait(false);
                }
            }
        }

        public async Task<string> GetFileCreatedDateTimeAsync([NotNull] string remoteFile, CancellationToken cancellation = default(CancellationToken)) {
            using (var killer = Request(remoteFile, WebRequestMethods.Ftp.GetDateTimestamp, cancellation))
            using (var response = (FtpWebResponse)await killer.Victim.GetResponseAsync().ConfigureAwait(false))
            using (var stream = response.GetResponseStream()) {
                cancellation.ThrowIfCancellationRequested();
                if (stream == null) throw new Exception("No response");
                return (await stream.ReadAsBytesAsync()).ToUtf8String();
            }
        }

        public async Task<string> GetFileSizeAsync([NotNull] string remoteFile, CancellationToken cancellation = default(CancellationToken)) {
            using (var killer = Request(remoteFile, WebRequestMethods.Ftp.GetFileSize, cancellation))
            using (var response = (FtpWebResponse)await killer.Victim.GetResponseAsync().ConfigureAwait(false))
            using (var stream = response.GetResponseStream()) {
                cancellation.ThrowIfCancellationRequested();
                if (stream == null) throw new Exception("No response");
                return (await stream.ReadAsBytesAsync()).ToUtf8String();
            }
        }

        public async Task<string[]> DirectoryListSimpleAsync([NotNull] string remoteFile, CancellationToken cancellation = default(CancellationToken)) {
            using (var killer = Request(remoteFile, WebRequestMethods.Ftp.ListDirectory, cancellation))
            using (var response = (FtpWebResponse)await killer.Victim.GetResponseAsync().ConfigureAwait(false))
            using (var stream = response.GetResponseStream()) {
                cancellation.ThrowIfCancellationRequested();
                if (stream == null) throw new Exception("No response");
                return (await stream.ReadAsBytesAsync()).ToUtf8String().TrimEnd().Split('\n');
            }
        }

		public class DetailedInformation {
			public bool IsDirectory { get; }
			public long Size { get; }
			public DateTime LastWriteTime { get; }
			public string FileName { get; }

			private DetailedInformation(bool isDirectory, long size, DateTime lastWriteTime, string fileName){
				IsDirectory = isDirectory;
				Size = size;
				LastWriteTime = lastWriteTime;
				FileName = fileName;
			}

			public static DetailedInformation[] Create(string data) {
			    return data.TrimEnd().Split('\n').Select(x => new {
			        Type = x.Length < 1 ? '\0' : x[0],
			        Match = Regex.Match(x, @"\s(\d+) ((?:Feb|Ma[ry]|A(?:pr|ug)|J(?:an|u[ln])|Sep|Oct|Nov|Dec) (?: \d|\d[\d ])) ( \d{4}|\d\d:\d\d) (.+?)(?:->.+)?$",
			                RegexOptions.IgnoreCase)
			    }).Select(x => {
			        var fileName = x.Match.Groups[4].Value.Trim();
			        return new DetailedInformation(IsDirectory(x.Type, fileName), x.Match.Groups[1].As<long>(), GetDate(x.Match.Groups), fileName);
			    }).ToArray();

				bool IsDirectory(char type, string fileName){
					return type == 'd' || type == 'l' && fileName.LastIndexOf('.') < fileName.Length - 4;
				}

				DateTime GetDate(GroupCollection p) {
				    var year = p[3].Length == 4 ? p[3].As<int>() : DateTime.Now.Year;
				    var monthAndDay = p[2];
				    var time = p[3].Length == 4 ? @"00:00" : p[3].Value;
				    return DateTime.Parse($@"{year} {monthAndDay} {time}", CultureInfo.InvariantCulture);
				}
			}
		}

        public async Task<DetailedInformation[]> DirectoryListDetailedAsync([NotNull] string remoteFile, CancellationToken cancellation = default(CancellationToken)) {
            using (var killer = Request(remoteFile, WebRequestMethods.Ftp.ListDirectoryDetails, cancellation))
            using (var response = (FtpWebResponse)await killer.Victim.GetResponseAsync().ConfigureAwait(false))
            using (var stream = response.GetResponseStream()) {
                cancellation.ThrowIfCancellationRequested();
                if (stream == null) throw new Exception("No response");
                return DetailedInformation.Create(stream.ReadAsString());
            }
        }
    }
}