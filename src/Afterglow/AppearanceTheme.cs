using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace Afterglow;
internal static class AppearanceTheme
{
    internal static void Apply(string theme)
    {
        if(theme=="System") { using var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");theme=key?.GetValue("AppsUseLightTheme") is int value&&value==0?"Dark":"Light"; }
        var colors=theme=="Dark"?new Dictionary<string,string>
        {{"WindowBrush","#1E1E21"},{"ToolbarBrush","#EE29292D"},{"PanelBrush","#27272B"},{"SidebarBrush","#EE242427"},{"SurfaceBrush","#333338"},{"TextBrush","#F5F5F7"},{"MutedBrush","#B2B2BA"},{"FaintBrush","#85858F"},{"StrokeBrush","#20FFFFFF"},{"SelectionBrush","#25EF3655"}}:
        new Dictionary<string,string>
        {{"WindowBrush","#FAFAFB"},{"ToolbarBrush","#EEF3F2F4"},{"PanelBrush","#FFFFFF"},{"SidebarBrush","#EEF0EFF2"},{"SurfaceBrush","#EFEFF2"},{"TextBrush","#222225"},{"MutedBrush","#65656E"},{"FaintBrush","#8A8A94"},{"StrokeBrush","#1E000000"},{"SelectionBrush","#18E9364F"}};
        foreach(var entry in colors)Application.Current.Resources[entry.Key]=new SolidColorBrush((Color)ColorConverter.ConvertFromString(entry.Value));
        Application.Current.Resources["OnAccentBrush"]=Brushes.White;Application.Current.Resources["SelectedTextBrush"]=Application.Current.Resources["AccentBrush"];
        if(SystemParameters.HighContrast){foreach(var name in new[]{"WindowBrush","ToolbarBrush","PanelBrush","SidebarBrush","SurfaceBrush"})Application.Current.Resources[name]=SystemColors.WindowBrush;foreach(var name in new[]{"TextBrush","MutedBrush","FaintBrush"})Application.Current.Resources[name]=SystemColors.WindowTextBrush;Application.Current.Resources["SelectionBrush"]=SystemColors.HighlightBrush;Application.Current.Resources["AccentBrush"]=SystemColors.HighlightBrush;Application.Current.Resources["OnAccentBrush"]=SystemColors.HighlightTextBrush;Application.Current.Resources["SelectedTextBrush"]=SystemColors.HighlightTextBrush;}
    }
}
