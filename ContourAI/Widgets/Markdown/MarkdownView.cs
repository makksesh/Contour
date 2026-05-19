using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;

namespace ContourAI.Widgets.Markdown;

/// <summary>
/// Lightweight in-app markdown renderer for chat messages.
/// Supports the common subset used by LLM responses without external packages.
/// </summary>
public sealed class MarkdownView : UserControl
{
    public static readonly StyledProperty<string?> MarkdownProperty =
        AvaloniaProperty.Register<MarkdownView, string?>(nameof(Markdown));

    private static readonly IBrush PrimaryTextBrush = Brush.Parse("#F3EBDD");
    private static readonly IBrush MutedTextBrush = Brush.Parse("#968B7E");
    private static readonly IBrush AccentBrush = Brush.Parse("#B7926A");
    private static readonly IBrush AccentSoftBrush = Brush.Parse("#33B7926A");
    private static readonly IBrush SurfaceBrush = Brush.Parse("#1D1F22");
    private static readonly IBrush SurfaceAltBrush = Brush.Parse("#26292D");
    private static readonly IBrush SurfaceBorderBrush = Brush.Parse("#3A3D42");
    private static readonly IBrush QuoteBorderBrush = Brush.Parse("#8E6C4B");
    private static readonly IBrush CodeForegroundBrush = Brush.Parse("#E7D8C6");
    private static readonly IBrush HeaderCellBrush = Brush.Parse("#2A2D31");
    private static readonly FontFamily MonospaceFontFamily =
        new("JetBrains Mono, Cascadia Mono, Menlo, Monaco, Consolas, monospace");

    private readonly StackPanel _root = new()
    {
        Spacing = 10
    };

    public string? Markdown
    {
        get => GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }

