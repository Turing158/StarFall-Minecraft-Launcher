using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Markdig;
using Markdig.Renderers;
using Markdig.Renderers.Wpf;
using Markdig.Renderers.Wpf.Inlines;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using StarFallMC.Util;

namespace StarFallMC.Component;

public partial class Notice : UserControl,INotifyPropertyChanged {
    //  需要用到NuGet安装 Markdig
    
    public ImageSource Icon {
        get => (ImageSource)GetValue(IconProperty);
        set {
            SetValue(IconProperty, value);
            if (value == null) {
                TitleMargin = new Thickness(15,0,0,0);
            }
            else {
                TitleMargin = new Thickness(40,0,0,0);
            }
        }
    }
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(nameof(Icon), typeof(ImageSource),
        typeof(Notice), new PropertyMetadata(null));
    
    public double IconSize {
        get => (double)GetValue(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }
    public static readonly DependencyProperty IconSizeProperty = DependencyProperty.Register(nameof(IconSize),
        typeof(double), typeof(Notice), new PropertyMetadata(22.0));
    
    public string Title {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string),
        typeof(Notice), new PropertyMetadata(""));
    
    public double TitleFontSize {
        get => (double)GetValue(TitleFontSizeProperty);
        set => SetValue(TitleFontSizeProperty, value);
    }
    public static readonly DependencyProperty TitleFontSizeProperty = DependencyProperty.Register(nameof(TitleFontSize),
        typeof(double), typeof(Notice), new PropertyMetadata(15.0));
    
