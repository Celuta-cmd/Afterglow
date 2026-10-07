using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace Afterglow;
internal static class AppearanceTheme
{
    internal static void Apply(string theme)
    {
        var colors=theme=="Dark"?new Dictionary<string,string>
        {{"WindowBrush","#292326"},{"PanelBrush","#ED30292D"},{"SidebarBrush","#D2332B2F"},{"SurfaceBrush","#CC453B40"},{"TextBrush","#F4EFF2"},{"MutedBrush","#B7A9B1"},{"FaintBrush","#8F8189"},{"StrokeBrush","#30A699A0"},{"SelectionBrush","#35FA2D48"}}:
        new Dictionary<string,string>
        {{"WindowBrush","#E9E6E4"},{"PanelBrush","#EFFFFFFF"},{"SidebarBrush","#CDEFECEB"},{"SurfaceBrush","#C4FFFFFF"},{"TextBrush","#282326"},{"MutedBrush","#80767B"},{"FaintBrush","#A3989F"},{"StrokeBrush","#26877B81"},{"SelectionBrush","#1AFA2D48"}};
        foreach(var entry in colors)Application.Current.Resources[entry.Key]=new SolidColorBrush((Color)ColorConverter.ConvertFromString(entry.Value));
    }
}
