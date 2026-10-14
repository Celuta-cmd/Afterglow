using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Microsoft.Win32;

namespace Afterglow;
internal static class InstallerLifecycle
{
    internal static string EventName=>"Local\\Afterglow.Exit."+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Environment.ProcessPath!.ToLowerInvariant())))[..16];
    internal static bool RequestExit()
    {
        try{using var handle=EventWaitHandle.OpenExisting(EventName);handle.Set();return true;}catch(WaitHandleCannotBeOpenedException){return false;}
    }
    internal static void Cleanup()
    {
        // Signal only a process launched from this exact installation path.
        RequestExitAndWait();
        using var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run",true);
        var value=key?.GetValue("Afterglow") as string;
        if(value?.StartsWith("\""+Environment.ProcessPath+"\"",StringComparison.OrdinalIgnoreCase)==true)key!.DeleteValue("Afterglow",false);
    }
    internal static void RequestExitAndWait()
    {
        if(!RequestExit())return;
        for(int attempt=0;attempt<50;attempt++){Thread.Sleep(100);try{using var handle=EventWaitHandle.OpenExisting(EventName);}catch(WaitHandleCannotBeOpenedException){return;}}
    }
}