    public MarkdownView()
    {
        Content = _root;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == MarkdownProperty)
            Rebuild();
    }

    private void Rebuild()
    {
        _root.Children.Clear();

        if (string.IsNullOrWhiteSpace(Markdown))
            return;

        try
        {
            var paragraphGroup = new List<ParagraphBlock>();

            void FlushParagraphGroup()
            {
                if (paragraphGroup.Count == 0)
                    return;

                _root.Children.Add(RenderParagraphGroup(paragraphGroup));
                paragraphGroup.Clear();
            }

            foreach (var block in ParseBlocks(Markdown!))
            {
                if (block is ParagraphBlock paragraph)
                {
                    paragraphGroup.Add(paragraph);
                    continue;
                }

                FlushParagraphGroup();
                _root.Children.Add(RenderBlock(block));
            }

            FlushParagraphGroup();
        }
        catch
        {
            _root.Children.Add(CreateSelectableTextBlock(Markdown!, 14, PrimaryTextBrush));
        }
    }

    private Control RenderBlock(MarkdownBlock block) => block switch
    {
        HeadingBlock heading => RenderHeading(heading),
        ParagraphBlock paragraph => RenderParagraph(paragraph),
        CodeBlock code => RenderCodeBlock(code),
        QuoteBlock quote => RenderQuoteBlock(quote),
        ListBlock list => RenderListBlock(list),
        TableBlock table => RenderTableBlock(table),
        HorizontalRuleBlock => RenderHorizontalRule(),
        _ => CreateSelectableTextBlock(block.RawText, 14, PrimaryTextBrush)
    };

    private Control RenderHeading(HeadingBlock block)
    {
        var fontSize = block.Level switch
        {
            1 => 24d,
            2 => 21d,
            3 => 18d,
            4 => 16d,
            _ => 14d
        };

        var textBlock = CreateSelectableTextBlock(string.Empty, fontSize, PrimaryTextBrush);
        textBlock.FontWeight = FontWeight.SemiBold;
        textBlock.Inlines = ParseInlines(block.Text);
        return textBlock;
    }

    private Control RenderParagraph(ParagraphBlock block)
    {
        var textBlock = CreateSelectableTextBlock(string.Empty, 14, PrimaryTextBrush);
        textBlock.LineHeight = 21;
        textBlock.Inlines = ParseInlines(block.Text);
        return textBlock;
    }

    private Control RenderParagraphGroup(IReadOnlyList<ParagraphBlock> blocks)
    {
        if (blocks.Count == 1)
            return RenderParagraph(blocks[0]);

        var textBlock = CreateSelectableTextBlock(string.Empty, 14, PrimaryTextBrush);
        textBlock.LineHeight = 21;

        var inlines = new InlineCollection();
        for (var index = 0; index < blocks.Count; index++)
        {
            if (index > 0)
            {
                inlines.Add(new LineBreak());
                inlines.Add(new LineBreak());
            }

            ParseInlinesInto(inlines, blocks[index].Text);
        }

        textBlock.Inlines = inlines;
        return textBlock;
    }

    private Control RenderCodeBlock(CodeBlock block)
    {
        var stack = new StackPanel
        {
            Spacing = 6
        };

        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 10
        };

        if (!string.IsNullOrWhiteSpace(block.Language))
        {
            header.Children.Add(new TextBlock
            {
                Text = block.Language!.ToUpperInvariant(),
                FontSize = 11,
                FontWeight = FontWeight.SemiBold,
                Foreground = MutedTextBrush
            });
        }

        var copyButton = CreateCodeActionButton("Copy", async (_, _) => await CopyCodeToClipboardAsync(block.Code));
        Grid.SetColumn(copyButton, 1);
        header.Children.Add(copyButton);
        stack.Children.Add(header);

        var codeText = CreateSelectableTextBlock(block.Code.TrimEnd('\n'), 13, CodeForegroundBrush);
        codeText.FontFamily = MonospaceFontFamily;
        codeText.TextWrapping = TextWrapping.NoWrap;
        codeText.LineHeight = 20;

        stack.Children.Add(new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = codeText
        });

        return new Border
        {
            Background = SurfaceAltBrush,
            BorderBrush = SurfaceBorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(12, 10),
            Child = stack
        };
    }

    private Control RenderQuoteBlock(QuoteBlock block)
    {
        var inner = new StackPanel
        {
            Spacing = 8
        };

        foreach (var child in block.Blocks)
            inner.Children.Add(RenderBlock(child));

        return new Border
        {
            BorderBrush = QuoteBorderBrush,
            BorderThickness = new Thickness(3, 0, 0, 0),
            Padding = new Thickness(12, 2, 0, 2),
            Child = inner
        };
    }

    private Control RenderListBlock(ListBlock block)
    {
        var stack = new StackPanel
        {
            Spacing = 6
        };

        for (var index = 0; index < block.Items.Count; index++)
        {
            var bullet = block.Ordered ? $"{index + 1}." : "•";
            var row = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*"),
                ColumnSpacing = 10
            };

            row.Children.Add(new TextBlock
            {
                Text = bullet,
                Foreground = AccentBrush,
                FontWeight = FontWeight.SemiBold,
                VerticalAlignment = VerticalAlignment.Top
            });

            var content = new StackPanel
            {
                Spacing = 6
            };

            Grid.SetColumn(content, 1);
            foreach (var child in block.Items[index])
                content.Children.Add(RenderBlock(child));

            row.Children.Add(content);
            stack.Children.Add(row);
        }

        return stack;
    }

    private Control RenderTableBlock(TableBlock block)
    {
        var table = new Grid
        {
            RowSpacing = 0,
            ColumnSpacing = 0
        };

        var columnCount = 0;
        foreach (var row in block.Rows)
            columnCount = Math.Max(columnCount, row.Count);

        for (var column = 0; column < columnCount; column++)
            table.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

        for (var rowIndex = 0; rowIndex < block.Rows.Count; rowIndex++)
        {
            table.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var row = block.Rows[rowIndex];
            for (var column = 0; column < columnCount; column++)
            {
                var text = column < row.Count ? row[column] : string.Empty;
                var textBlock = CreateSelectableTextBlock(string.Empty, 13, PrimaryTextBrush);
                textBlock.Inlines = ParseInlines(text);
                textBlock.LineHeight = 19;

                if (rowIndex == 0)
                    textBlock.FontWeight = FontWeight.SemiBold;

                var border = new Border
                {
                    Background = rowIndex == 0 ? HeaderCellBrush : SurfaceAltBrush,
                    BorderBrush = SurfaceBorderBrush,
                    BorderThickness = new Thickness(0, 0, 1, 1),
                    Padding = new Thickness(10, 8),
                    Child = textBlock
                };

                if (column == columnCount - 1)
                    border.BorderThickness = new Thickness(0, 0, 0, 1);

                Grid.SetRow(border, rowIndex);
                Grid.SetColumn(border, column);
                table.Children.Add(border);
            }
        }

        return new Border
        {
            BorderBrush = SurfaceBorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            ClipToBounds = true,
            Child = table
        };
    }

    private Control RenderHorizontalRule() => new Border
    {
        Background = SurfaceBorderBrush,
        Height = 1,
        Margin = new Thickness(0, 4)
    };

    private static SelectableTextBlock CreateSelectableTextBlock(string text, double fontSize, IBrush foreground) =>
        new()
        {
            Text = text,
            FontSize = fontSize,
            Foreground = foreground,
            TextWrapping = TextWrapping.Wrap,
            SelectionBrush = AccentSoftBrush,
            SelectionForegroundBrush = PrimaryTextBrush
        };

    private InlineCollection ParseInlines(string text)
    {
        var collection = new InlineCollection();
        ParseInlinesInto(collection, text);
        return collection;
    }

    private void ParseInlinesInto(InlineCollection target, string text)
    {
        var buffer = new StringBuilder();
        var index = 0;

        void FlushBuffer()
        {
            if (buffer.Length == 0)
                return;

            target.Add(buffer.ToString());
            buffer.Clear();
        }

        while (index < text.Length)
        {
            if (MatchDelimited(text, index, "**", out var boldContent, out var boldEnd))
            {
                FlushBuffer();
                var bold = new Bold();
                bold.Inlines = ParseInlines(boldContent);
                target.Add(bold);
                index = boldEnd;
                continue;
            }

            if (MatchDelimited(text, index, "__", out var strongContent, out var strongEnd))
            {
                FlushBuffer();
                var bold = new Bold();
                bold.Inlines = ParseInlines(strongContent);
                target.Add(bold);
                index = strongEnd;
                continue;
            }

            if (MatchDelimited(text, index, "*", out var italicContent, out var italicEnd))
            {
                FlushBuffer();
                var italic = new Italic();
                italic.Inlines = ParseInlines(italicContent);
                target.Add(italic);
                index = italicEnd;
                continue;
            }

            if (MatchDelimited(text, index, "_", out var emContent, out var emEnd))
            {
                FlushBuffer();
                var italic = new Italic();
                italic.Inlines = ParseInlines(emContent);
                target.Add(italic);
                index = emEnd;
                continue;
            }

            if (MatchCodeSpan(text, index, out var codeContent, out var codeEnd))
            {
                FlushBuffer();
                target.Add(CreateCodeRun(codeContent));
                index = codeEnd;
                continue;
            }

            if (MatchLink(text, index, out var linkLabel, out var linkUrl, out var linkEnd))
            {
                FlushBuffer();
                target.Add(CreateLinkButton(linkLabel, linkUrl));
                index = linkEnd;
                continue;
            }

            buffer.Append(text[index]);
            index++;
        }

        FlushBuffer();
    }

    private static Run CreateCodeRun(string content) => new()
    {
        Text = content,
        FontFamily = MonospaceFontFamily,
        Foreground = CodeForegroundBrush,
        Background = SurfaceBrush
    };

    private static bool MatchDelimited(string text, int start, string delimiter, out string content, out int nextIndex)
    {
        content = string.Empty;
        nextIndex = start;

        if (!text.AsSpan(start).StartsWith(delimiter.AsSpan(), StringComparison.Ordinal))
            return false;

        var closeIndex = text.IndexOf(delimiter, start + delimiter.Length, StringComparison.Ordinal);
        if (closeIndex <= start + delimiter.Length)
            return false;

        content = text.Substring(start + delimiter.Length, closeIndex - start - delimiter.Length);
        nextIndex = closeIndex + delimiter.Length;
        return true;
    }

    private static bool MatchCodeSpan(string text, int start, out string content, out int nextIndex)
    {
        content = string.Empty;
        nextIndex = start;

        if (text[start] != '`')
            return false;

        var closeIndex = text.IndexOf('`', start + 1);
        if (closeIndex <= start + 1)
            return false;

        content = text.Substring(start + 1, closeIndex - start - 1);
        nextIndex = closeIndex + 1;
        return true;
    }

    private static bool MatchLink(string text, int start, out string label, out string url, out int nextIndex)
    {
        label = string.Empty;
        url = string.Empty;
        nextIndex = start;

        if (text[start] != '[')
            return false;

        var labelEnd = text.IndexOf(']', start + 1);
        if (labelEnd < 0 || labelEnd + 1 >= text.Length || text[labelEnd + 1] != '(')
            return false;

        var urlEnd = text.IndexOf(')', labelEnd + 2);
        if (urlEnd < 0)
            return false;

        label = text.Substring(start + 1, labelEnd - start - 1);
        url = text.Substring(labelEnd + 2, urlEnd - labelEnd - 2);
        nextIndex = urlEnd + 1;
        return true;
    }

    private static List<MarkdownBlock> ParseBlocks(string markdown)
    {
        var normalized = markdown.Replace("\r\n", "\n").Replace('\r', '\n');
        var lines = normalized.Split('\n');
        var result = new List<MarkdownBlock>();

        for (var index = 0; index < lines.Length;)
        {
            if (string.IsNullOrWhiteSpace(lines[index]))
            {
                index++;
                continue;
            }

            var trimmed = lines[index].Trim();

            if (TryParseFence(lines, ref index, out var codeBlock))
            {
                result.Add(codeBlock);
                continue;
            }

            if (TryParseHeading(trimmed, out var heading))
            {
                result.Add(heading);
                index++;
                continue;
            }

            if (IsHorizontalRule(trimmed))
            {
                result.Add(new HorizontalRuleBlock(trimmed));
                index++;
                continue;
            }

            if (TryParseQuote(lines, ref index, out var quoteBlock))
            {
                result.Add(quoteBlock);
                continue;
            }

            if (TryParseTable(lines, ref index, out var tableBlock))
            {
                result.Add(tableBlock);
                continue;
            }

            if (TryParseList(lines, ref index, out var listBlock))
            {
                result.Add(listBlock);
                continue;
            }

            result.Add(ParseParagraph(lines, ref index));
        }

        return result;
    }

    private static bool TryParseFence(string[] lines, ref int index, out CodeBlock block)
    {
        block = null!;
        var trimmed = lines[index].Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
            return false;

        var language = trimmed.Length > 3 ? trimmed[3..].Trim() : null;
        var builder = new StringBuilder();
        index++;

        while (index < lines.Length && !lines[index].Trim().StartsWith("```", StringComparison.Ordinal))
        {
            builder.AppendLine(lines[index]);
            index++;
        }

        if (index < lines.Length)
            index++;

        block = new CodeBlock(builder.ToString(), builder.ToString(), language);
        return true;
    }

    private static bool TryParseHeading(string trimmed, out HeadingBlock block)
    {
        block = null!;
        var match = Regex.Match(trimmed, @"^(#{1,6})\s+(.*)$");
        if (!match.Success)
            return false;

        block = new HeadingBlock(trimmed, match.Groups[2].Value.Trim(), match.Groups[1].Value.Length);
        return true;
    }

    private static bool IsHorizontalRule(string trimmed)
    {
        if (trimmed.Length < 3)
            return false;

        var candidate = trimmed.Replace(" ", string.Empty, StringComparison.Ordinal);
        return candidate is "---" or "***" or "___" ||
               Regex.IsMatch(candidate, @"^(-{3,}|\*{3,}|_{3,})$");
    }

    private static bool TryParseQuote(string[] lines, ref int index, out QuoteBlock block)
    {
        block = null!;
        if (!lines[index].TrimStart().StartsWith(">", StringComparison.Ordinal))
            return false;

        var quoted = new List<string>();
        while (index < lines.Length)
        {
            var current = lines[index];
            var trimmedStart = current.TrimStart();
            if (!trimmedStart.StartsWith(">", StringComparison.Ordinal))
                break;

            var withoutMarker = trimmedStart.Length > 1 && trimmedStart[1] == ' '
                ? trimmedStart[2..]
                : trimmedStart[1..];
            quoted.Add(withoutMarker);
            index++;
        }

        var content = string.Join('\n', quoted);
        block = new QuoteBlock(content, ParseBlocks(content));
        return true;
    }

    private static bool TryParseTable(string[] lines, ref int index, out TableBlock block)
    {
        block = null!;

        if (index + 1 >= lines.Length)
            return false;

        var headerCells = TrySplitTableRow(lines[index]);
        var separatorCells = TrySplitTableRow(lines[index + 1]);
        if (headerCells is null || separatorCells is null || headerCells.Count == 0 || separatorCells.Count != headerCells.Count)
            return false;

        foreach (var separator in separatorCells)
        {
            if (!Regex.IsMatch(separator.Trim(), @"^:?-{3,}:?$"))
                return false;
        }

        var rows = new List<IReadOnlyList<string>> { headerCells };
        index += 2;

        while (index < lines.Length)
        {
            if (string.IsNullOrWhiteSpace(lines[index]))
                break;

            var cells = TrySplitTableRow(lines[index]);
            if (cells is null)
                break;

            rows.Add(cells);
            index++;
        }

        block = new TableBlock(string.Join('\n', lines), rows);
        return true;
    }

    private static bool TryParseList(string[] lines, ref int index, out ListBlock block)
    {
        block = null!;
        if (!TryMatchListItem(lines[index], out var baseIndent, out var ordered, out var content))
            return false;

        var items = new List<List<MarkdownBlock>>();

        while (index < lines.Length)
        {
            if (!TryMatchListItem(lines[index], out var itemIndent, out var itemOrdered, out var itemText) ||
                itemIndent != baseIndent ||
                itemOrdered != ordered)
                break;

            var itemLines = new List<string> { itemText };
            index++;

            while (index < lines.Length)
            {
                if (string.IsNullOrWhiteSpace(lines[index]))
                {
                    itemLines.Add(string.Empty);
                    index++;
                    continue;
                }

                if (TryMatchListItem(lines[index], out var siblingIndent, out var siblingOrdered, out _) &&
                    siblingIndent == baseIndent &&
                    siblingOrdered == ordered)
                    break;

                itemLines.Add(lines[index]);
                index++;
            }

            items.Add(ParseBlocks(string.Join('\n', itemLines)));
        }

        block = new ListBlock(string.Join('\n', lines), ordered, items);
        return true;
    }

    private static ParagraphBlock ParseParagraph(string[] lines, ref int index)
    {
        var parts = new List<string>();

        while (index < lines.Length)
        {
            if (string.IsNullOrWhiteSpace(lines[index]))
                break;

            if (parts.Count > 0 && IsBlockBoundary(lines[index]))
                break;

            parts.Add(lines[index].TrimEnd());
            index++;
        }

        return new ParagraphBlock(string.Join('\n', parts), string.Join('\n', parts));
    }

    private static bool IsBlockBoundary(string line)
    {
        var trimmed = line.Trim();
        return trimmed.StartsWith("```", StringComparison.Ordinal) ||
               trimmed.StartsWith(">", StringComparison.Ordinal) ||
               Regex.IsMatch(trimmed, @"^(#{1,6})\s+") ||
               Regex.IsMatch(trimmed, @"^\s*[-*+]\s+") ||
               Regex.IsMatch(trimmed, @"^\s*\d+\.\s+") ||
               IsHorizontalRule(trimmed);
    }

    private static bool TryMatchListItem(string line, out int indent, out bool ordered, out string content)
    {
        indent = 0;
        ordered = false;
        content = string.Empty;

        var orderedMatch = Regex.Match(line, @"^(?<indent>\s*)(?<marker>\d+\.)\s+(?<content>.+)$");
        if (orderedMatch.Success)
        {
            indent = orderedMatch.Groups["indent"].Value.Length;
            ordered = true;
            content = orderedMatch.Groups["content"].Value;
            return true;
        }

        var unorderedMatch = Regex.Match(line, @"^(?<indent>\s*)[-*+]\s+(?<content>.+)$");
        if (!unorderedMatch.Success)
            return false;

        indent = unorderedMatch.Groups["indent"].Value.Length;
        content = unorderedMatch.Groups["content"].Value;
        return true;
    }

    private static List<string>? TrySplitTableRow(string line)
    {
        if (!line.Contains('|'))
            return null;

        var trimmed = line.Trim();
        if (!trimmed.Contains('|'))
            return null;

        var cells = new List<string>();
        var current = new StringBuilder();
        var startIndex = trimmed.StartsWith("|", StringComparison.Ordinal) ? 1 : 0;
        var endIndex = trimmed.EndsWith("|", StringComparison.Ordinal) ? trimmed.Length - 1 : trimmed.Length;

        for (var index = startIndex; index < endIndex; index++)
        {
            var ch = trimmed[index];
            if (ch == '\\' && index + 1 < endIndex)
            {
                current.Append(trimmed[index + 1]);
                index++;
                continue;
            }

            if (ch == '|')
            {
                cells.Add(current.ToString().Trim());
                current.Clear();
                continue;
            }

            current.Append(ch);
        }

        cells.Add(current.ToString().Trim());
        return cells;
    }

    private Control CreateLinkButton(string label, string url)
    {
        var button = new Button
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            Margin = new Thickness(0),
            CornerRadius = new CornerRadius(0),
            Content = new TextBlock
            {
                Text = label,
                Foreground = AccentBrush,
                TextDecorations = TextDecorations.Underline
            }
        };

        button.Click += async (_, _) => await OpenUrlAsync(url);
        return button;
    }

    private Button CreateCodeActionButton(string text, Func<object?, EventArgs, Task> onClick)
    {
        var label = new TextBlock
        {
            Text = text,
            FontSize = 11,
            Foreground = MutedTextBrush
        };

        var button = new Button
        {
            Background = Brushes.Transparent,
            BorderBrush = SurfaceBorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(8, 4),
            HorizontalAlignment = HorizontalAlignment.Right,
            Content = label
        };

        button.Click += async (sender, args) => await onClick(sender, args);
        return button;
    }

    private async Task CopyCodeToClipboardAsync(string code)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.Clipboard is { } clipboard)
            await clipboard.SetTextAsync(code);
    }

    private async Task OpenUrlAsync(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.Launcher is { } launcher)
            await launcher.LaunchUriAsync(uri);
    }

    private abstract record MarkdownBlock(string RawText);
    private sealed record HeadingBlock(string RawText, string Text, int Level) : MarkdownBlock(RawText);
    private sealed record ParagraphBlock(string RawText, string Text) : MarkdownBlock(RawText);
    private sealed record CodeBlock(string RawText, string Code, string? Language) : MarkdownBlock(RawText);
    private sealed record QuoteBlock(string RawText, IReadOnlyList<MarkdownBlock> Blocks) : MarkdownBlock(RawText);
    private sealed record ListBlock(string RawText, bool Ordered, IReadOnlyList<List<MarkdownBlock>> Items) : MarkdownBlock(RawText);
    private sealed record TableBlock(string RawText, IReadOnlyList<IReadOnlyList<string>> Rows) : MarkdownBlock(RawText);
    private sealed record HorizontalRuleBlock(string RawText) : MarkdownBlock(RawText);
}
