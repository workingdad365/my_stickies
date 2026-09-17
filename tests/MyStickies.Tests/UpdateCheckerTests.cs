using MyStickies.Update;

namespace MyStickies.Tests;

public class UpdateCheckerTests
{
    private const string SampleRelease = """
        {
          "tag_name": "v1.0.4",
          "name": "v1.0.4",
          "draft": false,
          "prerelease": false,
          "body": "- 자동 업데이트 추가",
          "assets": [
            { "name": "MyStickies-1.0.4.zip", "size": 10, "browser_download_url": "https://example.com/a.zip" },
            { "name": "MyStickies-Setup-1.0.4.exe", "size": 50663130, "browser_download_url": "https://example.com/MyStickies-Setup-1.0.4.exe" }
          ]
        }
        """;

    [Theory]
    [InlineData("v1.0.4", 1, 0, 4)]
    [InlineData("1.0.4", 1, 0, 4)]
    [InlineData("V2.1", 2, 1, 0)]
    [InlineData(" v1.2.3 ", 1, 2, 3)]
    public void ParseTag_AcceptsVersionWithOrWithoutPrefix(string tag, int major, int minor, int build)
    {
        Assert.Equal(new Version(major, minor, build), UpdateChecker.ParseTag(tag));
    }

    [Theory]
    [InlineData("latest")]
    [InlineData("")]
    [InlineData("v")]
    public void ParseTag_ReturnsNullForInvalid(string tag)
    {
        Assert.Null(UpdateChecker.ParseTag(tag));
    }

    [Fact]
    public void IsNewer_ComparesThreePartVersions()
    {
        Assert.True(UpdateChecker.IsNewer(new Version(1, 0, 4), new Version(1, 0, 3)));
        Assert.True(UpdateChecker.IsNewer(new Version(1, 1), new Version(1, 0, 9)));
        Assert.False(UpdateChecker.IsNewer(new Version(1, 0, 3), new Version(1, 0, 3)));
        Assert.False(UpdateChecker.IsNewer(new Version(1, 0, 3, 0), new Version(1, 0, 3)));
        Assert.False(UpdateChecker.IsNewer(new Version(1, 0, 2), new Version(1, 0, 3)));
    }

    [Fact]
    public void Parse_PicksInstallerAssetAndNotes()
    {
        var info = UpdateChecker.Parse(SampleRelease);

        Assert.NotNull(info);
        Assert.Equal(new Version(1, 0, 4), info.Version);
        Assert.Equal("v1.0.4", info.Tag);
        Assert.Equal("MyStickies-Setup-1.0.4.exe", info.FileName);
        Assert.Equal("https://example.com/MyStickies-Setup-1.0.4.exe", info.DownloadUrl);
        Assert.Equal(50663130, info.Size);
        Assert.Equal("- 자동 업데이트 추가", info.Notes);
    }

    [Fact]
    public void Parse_ReturnsNullWithoutInstallerAsset()
    {
        var json = SampleRelease.Replace("MyStickies-Setup-1.0.4.exe", "MyStickies-Portable-1.0.4.exe");
        Assert.Null(UpdateChecker.Parse(json));
    }

    [Theory]
    [InlineData("\"draft\": false", "\"draft\": true")]
    [InlineData("\"prerelease\": false", "\"prerelease\": true")]
    public void Parse_IgnoresDraftAndPrerelease(string from, string to)
    {
        Assert.Null(UpdateChecker.Parse(SampleRelease.Replace(from, to)));
    }

    [Fact]
    public void Parse_ReturnsNullForUnexpectedShape()
    {
        Assert.Null(UpdateChecker.Parse("[]"));
        Assert.Null(UpdateChecker.Parse("{\"message\":\"Not Found\"}"));
        Assert.Null(UpdateChecker.Parse("{\"tag_name\":\"latest\",\"assets\":[]}"));
    }
}
