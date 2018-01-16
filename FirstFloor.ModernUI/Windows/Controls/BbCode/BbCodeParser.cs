using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Shapes;
using FirstFloor.ModernUI.Serialization;
using FirstFloor.ModernUI.Windows.Converters;
using FirstFloor.ModernUI.Windows.Navigation;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Windows.Controls.BbCode {
    internal partial class BbCodeParser : Parser<Span> {
        private static InlineImageCache _imageCache, _emojiCache;

        private const string TagBold = "b";
        private const string TagMono = "mono";
        private const string TagColor = "color";
        private const string TagItalic = "i";
        private const string TagSize = "size";
        private const string TagStrike = "s";
        private const string TagSuperscript = "sup";
        private const string TagUnderline = "u";
        private const string TagUrl = "url";
        private const string TagImage = "img";
        private const string TagIcon = "ico";

        private class ParseContext {
            public ParseContext(Span parent) {
                // Parent = parent;
            }

            // public Span Parent;
            public double? FontSize;
            public FontWeight? FontWeight;
            public FontStyle? FontStyle;
            public FontFamily FontFamily;
            public Brush Foreground;
            public TextDecorationCollection TextDecorations;
            public FontVariants? FontVariants;

            [CanBeNull]
            public string NavigateUri, ImageUri;

            [CanBeNull]
            public Geometry IconGeometry;

            /// <summary>
            /// Creates a run reflecting the current context settings.
            /// </summary>
            /// <returns></returns>
            public Run CreateRun(string text) {
                var run = new Run { Text = text };
                if (FontSize.HasValue) {
                    run.FontSize = FontSize.Value;
                }
                if (FontWeight.HasValue) {
                    run.FontWeight = FontWeight.Value;
                }
                if (FontStyle.HasValue) {
                    run.FontStyle = FontStyle.Value;
                }
                if (FontVariants.HasValue) {
                    Typography.SetVariants(run, FontVariants.Value);
                }
                if (Foreground != null) {
                    run.Foreground = Foreground;
                }
                if (FontFamily != null) {
                    run.FontFamily = FontFamily;
                }
                run.TextDecorations = TextDecorations;
                return run;
            }
        }

        [CanBeNull]
        private readonly FrameworkElement _source;
        private readonly List<Tuple<Uri, string>> _imageUrls = new List<Tuple<Uri, string>>();

        /// <summary>
        /// Initializes a new instance of the <see cref="T:BBCodeParser"/> class.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="source">The framework source element this parser operates in.</param>
        public BbCodeParser(string value, [CanBeNull] FrameworkElement source) : base(new BbCodeLexer(value)) {
            _source = source;
        }

        /// <summary>
        /// Gets or sets the available navigable commands.
        /// </summary>
        public CommandDictionary Commands { get; set; }

        private void ParseTag(string tag, bool start, ParseContext context) {
            if (tag == TagBold) {
                context.FontWeight = start ? (FontWeight?)FontWeights.Bold : null;
            } else if (tag == TagColor) {
                if (start) {
                    var token = La(1);
                    if (token.TokenType != BbCodeLexer.TokenAttribute) return;
                    var convertFromString = ColorConverter.ConvertFromString(token.Value);
                    if (convertFromString != null) {
                        var color = (Color)convertFromString;
                        context.Foreground = new SolidColorBrush(color);
                    }

                    Consume();
                } else {
                    context.Foreground = null;
                }
            } else if (tag == TagItalic) {
                context.FontStyle = start ? (FontStyle?)FontStyles.Italic : null;
            } else if (tag == TagMono) {
                context.FontFamily = start ? new FontFamily("Consolas") : null;
            } else if (tag == TagSize) {
                if (start) {
                    var token = La(1);
                    if (token.TokenType != BbCodeLexer.TokenAttribute) return;
                    context.FontSize = Convert.ToDouble(token.Value);
                    Consume();
                } else {
                    context.FontSize = null;
                }
            } else if (tag == TagUnderline) {
                context.TextDecorations = start ? TextDecorations.Underline : null;
            } else if (tag == TagStrike) {
                context.TextDecorations = start ? TextDecorations.Strikethrough : null;
            } else if (tag == TagSuperscript) {
                context.FontVariants = start ? (FontVariants?)FontVariants.Superscript : null;
            } else if (tag == TagImage) {
                if (start) {
                    var token = La(1);
                    if (token.TokenType != BbCodeLexer.TokenAttribute) return;
                    context.ImageUri = token.Value;
                    Consume();
                } else {
                    context.ImageUri = null;
                }
            } else if (tag == TagIcon) {
                if (start) {
                    var token = La(1);
                    if (token.TokenType != BbCodeLexer.TokenAttribute) return;
                    context.IconGeometry = Geometry.Parse(token.Value);
                    Consume();
                } else {
                    context.IconGeometry = null;
                }
            } else if (tag == TagUrl) {
                if (start) {
                    var token = La(1);
                    if (token.TokenType != BbCodeLexer.TokenAttribute) return;
                    context.NavigateUri = token.Value;
                    Consume();
                } else {
                    context.NavigateUri = null;
                }
            }
        }

        private void Parse(Span span) {
            var context = new ParseContext(span);

            while (true) {
                var token = La(1);
                Consume();

                switch (token.TokenType) {
                    case BbCodeLexer.TokenStartTag:
                        ParseTag(token.Value, true, context);
                        break;
                    case BbCodeLexer.TokenEndTag:
                        ParseTag(token.Value, false, context);
                        break;
                    case BbCodeLexer.TokenText:
                        var parent = span;

                        // parse uri value for optional parameter and/or target, eg [url=cmd://foo|parameter|target]
                        if (NavigationHelper.TryParseUriWithParameters(context.NavigateUri, out var parsedUri, out var parsedParameter,
                                out var parsedTargetName)) {
                            var link = new Hyperlink();

                            if (context.IconGeometry != null) {
                                link.TextDecorations.Clear();
                            }

                            // assign ICommand instance if available, otherwise set NavigateUri
                            if (Commands != null && Commands.TryGetValue(parsedUri, out var command)) {
                                link.Command = command;
                                link.CommandParameter = parsedParameter;
                                if (parsedTargetName != null) {
                                    link.CommandTarget = _source?.FindName(parsedTargetName) as IInputElement;
                                }
                            } else {
                                link.NavigateUri = parsedUri;
                                link.TargetName = parsedParameter;
                            }

                            parent = link;
                            span.Inlines.Add(parent);
                        }

                        if (context.IconGeometry != null) {
                            var icon = new Path {
                                Stretch = Stretch.Uniform,
                                Data = context.IconGeometry
                            };

                            icon.SetBinding(Shape.FillProperty, new Binding {
                                Path = new PropertyPath("(TextBlock.Foreground)"),
                                RelativeSource = new RelativeSource(RelativeSourceMode.Self),
                            });

                            var border = new Border {
                                Background = new SolidColorBrush(Colors.Transparent),
                                Child = icon,
                                ToolTip = new ToolTip {
                                    Content = new TextBlock { Text = token.Value }
                                }
                            };

                            border.SetBinding(FrameworkElement.HeightProperty, new Binding {
                                Path = new PropertyPath("(TextBlock.FontSize)"),
                                RelativeSource = new RelativeSource(RelativeSourceMode.Self),
                                Converter = new MultiplyConverter(),
                                ConverterParameter = 0.7
                            });

                            border.SetBinding(FrameworkElement.WidthProperty, new Binding {
                                Path = new PropertyPath(nameof(Border.Height)),
                                RelativeSource = new RelativeSource(RelativeSourceMode.Self)
                            });

                            parent.Inlines.Add(new InlineUIContainer { Child = border });
                            continue;
                        } {

                        Uri url;
                        ImageSource imageSource = null;
                        double maxSize;
                        bool expand, toolTip;
                        InlineImageCache cache;

                        if (context.ImageUri?.StartsWith(@"emoji://") == true) {
                            maxSize = 0;
                            expand = false;
                            toolTip = false;

                            var emojiCode = context.ImageUri.Substring(8);
                            url = BbCodeBlock.OptionEmojiProvider?.GetUri(emojiCode);
                            if (url == null) {
                                cache = null;
                                imageSource = BbCodeBlock.OptionEmojiProvider?.GetImageSource(emojiCode);
                            } else {
                                cache = BbCodeBlock.OptionEmojiCacheDirectory == null ? null :
                                        _emojiCache ?? (_emojiCache = new InlineImageCache(BbCodeBlock.OptionEmojiCacheDirectory));
                            }
                        } else {
                            toolTip = true;

                            if (NavigationHelper.TryParseUriWithParameters(context.ImageUri, out var temporary, out var parameter, out _)) {
                                try {
                                    url = new Uri(temporary.OriginalString);
                                    if (double.TryParse(parameter, out maxSize)) {
                                        expand = true;
                                    } else {
                                        maxSize = double.NaN;
                                        expand = false;
                                    }

                                    cache = BbCodeBlock.OptionImageCacheDirectory == null ? null :
                                            _imageCache ?? (_imageCache = new InlineImageCache(BbCodeBlock.OptionImageCacheDirectory));
                                } catch {
                                    url = null;
                                    maxSize = double.NaN;
                                    expand = false;
                                    cache = null;
                                }
                            } else {
                                url = null;
                                maxSize = double.NaN;
                                expand = false;
                                cache = null;
                            }
                        }

                        if (url != null || imageSource != null) {
                            FrameworkElement image = imageSource != null ?
                                    new Image { Source = imageSource } : new InlineImage(cache) { ImageUri = url };

                            if (toolTip) {
                                image.ToolTip = new ToolTip {
                                    Content = new TextBlock { Text = token.Value }
                                };
                            }

                            if (double.IsNaN(maxSize)) {
                                RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.LowQuality);
                            } else {
                                if (Equals(maxSize, 0d)) {
                                    image.SetBinding(FrameworkElement.MaxHeightProperty, new Binding {
                                        Path = new PropertyPath(nameof(TextBlock.FontSize)),
                                        FallbackValue = 16d,
                                        RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(TextBlock), 1),
                                        Converter = new MultiplyConverter(),
                                    });
                                    image.Margin = new Thickness(1, -1, 1, -1);
                                } else {
                                    image.MaxWidth = maxSize;
                                    image.MaxHeight = maxSize;
                                }

                                RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
                            }

                            if (expand && url != null) {
                                _imageUrls.Add(Tuple.Create(url, toolTip ? token.Value : null));
                                image.Cursor = Cursors.Hand;
                                image.MouseDown += (sender, args) => {
                                    if (args.Handled) return;
                                    args.Handled = true;
                                };
                                image.MouseUp += (sender, args) => {
                                    if (args.Handled) return;
                                    args.Handled = true;
                                    BbCodeBlock.OnImageClicked(new BbCodeImageEventArgs(url, _imageUrls));
                                };
                            }

                            if (!expand) {
                                parent.Inlines.Add(new EmojiSpan(token.Value) {
                                    Inlines = {
                                        new InlineUIContainer { Child = image, Tag = token.Value }
                                    }
                                });
                            } else {
                                parent.Inlines.Add(new InlineUIContainer { Child = image, Tag = token.Value });
                            }

                            continue;
                        }
                    }

                        var run = context.CreateRun(token.Value);
                        parent.Inlines.Add(run);
                        break;
                    case BbCodeLexer.TokenLineBreak:
                        span.Inlines.Add(new LineBreak());
                        break;
                    case BbCodeLexer.TokenAttribute:
                        throw new ParseException(UiStrings.UnexpectedToken);
                    case Lexer.TokenEnd:
                        return;
                    default:
                        throw new ParseException(UiStrings.UnknownTokenType);
                }
            }
        }

        private class MultiplyConverter : IValueConverter {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
                var v = value.As(0d) * parameter.As(1d);
                if (v < 15) return v * 1.15;
                if (v < 20) return v * 1.08;
                return v;
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
                throw new NotSupportedException();
            }
        }

        /// <summary>
        /// Parses the text and returns a Span containing the parsed result.
        /// </summary>
        /// <returns></returns>
        public override Span Parse() {
            var span = new Span();
            Parse(span);
            return span;
        }
    }
}