    public Brush TitleForeground {
        get => (Brush)GetValue(TitleForegroundProperty);
        set => SetValue(TitleForegroundProperty, value);
    }
    public static readonly DependencyProperty TitleForegroundProperty = DependencyProperty.Register(nameof(TitleForeground),
        typeof(Brush), typeof(Notice), new PropertyMetadata(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#264446"))));
    
    public string ContentText {
        get => (string)GetValue(ContentTextProperty);
        set => SetValue(ContentTextProperty, value);
    }
    public static readonly DependencyProperty ContentTextProperty = DependencyProperty.Register(nameof(ContentText), typeof(string),
        typeof(Notice), new PropertyMetadata("" ));
    
    public Brush ContentForeground {
        get => (Brush)GetValue(ContentForegroundProperty);
        set => SetValue(ContentForegroundProperty, value);
    }
    public static readonly DependencyProperty ContentForegroundProperty = DependencyProperty.Register(nameof(ContentForeground),
        typeof(Brush), typeof(Notice), new PropertyMetadata(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#264446"))));
    
    private Thickness _titleMargin;
    public Thickness TitleMargin {
        get => _titleMargin;
        set => SetField(ref _titleMargin, value);
    }
        
    private MarkdownPipeline _markdownPipeline;
    public MarkdownPipeline MarkdownPipeline {
        get => _markdownPipeline;
        set => SetField(ref _markdownPipeline, value);
    }
    
    public Notice() {
        InitializeComponent();
        DataContext = this;
        
        if (Icon == null) {
            TitleMargin = new Thickness(15,0,0,0);
        }
        else {
            TitleMargin = new Thickness(40,0,0,0);
        }
    }

    public override void OnApplyTemplate() {
        base.OnApplyTemplate();
        updateColor();
        ThemeUtil.AddColorChangedHandler(OnThemeColorChanged);
        Unloaded += OnUnloaded;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) {
        ThemeUtil.RemoveColorChangedHandler(OnThemeColorChanged);
        Unloaded -= OnUnloaded;
    }

    private void OnThemeColorChanged(object? sender, EventArgs e) {
        updateColor();
    }

    private void updateColor() {
        MarkdownPipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Use(new MarkdownStyleExtension())
            .Build();
    }
    
    public class MarkdownStyleExtension : IMarkdownExtension {
        public void Setup(MarkdownPipelineBuilder pipeline) {
            
        }

        public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer) {
            if (renderer is WpfRenderer wpfRenderer) {
                wpfRenderer.ObjectRenderers.Replace<HeadingRenderer>(new MarkdownHeadingBlock());
                wpfRenderer.ObjectRenderers.Replace<ParagraphRenderer>(new MarkdownParagraphBlock());
                wpfRenderer.ObjectRenderers.Replace<CodeBlockRenderer>(new MarkdownCodeBlock());
                wpfRenderer.ObjectRenderers.Replace<QuoteBlockRenderer>(new MarkdownQuoteBlock());
                wpfRenderer.ObjectRenderers.Replace<LinkInlineRenderer>(new MarkdownHyperlink());
                wpfRenderer.ObjectRenderers.Replace<CodeInlineRenderer>(new MarkdownCodeInline());
            }
        }
    }
    
    public class MarkdownHeadingBlock : WpfObjectRenderer<HeadingBlock> {
        protected override void Write(WpfRenderer renderer, HeadingBlock obj) {
            var paragraph = new Paragraph {
                FontSize = 20 - obj.Level,
                Foreground = ThemeUtil.SecondaryBrush,
                Margin = new Thickness(0,10,0,10),
                FontWeight = FontWeights.Bold
            };
            renderer.Push(paragraph);
            renderer.WriteChildren(obj.Inline);
            renderer.Pop();
        }
    }
    
    public class MarkdownParagraphBlock : WpfObjectRenderer<ParagraphBlock> {
        protected override void Write(WpfRenderer renderer, ParagraphBlock obj) {
            var paragraph = new Paragraph {
                FontSize = 14,
                Foreground = ThemeUtil.SecondaryBrush_1,
                Margin = new Thickness(0,5,0,5),
            };
            renderer.Push(paragraph);
            renderer.WriteChildren(obj.Inline);
            renderer.Pop();
        }
    }
    
    public class MarkdownCodeBlock : WpfObjectRenderer<CodeBlock> {
        protected override void Write(WpfRenderer renderer, CodeBlock obj) {
            var paragraph = new Paragraph {
                FontSize = 14,
                Foreground = ThemeUtil.SecondaryBrush_1,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#66ffffff")),
                FontFamily = new FontFamily("Consolas"),
                Padding = new Thickness(10,10,10,0),
            };
            renderer.Push(paragraph);
            foreach (var line in obj.Lines.Lines.SkipLast(1)) {
                var slice = line.Slice;
                var text = slice.ToString();
                paragraph.Inlines.Add(new Run(text));
                paragraph.Inlines.Add(new LineBreak());
            }
            renderer.Pop();
        }
    }
    
    public class MarkdownQuoteBlock  : WpfObjectRenderer<QuoteBlock> {
        protected override void Write(WpfRenderer renderer, QuoteBlock obj) {
            var border = new Border {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#22ffffff")),
            };
            var section = new Section {
                FontSize = 14,
                Foreground = ThemeUtil.SecondaryBrush,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#22ffffff")),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#88ffffff")),
                Padding = new Thickness(5,1,5,1),
                Margin = new Thickness(0,10,0,10),
                BorderThickness = new Thickness(5,0,0,0)
            };
            renderer.Push(section);
            renderer.WriteChildren(obj);
            renderer.Pop();
        }
    }
    
    public class MarkdownHyperlink : WpfObjectRenderer<LinkInline> {
        public static Style GetHyperlinkStyle() {
            var hyperlinkStyle = new Style(typeof(Hyperlink));
            hyperlinkStyle.Setters.Add(new Setter(Hyperlink.ForegroundProperty, 
                ThemeUtil.SecondaryBrush));
            return hyperlinkStyle;
        }

        protected override void Write(WpfRenderer renderer, LinkInline obj) {
            if (obj.IsImage) {
                string url = obj.Url;
                if (!url.StartsWith("http") || !url.Contains("://")) {
                    url = DirFileUtil.GetAbsolutePathInLauncherSettingDir(url);
                }
                var flag = true;
                BitmapImage bitmapImage;
                try {
                    bitmapImage = new BitmapImage(new Uri(url, UriKind.RelativeOrAbsolute));
                } catch {
                    flag = false;
                    try {
                        // 使用pack:// URI訪問應用程式資源
                        string defaultImagePath = "pack://application:,,,/assets/DefaultGameIcon/unknowGame.png";
                        bitmapImage = new BitmapImage(new Uri(defaultImagePath, UriKind.Absolute));
                    } catch {
                        // 如果pack://方案也失敗，使用原始方案作為備份
                        try {
                            bitmapImage = new BitmapImage(new Uri("../assets/DefaultGameIcon/unknowGame.png", UriKind.Relative));
                        } catch {
                            bitmapImage = new BitmapImage();
                        }
                    }
                }
                if (flag) {
                    var image = new Image {
                        Source = bitmapImage,
                        ToolTip = url,
                        Stretch = Stretch.Uniform,
                        StretchDirection = StretchDirection.DownOnly,
                    };
                    renderer.Push(new InlineUIContainer(image));
                } else {
                    var stackPanel = new StackPanel {
                        Orientation = Orientation.Horizontal,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 2, 0, 2)
                    };

                    var image = new Image {
                        Source = bitmapImage,
                        ToolTip = "图片加载失败，URL: " + url,
                        Stretch = Stretch.Uniform,
                        StretchDirection = StretchDirection.DownOnly,
                        Width = 16,
                        Height = 16,
                        Margin = new Thickness(0, 0, 5, 0)
                    };

                    var errorText = new TextBlock {
                        Text = "图片加載失败！",
                        Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF5252")),
                        FontSize = 12,
                        VerticalAlignment = VerticalAlignment.Center
                    };

                    stackPanel.Children.Add(image);
                    stackPanel.Children.Add(errorText);

                    renderer.Push(new InlineUIContainer(stackPanel));
                }
            }
            else {
                var hyperlink = new Hyperlink {
                    NavigateUri = !string.IsNullOrEmpty(obj.Url) ? new Uri(obj.Url, UriKind.RelativeOrAbsolute) : null,
                    Style = GetHyperlinkStyle()
                };
                if (hyperlink.NavigateUri != null) {
                    hyperlink.RequestNavigate += (sender, e) => {
                        string url = e.Uri.ToString();
                        if (!string.IsNullOrEmpty(url) && url.StartsWith("#")) {
                            // 锚点链接处理逻辑（未实现功能）
                           MessageTips.Show("未实现功能：页面内标题跳转功能暂不支持");
                        } else {
                            // 普通链接处理逻辑
                            NetworkUtil.OpenUrl(e.Uri.AbsoluteUri);
                        }
                        e.Handled = true;
                    };
                }
                renderer.Push(hyperlink);
                renderer.WriteChildren(obj);
            }
            renderer.Pop();
        }
    }
    
    public class MarkdownCodeInline : WpfObjectRenderer<CodeInline> {
        protected override void Write(WpfRenderer renderer, CodeInline obj) {
            var run = new TextBlock {
                Text = obj.Content,
                FontSize = 13,
                Padding = new Thickness(2,0,2,0),
                Margin = new Thickness(2,0,2,0),
                FontFamily = new FontFamily("Consolas"),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#55ffffff")),
                
            };
            renderer.Push(run);
            renderer.Pop();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null) {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null) {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
