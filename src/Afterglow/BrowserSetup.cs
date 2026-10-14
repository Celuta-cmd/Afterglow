using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Win32;

namespace Afterglow;

internal sealed record SetupBrowser(string Name,string Executable,string ExtensionsUrl)
{public override string ToString()=>Name;}

internal static class BrowserSetup
{
    internal static string ExtensionFolder=>Path.Combine(Preferences.DirectoryPath,"browser-extension");
    internal static string PrepareExtension(Preferences preferences,string? source=null,string? destination=null)
    {
        source??=Path.Combine(AppContext.BaseDirectory,"extension");
        destination??=ExtensionFolder;
        if(!Directory.Exists(source)||!File.Exists(Path.Combine(source,"manifest.json")))
            throw new IOException("The browser extension files are missing. Repair Afterglow using its MSI installer, then try again.");
        if(preferences.Token.Length!=48||!preferences.Token.All(Uri.IsHexDigit))throw new InvalidOperationException("The pairing key is invalid.");
        // A separate per-user folder stays stable across MSI updates. Only shipped extension files are copied.
        Directory.CreateDirectory(destination);
        foreach(var path in Directory.GetFiles(source,"*",SearchOption.AllDirectories))
        {
            var target=Path.Combine(destination,Path.GetRelativePath(source,path));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);File.Copy(path,target,true);
        }
        File.WriteAllText(Path.Combine(destination,"pairing.js"),"\"use strict\";\nglobalThis.AFTERGLOW_SETUP_TOKEN = "+JsonSerializer.Serialize(preferences.Token)+";\n");
        return destination;
    }
    internal static IReadOnlyList<SetupBrowser> FindBrowsers()
    {
        var result=new List<SetupBrowser>();
        foreach(var entry in new[]{("Microsoft Edge","msedge.exe","edge://extensions","Microsoft/Edge/Application/msedge.exe"),("Google Chrome","chrome.exe","chrome://extensions","Google/Chrome/Application/chrome.exe")})
        {
            string? executable=null;
            foreach(var hive in new[]{Registry.CurrentUser,Registry.LocalMachine})
            {using var key=hive.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\App Paths\"+entry.Item2);var path=key?.GetValue(null) as string;if(path is not null&&File.Exists(path)){executable=path;break;}}
            executable??=new[]{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}.Select(root=>Path.Combine(root,entry.Item4.Replace('/',Path.DirectorySeparatorChar))).FirstOrDefault(File.Exists);
            if(executable is not null)result.Add(new(entry.Item1,executable,entry.Item3));
        }
        return result;
    }
    internal static void Open(SetupBrowser browser,string url)
    {
        var start=new ProcessStartInfo(browser.Executable){UseShellExecute=true};start.ArgumentList.Add(url);Process.Start(start);
    }
}
