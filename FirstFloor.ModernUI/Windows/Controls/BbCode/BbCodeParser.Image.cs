/* The MIT License (MIT)

Copyright (c) 2014 Haridas Pachuveetil

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE. */

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Cache;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace FirstFloor.ModernUI.Windows.Controls.BbCode {
    internal partial class BbCodeParser {
        private class Image : System.Windows.Controls.Image {
            private readonly FileCache _cache;

            static Image() {
                DefaultStyleKeyProperty.OverrideMetadata(typeof(Image),
                        new FrameworkPropertyMetadata(typeof(Image)));
            }

            public Image(FileCache cache = null) {
                _cache = cache;
            }

            public string ImageUrl {
                get { return (string)GetValue(ImageUrlProperty); }
                set { SetValue(ImageUrlProperty, value); }
            }

            public BitmapCreateOptions CreateOptions {
                get { return (BitmapCreateOptions)GetValue(CreateOptionsProperty); }
                set { SetValue(CreateOptionsProperty, value); }
            }

            private static async void ImageUrlPropertyChanged(DependencyObject obj, DependencyPropertyChangedEventArgs e) {
                var url = e.NewValue as string;

                if (string.IsNullOrEmpty(url)) return;

                var cachedImage = (Image)obj;
                var bitmapImage = new BitmapImage();

                if (cachedImage._cache != null) {
                    try {
                        var memoryStream = await cachedImage._cache.HitAsync(url);
                        if (memoryStream == null) return;

                        bitmapImage.BeginInit();
                        bitmapImage.CreateOptions = cachedImage.CreateOptions;
                        bitmapImage.StreamSource = memoryStream;
                        bitmapImage.EndInit();
                        cachedImage.Source = bitmapImage;
                    } catch (Exception) {
                        // ignored, in case the downloaded file is a broken or not an image.
                    }
                } else {
                    bitmapImage.BeginInit();
                    bitmapImage.CreateOptions = cachedImage.CreateOptions;
                    bitmapImage.UriSource = new Uri(url);

                    // Enable IE-like cache policy.
                    bitmapImage.UriCachePolicy = new RequestCachePolicy(RequestCacheLevel.Default);
                    bitmapImage.EndInit();
                    cachedImage.Source = bitmapImage;
                }
            }

            public static readonly DependencyProperty ImageUrlProperty = DependencyProperty.Register("ImageUrl",
                    typeof(string), typeof(Image), new PropertyMetadata("", ImageUrlPropertyChanged));

            public static readonly DependencyProperty CreateOptionsProperty = DependencyProperty.Register("CreateOptions",
                    typeof(BitmapCreateOptions), typeof(Image));
        }

        private class FileCache {
            // Record whether a file is being written.
            private readonly Dictionary<string, bool> _isWritingFile = new Dictionary<string, bool>();

            public FileCache(string directory) {
                _appCacheDirectory = directory;
            }

            private readonly string _appCacheDirectory;

            public async Task<MemoryStream> HitAsync(string url) {
                if (!Directory.Exists(_appCacheDirectory)) {
                    Directory.CreateDirectory(_appCacheDirectory);
                }

                var uri = new Uri(url);
                var fileNameBuilder = new StringBuilder();
                using (var sha1 = new SHA1Managed()) {
                    var canonicalUrl = uri.ToString();
                    var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(canonicalUrl));
                    fileNameBuilder.Append(BitConverter.ToString(hash).Replace(@"-", "").ToLower());
                    if (Path.HasExtension(canonicalUrl))
                        fileNameBuilder.Append(Path.GetExtension(canonicalUrl));
                }

                var fileName = fileNameBuilder.ToString();
                var localFile = Path.Combine(_appCacheDirectory, fileName);
                var memoryStream = new MemoryStream();

                FileStream fileStream = null;
                if (!_isWritingFile.ContainsKey(fileName) && File.Exists(localFile)) {
                    using (fileStream = new FileStream(localFile, FileMode.Open, FileAccess.Read)) {
                        await fileStream.CopyToAsync(memoryStream);
                    }
                    memoryStream.Seek(0, SeekOrigin.Begin);
                    return memoryStream;
                }

                var request = WebRequest.Create(uri);
                request.Timeout = 30;
                try {
                    var response = await request.GetResponseAsync();
                    var responseStream = response.GetResponseStream();
                    if (responseStream == null)
                        return null;
                    if (!_isWritingFile.ContainsKey(fileName)) {
                        _isWritingFile[fileName] = true;
                        fileStream = new FileStream(localFile, FileMode.Create, FileAccess.Write);
                    }

                    using (responseStream) {
                        var bytebuffer = new byte[100];
                        int bytesRead;
                        do {
                            bytesRead = await responseStream.ReadAsync(bytebuffer, 0, 100);
                            if (fileStream != null)
                                await fileStream.WriteAsync(bytebuffer, 0, bytesRead);
                            await memoryStream.WriteAsync(bytebuffer, 0, bytesRead);
                        } while (bytesRead > 0);
                        if (fileStream != null) {
                            await fileStream.FlushAsync();
                            fileStream.Dispose();
                            _isWritingFile.Remove(fileName);
                        }
                    }
                    memoryStream.Seek(0, SeekOrigin.Begin);
                    return memoryStream;
                } catch (WebException) {
                    return null;
                }
            }
        }
    }
}
