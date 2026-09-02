using System.Globalization;

namespace HonorBatterySaver.Core;

public static class ProjectInfo
{
    public const string WebsiteUrl = "https://honor-battery-saver.onrender.com/en/";
    public const string RussianWebsiteUrl = "https://honor-battery-saver.onrender.com/ru/";
    public const string RepositoryUrl = "https://github.com/vyach-vasiliev/honor-battery-saver";
    public const string IssuesUrl = RepositoryUrl + "/issues";
    public const string FeedbackUrl = "https://thebestofflineapp.canny.io/honor-battery-saver-feedback";
    public const string CopyrightNotice = "© 2026 Honor Battery Saver contributors · MIT";

    public static string GetWebsiteUrl(CultureInfo culture) =>
        Strings.ResolveCulture(culture.Name).TwoLetterISOLanguageName == "ru"
            ? RussianWebsiteUrl
            : WebsiteUrl;
}
