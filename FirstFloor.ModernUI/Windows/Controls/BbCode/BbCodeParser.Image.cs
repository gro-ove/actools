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
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace FirstFloor.ModernUI.Windows.Controls.BbCode {
    internal partial class BbCodeParser {
        public static int OptionReadCachedAsyncThreshold = 20000;

        private class InlineImage : Image {
            private readonly InlineImageCache _cache;

            static InlineImage() {
                DefaultStyleKeyProperty.OverrideMetadata(typeof(InlineImage),
                        new FrameworkPropertyMetadata(typeof(InlineImage)));
            }

            public InlineImage(InlineImageCache cache = null) {
                _cache = cache;
            }

            public Uri ImageUri {
                get => (Uri)GetValue(ImageUriProperty);
                set => SetValue(ImageUriProperty, value);
            }

            public BitmapCreateOptions CreateOptions {
                get => GetValue(CreateOptionsProperty) as BitmapCreateOptions? ?? default(BitmapCreateOptions);
                set => SetValue(CreateOptionsProperty, value);
            }

            private static async void ImageUrlPropertyChanged(DependencyObject obj, DependencyPropertyChangedEventArgs e) {
                var uri = e.NewValue as Uri;
                if (uri == null) return;

                var cachedImage = (InlineImage)obj;
                var bitmapImage = new BitmapImage();

                if (cachedImage._cache != null) {
                    try {
                        var memoryStream = await cachedImage._cache.HitAsync(uri);
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
                    bitmapImage.UriSource = uri;

                    // Enable IE-like cache policy.
                    bitmapImage.UriCachePolicy = new RequestCachePolicy(RequestCacheLevel.Default);
                    bitmapImage.EndInit();
                    cachedImage.Source = bitmapImage;
                }
            }

            public static readonly DependencyProperty ImageUriProperty = DependencyProperty.Register("ImageUri",
                    typeof(Uri), typeof(InlineImage), new PropertyMetadata(null, ImageUrlPropertyChanged));

            public static readonly DependencyProperty CreateOptionsProperty = DependencyProperty.Register("CreateOptions",
                    typeof(BitmapCreateOptions), typeof(InlineImage));
        }

        private class InlineImageCache {
            // Record whether a file is being written.
            private readonly Dictionary<string, bool> _isWritingFile = new Dictionary<string, bool>();

            public InlineImageCache(string directory) {
                _appCacheDirectory = directory;
            }

            private readonly string _appCacheDirectory;

            public async Task<MemoryStream> HitAsync(Uri uri) {
                if (!Directory.Exists(_appCacheDirectory)) {
                    Directory.CreateDirectory(_appCacheDirectory);
                }

                var fileNameBuilder = new StringBuilder();
                using (var sha1 = new SHA1Managed()) {
                    var canonicalUrl = uri.ToString();
                    var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(canonicalUrl));
                    fileNameBuilder.Append(BitConverter.ToString(hash).Replace(@"-", "").ToLower());
                    if (Path.HasExtension(canonicalUrl)) {
                        fileNameBuilder.Append(Path.GetExtension(canonicalUrl));
                    }
                }

                var fileName = fileNameBuilder.ToString();
                var localFile = Path.Combine(_appCacheDirectory, fileName);
                var memoryStream = new MemoryStream();

                var info = new FileInfo(localFile);
                FileStream fileStream = null;
                if (!_isWritingFile.ContainsKey(fileName) && info.Exists) {
                    using (fileStream = new FileStream(localFile, FileMode.Open, FileAccess.Read)) {
                        if (info.Length < OptionReadCachedAsyncThreshold) {
                            fileStream.CopyTo(memoryStream);
                        } else {
                            await fileStream.CopyToAsync(memoryStream);
                        }
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
                            if (fileStream != null) {
                                await fileStream.WriteAsync(bytebuffer, 0, bytesRead);
                            }
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
