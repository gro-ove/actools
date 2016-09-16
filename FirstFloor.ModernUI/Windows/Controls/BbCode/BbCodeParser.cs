using System;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using FirstFloor.ModernUI.Windows.Navigation;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Windows.Controls.BbCode {
    /// <summary>
    /// Represents the BbCode parser.
    /// </summary>
    internal class BbCodeParser
        : Parser<Span> {

        // supporting a basic set of BbCode tags
        private const string TagBold = "b";
        private const string TagMono = "mono";
        private const string TagColor = "color";
        private const string TagItalic = "i";
        private const string TagSize = "size";
        private const string TagStrike = "s";
        private const string TagUnderline = "u";
        private const string TagUrl = "url";
        private const string TagImage = "img";

        private class ParseContext {
            public ParseContext(Span parent) {
                Parent = parent;
            }

            public Span Parent { get; private set; }
            public double? FontSize { get; set; }
            public FontWeight? FontWeight { get; set; }
            public FontStyle? FontStyle { get; set; }
            public FontFamily FontFamily { get; set; }
            public Brush Foreground { get; set; }
            public TextDecorationCollection TextDecorations { get; set; }
            public string NavigateUri { get; set; }
            public string ImageUri { get; set; }

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
                context.FontWeight = null;
                if (start) {
                    context.FontWeight = FontWeights.Bold;
                }
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
                if (start) {
                    context.FontStyle = FontStyles.Italic;
                } else {
                    context.FontStyle = null;
                }
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
            } else if (tag == TagImage) {
                if (start) {
                    var token = La(1);
                    if (token.TokenType != BbCodeLexer.TokenAttribute) return;
                    context.ImageUri = token.Value;
                    Consume();
                } else {
                    context.ImageUri = null;
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

                if (token.TokenType == BbCodeLexer.TokenStartTag) {
                    ParseTag(token.Value, true, context);
                } else if (token.TokenType == BbCodeLexer.TokenEndTag) {
                    ParseTag(token.Value, false, context);
                } else if (token.TokenType == BbCodeLexer.TokenText) {
                    var parent = span;

                    {
                        Uri uri;
                        string parameter;
                        string targetName;

                        // parse uri value for optional parameter and/or target, eg [url=cmd://foo|parameter|target]
                        if (NavigationHelper.TryParseUriWithParameters(context.NavigateUri, out uri, out parameter, out targetName)) {
                            var link = new Hyperlink();

                            // assign ICommand instance if available, otherwise set NavigateUri
                            ICommand command;
                            if (Commands != null && Commands.TryGetValue(uri, out command)) {
                                link.Command = command;
                                link.CommandParameter = parameter;
                                if (targetName != null) {
                                    link.CommandTarget = _source?.FindName(targetName) as IInputElement;
                                }
                            } else {
                                link.NavigateUri = uri;
                                link.TargetName = parameter;
                            }
                            parent = link;
                            span.Inlines.Add(parent);
                        }
                    }

                    {
                        Uri uri;
                        string parameter;
                        string targetName;

                        if (NavigationHelper.TryParseUriWithParameters(context.ImageUri, out uri, out parameter, out targetName)) {
                            var bi = new BitmapImage();
                            bi.BeginInit();
                            bi.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                            bi.CacheOption = BitmapCacheOption.OnLoad;
                            bi.UriSource = uri;
                            bi.EndInit();

                            double maxSize;
                            if (!double.TryParse(parameter, out maxSize)) {
                                maxSize = double.MaxValue;
                            }

                            var image = new Image {
                                Source = bi,
                                ToolTip = new ToolTip {
                                    Content = new TextBlock { Text = token.Value }
                                },
                                MaxWidth = maxSize,
                                MaxHeight = maxSize,
                                Cursor = Cursors.Hand
                            };

                            image.MouseDown += (sender, args) => {
                                BbCodeBlock.OnImageClicked(new BbCodeImageEventArgs(uri));
                            };

                            RenderOptions.SetBitmapScalingMode(image,
                                    Equals(maxSize, double.MaxValue) ? BitmapScalingMode.LowQuality : BitmapScalingMode.HighQuality);
                            var container = new InlineUIContainer { Child = image };
                            span.Inlines.Add(container);
                            continue;
                        }
                    }

                    var run = context.CreateRun(token.Value);
                    parent.Inlines.Add(run);
                } else if (token.TokenType == BbCodeLexer.TokenLineBreak) {
                    span.Inlines.Add(new LineBreak());
                } else if (token.TokenType == BbCodeLexer.TokenAttribute) {
                    throw new ParseException(UiStrings.UnexpectedToken);
                } else if (token.TokenType == Lexer.TokenEnd) {
                    break;
                } else {
                    throw new ParseException(UiStrings.UnknownTokenType);
                }
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
