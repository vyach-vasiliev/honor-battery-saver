using System.Globalization;
using HonorBatterySaver.Core;

namespace HonorBatterySaver.Core.Tests;

public sealed class ProjectInfoTests
{
    [Theory]
    [InlineData("ru", ProjectInfo.RussianWebsiteUrl)]
    [InlineData("ru-RU", ProjectInfo.RussianWebsiteUrl)]
    [InlineData("en-US", ProjectInfo.WebsiteUrl)]
    [InlineData("en-GB", ProjectInfo.WebsiteUrl)]
    [InlineData("de-DE", ProjectInfo.WebsiteUrl)]
    public void WebsiteFollowsSupportedUiLanguage(string culture, string expectedUrl) =>
        Assert.Equal(expectedUrl, ProjectInfo.GetWebsiteUrl(CultureInfo.GetCultureInfo(culture)));

    [Theory]
    [InlineData(ProjectInfo.WebsiteUrl, "honor-battery-saver.onrender.com", "/en/")]
    [InlineData(ProjectInfo.RussianWebsiteUrl, "honor-battery-saver.onrender.com", "/ru/")]
    [InlineData(ProjectInfo.RepositoryUrl, "github.com", "/vyach-vasiliev/honor-battery-saver")]
    [InlineData(ProjectInfo.IssuesUrl, "github.com", "/vyach-vasiliev/honor-battery-saver/issues")]
    [InlineData(ProjectInfo.FeedbackUrl, "thebestofflineapp.canny.io", "/honor-battery-saver-feedback")]
    public void LinksUseExpectedHttpsDestinationsWithoutAttachedData(string url, string host, string path)
    {
        var uri = new Uri(url, UriKind.Absolute);

        Assert.Equal("https", uri.Scheme);
        Assert.Equal(host, uri.Host);
        Assert.Equal(path, uri.AbsolutePath);
        Assert.Empty(uri.UserInfo);
        Assert.Empty(uri.Query);
        Assert.Empty(uri.Fragment);
    }

    [Theory]
    [InlineData("en-US", "Website", "Issues", "Ideas & feedback")]
    [InlineData("ru", "Сайт", "Проблемы", "Идеи и отзывы")]
    public void FooterLabelsAndBrowserErrorAreLocalized(string cultureName, string website, string issues, string feedback)
    {
        var culture = CultureInfo.GetCultureInfo(cultureName);

        Assert.Equal(website, Strings.Get("Project_Website", culture));
        Assert.Equal(issues, Strings.Get("Project_Issues", culture));
        Assert.Equal(feedback, Strings.Get("Project_Feedback", culture));
        Assert.Contains(ProjectInfo.WebsiteUrl, Strings.Format(culture, "Project_OpenFailed", ProjectInfo.WebsiteUrl));
    }
}
