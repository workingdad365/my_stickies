using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using MyStickies.Localization;

namespace MyStickies.Windows;

/// <summary>최초 실행 시 언어를 선택하고 해당 언어로 안내를 표시함</summary>
public partial class LanguageWindow : Window
{
    public string SelectedLanguage => LanguageBox.SelectedValue as string ?? "ko";

    public LanguageWindow()
    {
        InitializeComponent();
        LanguageBox.SelectedValue = Strings.PreferredLanguage(CultureInfo.InstalledUICulture);
    }

    private void LanguageBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ContinueButton is null) return;
        var catalog = Strings.Catalog(SelectedLanguage);
        Title = $"My Stickies · {catalog["Language"]}";
        Heading.Text = catalog["ChooseLanguage"];
        LanguageHint.Text = catalog["LanguageLater"];
        ContinueButton.Content = catalog["Continue"];
    }

    private void Continue_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
