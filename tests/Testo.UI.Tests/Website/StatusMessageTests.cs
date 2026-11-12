using Bunit;
using Testo.UI.Tests.Support;
using Testo.Website.Components;

namespace Testo.UI.Tests.Website;

public class StatusMessageTests : DesignSystemTestContext
{
    [Fact]
    public void Render_RequiredTexts_ShowsCodeTitleAndText()
    {
        var cut = Render<StatusMessage>(parameters => parameters
            .Add(component => component.Code, "Error 404")
            .Add(component => component.Title, "Page not found")
            .Add(component => component.Text, "It does not exist."));

        Assert.Equal("Page not found", cut.Find("h1").TextContent);
        Assert.Equal("Error 404", cut.Find(".status-message__code").TextContent);
        Assert.Contains("It does not exist.", cut.Markup, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll(".status-message__actions"));
        Assert.Empty(cut.FindAll(".status-message__extra"));
    }

    [Fact]
    public void Render_ActionsAndDetails_ShowsBoth()
    {
        var cut = Render<StatusMessage>(parameters => parameters
            .Add(component => component.Code, "Error")
            .Add(component => component.Title, "Something went wrong")
            .Add(component => component.Text, "Try again.")
            .AddChildContent("<p>Request ID: 42</p>")
            .Add(component => component.Actions, "<a href=\"/en\">Home</a>"));

        Assert.Equal("Request ID: 42", cut.Find(".status-message__extra p").TextContent);
        Assert.Equal("/en", cut.Find(".status-message__actions a").GetAttribute("href"));
    }
}
