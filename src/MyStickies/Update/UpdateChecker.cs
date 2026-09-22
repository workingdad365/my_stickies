using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using MyStickies.Data;

namespace MyStickies.Update;

/// <summary>GitHub 최신 릴리스의 버전과 설치 파일 정보</summary>
public sealed record UpdateInfo(Version Version, string Tag, string DownloadUrl, string FileName, long Size, string Notes);

/// <summary>GitHub Releases에서 새 버전 확인, 설치 파일 다운로드, 조용한 설치 실행</summary>
public static class UpdateChecker
{
    public const string Repository = "workingdad365/my_stickies";
    private const string LatestReleaseUrl = $"https://api.github.com/repos/{Repository}/releases/latest";

    /// <summary>릴리스 자산 중 설치 파일로 인식하는 이름 규칙: MyStickies-Setup-*.exe</summary>
    private const string InstallerPrefix = "MyStickies-Setup-";

    private static readonly HttpClient Api = CreateClient(TimeSpan.FromSeconds(30));
    private static readonly HttpClient Download = CreateClient(TimeSpan.FromMinutes(10));

    private static HttpClient CreateClient(TimeSpan timeout)
    {
        // GitHub API는 User-Agent 헤더가 없으면 403을 돌려줌
        var client = new HttpClient { Timeout = timeout };
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"MyStickies/{AppInfo.Version}");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }

    /// <summary>태그 문자열(v1.2.3 또는 1.2.3)을 버전으로. 형식이 다르면 null</summary>
    public static Version? ParseTag(string tag)
    {
        var text = tag.Trim();
        if (text.StartsWith('v') || text.StartsWith('V'))
            text = text[1..];
        return Version.TryParse(text, out var version) ? Normalize(version) : null;
    }

    /// <summary>비교용으로 Major.Minor.Build 세 자리로 맞춤. 빠진 자리는 0</summary>
    public static Version Normalize(Version version) =>
        new(version.Major, Math.Max(version.Minor, 0), Math.Max(version.Build, 0));

    public static bool IsNewer(Version latest, Version current) => Normalize(latest) > Normalize(current);

    /// <summary>
    /// 릴리스 JSON에서 버전과 설치 파일 정보를 추출.
    /// 초안이나 사전 릴리스, 설치 파일 자산이 없는 릴리스는 null
    /// </summary>
    public static UpdateInfo? Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object) return null;

        if (IsTrue(root, "draft") || IsTrue(root, "prerelease")) return null;

        var tag = GetString(root, "tag_name");
        if (tag is null) return null;
        var version = ParseTag(tag);
        if (version is null) return null;

        if (!root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var asset in assets.EnumerateArray())
        {
            var name = GetString(asset, "name");
            if (name is null
                || !name.StartsWith(InstallerPrefix, StringComparison.OrdinalIgnoreCase)
                || !name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                continue;

            var url = GetString(asset, "browser_download_url");
            if (url is null) continue;

            var size = asset.TryGetProperty("size", out var sizeEl) && sizeEl.ValueKind == JsonValueKind.Number
                ? sizeEl.GetInt64()
                : 0;
            var notes = GetString(root, "body") ?? string.Empty;
            return new UpdateInfo(version, tag, url, name, size, notes);
        }

        return null;
    }

    private static bool IsTrue(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.True;

    private static string? GetString(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String ? el.GetString() : null;

    /// <summary>최신 릴리스 조회. 현재보다 새 버전이면 정보 반환, 아니면 null. 네트워크 오류는 예외로 전달</summary>
    public static async Task<UpdateInfo?> CheckAsync(Version current, CancellationToken ct = default)
    {
        var json = await Api.GetStringAsync(LatestReleaseUrl, ct);
        var info = Parse(json);
        return info is not null && IsNewer(info.Version, current) ? info : null;
    }

    /// <summary>설치 파일을 임시 폴더에 다운로드하고 경로 반환. 진행률은 0~1</summary>
    public static async Task<string> DownloadAsync(UpdateInfo info, IProgress<double>? progress, CancellationToken ct)
    {
        var dir = Path.Combine(Path.GetTempPath(), "MyStickies", "update");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, info.FileName);

        using var response = await Download.GetAsync(info.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        var total = response.Content.Headers.ContentLength ?? info.Size;

        await using var source = await response.Content.ReadAsStreamAsync(ct);
        await using var target = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);

        var buffer = new byte[81920];
        long done = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, ct)) > 0)
        {
            await target.WriteAsync(buffer.AsMemory(0, read), ct);
            done += read;
            if (total > 0)
                progress?.Report(Math.Min(1.0, (double)done / total));
        }

        if (info.Size > 0 && done != info.Size)
            throw new IOException(MyStickies.Localization.Strings.Get("DownloadSizeMismatch", done, info.Size));
        return path;
    }

    /// <summary>
    /// 설치 파일을 조용히 실행. 설치기가 실행 중인 앱을 닫고 설치 후 /RELAUNCH=1로 다시 실행함.
    /// 호출 측은 곧바로 앱을 종료해야 단일 실행 뮤텍스가 풀림
    /// </summary>
    public static void RunInstaller(string path) =>
        Process.Start(new ProcessStartInfo(path)
        {
            Arguments = "/SILENT /CLOSEAPPLICATIONS /NORESTART /RELAUNCH=1",
            UseShellExecute = true,
        });
}
