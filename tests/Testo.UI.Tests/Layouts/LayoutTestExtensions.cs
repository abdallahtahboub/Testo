using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;

namespace Testo.UI.Tests.Layouts;

internal static class LayoutTestExtensions
{
    /// <summary>The elements a keyboard user can reach with Tab, in document order.</summary>
    public static IReadOnlyList<IElement> TabStops<TComponent>(this IRenderedComponent<TComponent> cut)
        where TComponent : IComponent =>
        [.. cut.FindAll("a[href], button, input:not([type=hidden]), select, textarea, [tabindex]")
            .Where(element => element.GetAttribute("tabindex") != "-1")];
}
