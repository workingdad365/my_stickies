using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using MyStickies.Models;

namespace MyStickies.Data;

/// <summary>메모를 Markdown/평문으로 변환하고, 파일 내용을 메모로 해석</summary>
public static partial class NoteExporter
{
    private const string MetaPrefix = "<!-- mystickies:";

    [GeneratedRegex(@"^<!--\s*mystickies:.*?-->\s*$", RegexOptions.Multiline)]
    private static partial Regex MetaLine();

    /// <summary>한 장을 Markdown으로. 제목은 1단계 헤딩, 끝에 메타데이터 주석</summary>
    public static string ToMarkdown(Note note)
    {
        var sb = new StringBuilder();
        sb.Append("# ").AppendLine(note.Title);
        sb.AppendLine();
        if (note.Body.Length > 0)
        {
            sb.AppendLine(note.Body.TrimEnd());
            sb.AppendLine();
        }
        sb.Append(MetaPrefix)
          .Append(" created=").Append(Iso(note.CreatedAt))
          .Append(" updated=").Append(Iso(note.UpdatedAt))
          .Append(" color=").Append(note.ColorHex)
          .Append(" archived=").Append(note.IsArchived ? "true" : "false")
          .AppendLine(" -->");
        return sb.ToString();
    }

    /// <summary>여러 장을 하나의 Markdown 문서로. 장 사이는 수평선</summary>
    public static string ToMarkdown(IEnumerable<Note> notes)
    {
        var parts = notes.Select(ToMarkdown).ToList();
        return string.Join(Environment.NewLine + "---" + Environment.NewLine + Environment.NewLine, parts);
    }

    /// <summary>한 장을 평문으로. 제목, 빈 줄, 본문</summary>
    public static string ToPlainText(Note note)
    {
        var sb = new StringBuilder();
        sb.AppendLine(note.Title);
        if (note.Body.Length > 0)
        {
            sb.AppendLine();
            sb.AppendLine(note.Body.TrimEnd());
        }
        return sb.ToString();
    }

    /// <summary>
    /// 파일 내용을 (제목, 본문)으로 해석.
    /// 첫 줄이 "# 제목"이면 그것을 제목으로, 아니면 파일 이름을 제목으로 사용.
    /// 내보내기 때 붙인 메타데이터 주석은 제거
    /// </summary>
    public static (string Title, string Body) Parse(string content, string fileName)
    {
        var text = MetaLine().Replace(content.Replace("\r\n", "\n"), string.Empty).Trim('\n', ' ', '\t');
        var lines = text.Split('\n');

        string title;
        IEnumerable<string> bodyLines;
        if (lines.Length > 0 && lines[0].StartsWith("# ", StringComparison.Ordinal))
        {
            title = lines[0][2..].Trim();
            bodyLines = lines.Skip(1);
        }
        else
        {
            title = Path.GetFileNameWithoutExtension(fileName).Trim();
            bodyLines = lines;
        }

        var body = string.Join('\n', bodyLines).Trim('\n', ' ', '\t');
        return (title, body);
    }

    private static string Iso(DateTime value) => value.ToString("o", CultureInfo.InvariantCulture);
}
