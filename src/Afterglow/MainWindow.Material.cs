using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Afterglow;
public partial class MainWindow
{
    private void InitializeMaterials()=>SourceInitialized+=(_,_)=>
    {
        if(AutomationMode||SystemParameters.HighContrast)return;
        var handle=new WindowInteropHelper(this).Handle;int rounded=2;
        DwmSetWindowAttribute(handle,33,ref rounded,sizeof(int));
        int mica=2;
        if(DwmSetWindowAttribute(handle,38,ref mica,sizeof(int))==0)
        {
            var chrome=System.Windows.Shell.WindowChrome.GetWindowChrome(this);if(chrome is not null)chrome.GlassFrameThickness=new Thickness(-1);
            Background=Brushes.Transparent;StudioSurface.Background=Brushes.Transparent;
        }
    };
    [DllImport("dwmapi.dll")]private static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size);
}
