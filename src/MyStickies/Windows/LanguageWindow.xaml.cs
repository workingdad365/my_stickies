using System.Globalization;
using System.Windows;

namespace MyStickies.Windows;

/// <summary>최초 실행 시 두 언어를 함께 표시하여 앱 언어를 선택함</summary>
public partial class LanguageWindow : Window
{
    public string SelectedLanguage => LanguageBox.SelectedValue as string ?? "ko";

    public LanguageWindow()
    {
        InitializeComponent();
        LanguageBox.SelectedValue = CultureInfo.InstalledUICulture.TwoLetterISOLanguageName == "ko" ? "ko" : "en";
    }

    private void Continue_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
