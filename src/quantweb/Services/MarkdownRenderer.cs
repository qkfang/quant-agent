using Markdig;

namespace quantweb.Services;

public static class MarkdownRenderer
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .UseSoftlineBreakAsHardlineBreak()
        .Build();

    public static string Render(string? content)
    {
        if (string.IsNullOrEmpty(content)) return string.Empty;

        // Collapse 3+ consecutive newlines down to a single blank line to reduce excess vertical gaps.
        content = System.Text.RegularExpressions.Regex.Replace(content, @"(\r?\n){3,}", "\n\n");

        // Pre-translate citation tokens to inline HTML; Markdig preserves raw HTML.
        content = System.Text.RegularExpressions.Regex.Replace(
            content, @"【(\d+:\d+†.+?)】", "<sup class=\"citation\">[$1]</sup>");
        content = System.Text.RegularExpressions.Regex.Replace(
            content, @"\[doc(\d+)\]", "<sup class=\"citation\">[doc$1]</sup>");

        return Markdown.ToHtml(content, Pipeline);
    }
}
