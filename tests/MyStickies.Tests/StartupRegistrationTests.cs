using MyStickies.Data;

namespace MyStickies.Tests;

public class StartupRegistrationTests
{
    [Fact]
    public void BuildCommand_QuotesPathWithSpaces()
    {
        Assert.Equal("\"C:\\Program Files\\My Stickies\\MyStickies.exe\"",
            StartupRegistration.BuildCommand(@"C:\Program Files\My Stickies\MyStickies.exe"));
    }

    [Fact]
    public void IsUnderDirectory_TrueForFileInsideInstallFolder()
    {
        Assert.True(StartupRegistration.IsUnderDirectory(
            @"C:\Users\me\AppData\Local\Programs\MyStickies\MyStickies.exe",
            @"C:\Users\me\AppData\Local\Programs\MyStickies"));
    }

    [Fact]
    public void IsUnderDirectory_IgnoresCaseAndTrailingSeparator()
    {
        Assert.True(StartupRegistration.IsUnderDirectory(
            @"c:\users\ME\appdata\local\programs\mystickies\MyStickies.exe",
            @"C:\Users\me\AppData\Local\Programs\MyStickies\"));
    }

    [Fact]
    public void IsUnderDirectory_FalseForBuildOutput()
    {
        Assert.False(StartupRegistration.IsUnderDirectory(
            @"G:\project\my_stickies\src\MyStickies\bin\Debug\net9.0-windows\MyStickies.exe",
            @"C:\Users\me\AppData\Local\Programs\MyStickies"));
    }

    [Fact]
    public void IsUnderDirectory_FalseForSiblingFolderWithSamePrefix()
    {
        Assert.False(StartupRegistration.IsUnderDirectory(
            @"C:\Users\me\AppData\Local\Programs\MyStickiesOld\MyStickies.exe",
            @"C:\Users\me\AppData\Local\Programs\MyStickies"));
    }
}

public class AppInfoTests
{
    [Fact]
    public void Version_Is_1_0_8()
    {
        Assert.Equal("1.0.11", MyStickies.Data.AppInfo.Version);
    }
}
