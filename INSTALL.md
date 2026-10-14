# Install Afterglow

Run Afterglow-2.1.1-x64.msi and follow the wizard. The default is a per-user install in `%LOCALAPPDATA%\Programs\Afterglow`, with a Start menu entry and an Afterglow Studio desktop shortcut. Windows 10 version 1809 or newer, 64-bit, is required. The .NET desktop/runtime dependencies and native graphics helper are bundled, so there is no separate runtime installer.

Close any earlier portable Afterglow from its tray menu before opening the installed version. Both use the same settings, pairing key, local bridge, and single-instance identity. The installer does not overwrite the old Desktop folder. If Windows startup was already enabled, opening the installed app redirects that entry to the installed executable.

## Browser setup

Leave "Open Afterglow and set up your browser" checked on the MSI's final screen. On a new installation, Afterglow opens a three-step guide automatically. Choose Microsoft Edge or Google Chrome, then select Prepare & open browser. The app creates a private, pre-paired extension copy in `%LOCALAPPDATA%\Afterglow\browser-extension`, copies its folder path, and opens the selected browser's Extensions page.

In the browser, turn on Developer mode, choose Load unpacked, paste the copied folder path into the folder picker, and select that folder. These approval clicks are required for the current unpacked extension. A published store extension could shorten this in a future release. The MSI does not change browser policies or secretly install an extension.

The extension's connection page opens automatically and checks its saved pairing key. Return to the guide, choose Open music, and play a song on YouTube Music. Refresh an already-open music tab once after loading the extension. Finish setup becomes available after an actual music connection; successful pairing alone does not falsely mark the song connection complete. Automatic wallpaper following is an optional checkbox.

The guide can be skipped and reopened from Connection. Existing profiles are not forced through setup after an upgrade, and the chosen browser is remembered. Manual pairing remains available for an extension loaded from the original installed `extension` folder. When updating an already-loaded private copy, reload the extension in the browser and refresh YouTube Music. The prepared folder contains this PC's private pairing key. Do not share it; redistributable installer and source archives contain an empty pairing.js instead.

## Maintenance and data

Windows Apps settings can uninstall the package. Repair is available by rerunning the MSI or Windows Installer repair. Later MSI versions use a stable upgrade identity and block downgrades. Uninstall closes a running app from that installation path, respecting its restore-on-exit preference, and removes only an autorun entry that points to that executable. Other portable copies are not closed.

User configuration in `%LOCALAPPDATA%\Afterglow`, cached native helpers, and wallpaper exports/backups in Pictures\Afterglow are preserved. They are not installer components. Empty installed program folders and the package's shortcuts are removed. Additional user-created files are not recursively deleted.

The MSI and executable are unsigned. No signing identity or certificate is fabricated.

## Building

Run `build.ps1` for the portable app, then `installer\build-installer.ps1 -SkipPublish` for the MSI. The installer script uses WiX 5.0.2 and its UI extension from NuGet. A .NET 8 SDK and Visual Studio C++ Build Tools are needed to build the app. WiX and installer sources are included; tools and local build caches are excluded from source archives.
