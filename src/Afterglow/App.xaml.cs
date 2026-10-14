using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Afterglow;

public partial class App : System.Windows.Application
{
    private Mutex? mutex;
    private System.Windows.Forms.NotifyIcon? tray;
    private Bridge? bridge;
    private MainWindow? studio;
    private Preferences? settings;
    private bool exiting;
    private EventWaitHandle? exitSignal;
    private RegisteredWaitHandle? exitRegistration;
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if(e.Args.Contains("--setup-test")||e.Args.Contains("--setup-preview"))
        {
            ShutdownMode=ShutdownMode.OnExplicitShutdown;
            try
            {
                var guide=new ConnectWindow(new Preferences(),new Bridge(new Preferences().Token));guide.Show();guide.VerifyGuide();await Task.Delay(300);
                if(e.Args.Contains("--setup-preview")){var content=(FrameworkElement)guide.Content;var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap((int)content.ActualWidth,(int)content.ActualHeight,96,96,System.Windows.Media.PixelFormats.Pbgra32);bitmap.Render(content);WallpaperRenderer.Save(bitmap,e.Args.Last());}
                else File.WriteAllText(e.Args.Last(),"PASS: Browser guide opens, manual installation stays navigable, finish waits for a real song, and UI closes cleanly.\n");
                guide.Close();Shutdown(0);
            }catch(Exception ex){File.WriteAllText(e.Args.Last(),ex.ToString());Shutdown(1);}return;
        }
        if(e.Args.Contains("--ui-test")){ShutdownMode=ShutdownMode.OnExplicitShutdown;try{var test=new MainWindow(new Preferences(),new Bridge(new Preferences().Token)){AutomationMode=true,Title="Afterglow interface check"};test.Show();await test.PreviewReady;test.VerifyNavigation();test.Exiting=true;test.Close();File.WriteAllText(e.Args.Last(),"PASS: Five working navigation destinations, selection state, and sidebar collapse/expand.\n");Shutdown(0);}catch(Exception ex){File.WriteAllText(e.Args.Last(),ex.ToString());Shutdown(1);}return;}
        if(e.Args.Contains("--installer-cleanup")){InstallerLifecycle.Cleanup();Shutdown();return;}
        if(e.Args.Contains("--request-exit")){InstallerLifecycle.RequestExitAndWait();Shutdown();return;}
        if(e.Args.Contains("--motion-test")){ShutdownMode=ShutdownMode.OnExplicitShutdown;try{var test=new MainWindow(new Preferences(),new Bridge(new Preferences().Token)){AutomationMode=true,Title="Afterglow window animation check"};test.Show();await test.PreviewReady;await test.VerifyMotionAsync();File.WriteAllText(e.Args.Last(),"PASS: Maximize stays within the monitor work area.\nPASS: Restore keeps original window dimensions.\nPASS: Minimize completes and restores full opacity.\nNo wallpaper, playback or saved configuration was changed.\n");Shutdown(0);}catch(Exception ex){File.WriteAllText(e.Args.Last(),ex.ToString());Shutdown(1);}return;}
        if(e.Args.Contains("--lyrics-test")){ShutdownMode=ShutdownMode.OnExplicitShutdown;try{await LyricsAcceptance.Run(e.Args.Last());Shutdown(0);}catch(Exception ex){File.WriteAllText(e.Args.Last(),ex.ToString());Shutdown(1);}return;}
        if(e.Args.Contains("--transition-test")){ShutdownMode=ShutdownMode.OnExplicitShutdown;try{await TransitionAcceptance.RunAsync(e.Args.Last());Shutdown(0);}catch(Exception ex){File.WriteAllText(e.Args.Last(),ex.ToString());Shutdown(1);}return;}
        if(e.Args.Contains("--wallpaper-test")){try{WallpaperAcceptance.Run(e.Args.Last());Shutdown(0);}catch(Exception ex){File.WriteAllText(e.Args.Last(),ex.ToString());Shutdown(1);}return;}
        if(e.Args.Contains("--self-test")){try{await SelfTest.Run(e.Args.Last());Shutdown(0);}catch(Exception ex){File.WriteAllText(e.Args.Last(),ex.ToString());Shutdown(1);}return;}
        bool preview=e.Args.Contains("--preview");
        if(!preview)
        {
            mutex=new Mutex(true,@"Local\Afterglow.Windows",out var created);
            if(!created){MessageBox.Show("Afterglow is already running. Open it from the system tray.","Afterglow");Shutdown();return;}
        }
        settings=preview?new Preferences{Theme=e.Args.Contains("--dark")?"Dark":"Light",ShowLyrics=e.Args.Contains("--lyrics")}:Preferences.Load();bridge=new Bridge(settings.Token);
        studio=new MainWindow(settings,bridge){AutomationMode=preview};MainWindow=studio;
        if(preview)
        {
            studio.Show();if(e.Args.Contains("--page"))studio.Navigate(e.Args[Array.IndexOf(e.Args,"--page")+1],false);await studio.PreviewReady;if(settings.ShowLyrics)studio.PreviewLyricsDemo();await Task.Delay(500);
            var content=(FrameworkElement)studio.Content;
            var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap((int)content.ActualWidth,(int)content.ActualHeight,96,96,System.Windows.Media.PixelFormats.Pbgra32);
            bitmap.Render(content);WallpaperRenderer.Save(bitmap,e.Args.Last());studio.Exiting=true;studio.Close();Shutdown();return;
        }
        DispatcherUnhandledException+=(_,args)=>{File.AppendAllText(Path.Combine(Preferences.DirectoryPath,"errors.log"),DateTime.Now+" "+args.Exception+Environment.NewLine);MessageBox.Show(args.Exception.Message,"Afterglow");args.Handled=true;};
        settings.Save();
        try{await bridge.StartAsync();}catch(Exception ex){MessageBox.Show("The browser bridge could not start on port 47836. Close any other Afterglow instance or app using that port, then restart.\n\n"+ex.Message,"Afterglow connection");}
        exitSignal=new EventWaitHandle(false,EventResetMode.AutoReset,InstallerLifecycle.EventName);exitRegistration=ThreadPool.RegisterWaitForSingleObject(exitSignal,(_,_)=>Dispatcher.InvokeAsync(async()=>await ExitAsync()),null,Timeout.Infinite,true);
        if(Desktop.StartWithWindows)Desktop.StartWithWindows=true;
        CreateTray();if(!e.Args.Contains("--tray")){studio.Show();if(!settings.SetupGuideSeen)_ = Dispatcher.BeginInvoke(new Action(()=>studio.ShowSetupGuide()));}
        SystemEventsHook();
    }
    private void CreateTray()
    {
        var menu=new System.Windows.Forms.ContextMenuStrip();menu.Items.Add("Open Afterglow",null,(_,_)=>Dispatcher.Invoke(()=>studio!.ShowStudio()));
        menu.Items.Add("Open YouTube Music",null,(_,_)=>MainWindowOpen());menu.Items.Add("Exit Afterglow",null,async(_,_)=>await ExitAsync());
        tray=new(){Icon=Icon.ExtractAssociatedIcon(Environment.ProcessPath!)??SystemIcons.Application,Text="Afterglow · YouTube Music wallpapers",ContextMenuStrip=menu,Visible=true};tray.DoubleClick+=(_,_)=>Dispatcher.Invoke(()=>studio!.ShowStudio());
    }
    private static void MainWindowOpen()=>Afterglow.MainWindow.Open("https://music.youtube.com/");
    private void SystemEventsHook()=>Microsoft.Win32.SystemEvents.SessionEnding+=OnSessionEnding;
    private void OnSessionEnding(object sender,Microsoft.Win32.SessionEndingEventArgs e){if(settings?.RestoreOnExit==true)try{Desktop.Restore(settings);}catch{};}
    private async Task ExitAsync()
    {
        if(exiting)return;exiting=true;
        if(studio is not null)await studio.PrepareExitAsync();
        if(settings?.RestoreOnExit==true)try{Desktop.Restore(settings);}catch(Exception ex){MessageBox.Show(ex.Message,"Wallpaper restoration");}
        if(studio is not null){studio.Exiting=true;studio.Close();}
        tray?.Dispose();if(bridge is not null)await bridge.DisposeAsync();settings?.Save();Shutdown();
    }
    protected override void OnExit(ExitEventArgs e)
    {
        exitRegistration?.Unregister(null);exitSignal?.Dispose();tray?.Dispose();Microsoft.Win32.SystemEvents.SessionEnding-=OnSessionEnding;mutex?.Dispose();base.OnExit(e);
    }
}
