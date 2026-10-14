# Building Afterglow

Requires Windows, .NET 8 SDK 8.0.424 or a newer .NET 8 feature band, Visual Studio C++ build tools with the x64 compiler and Windows SDK, and Node.js for extension tests. There are no third-party NuGet or npm dependencies. C# source is in `src/Afterglow`, the C++ composition helper is in `src/Afterglow/Native`, and the Manifest V3 browser extension is in `extension`. The SDK version is selected through `global.json`.

The project builds the graphics helper automatically before compilation and embeds it in the executable. It uses Direct3D 11, Direct2D and DirectComposition from Windows, with the C++ runtime linked statically. End users do not need Visual Studio or a separate runtime installation. The embedded helper extracts to a hash-specific cache under local Afterglow app data when first used. Builds target x64.

From PowerShell in the extracted source folder:

```powershell
.\build.ps1 -Test
```

This publishes `portable/Afterglow.exe`, copies the extension and docs next to it, and runs the native integration/rendering checks plus the Node.js extension checks. The executable includes its .NET, WPF, and ASP.NET runtimes. Keep the `extension` folder alongside it for the in-app setup link.

The optional icon-generation script is `src/create-icon.ps1`. The generated icon is already included, so it is not needed for normal builds.

Diagnostic entry points:

```powershell
.\portable\Afterglow.exe --self-test C:\path\to\existing-folder\report.txt
.\portable\Afterglow.exe --preview C:\path\to\existing-folder\studio.png
```

The self-test launches an isolated loopback bridge on port 47837 and renders test wallpapers beside the report. It does not apply them to Windows. If that directory contains `live-track.json`, the test also fetches its allowed YouTube Music artwork URL and renders a wallpaper; this fixture is optional and not bundled. Preview captures the app's own visual tree and closes. Neither diagnostic entry point changes the wallpaper or startup setting.

Manual acceptance: load the extension, pair to the running app, play two YouTube Music songs, check that the cover changes, use play/pause/previous/next/seek, apply a wallpaper, test automatic follow while the studio is closed to tray, and restore the prior wallpaper. Also test your own monitor layout if it differs from the primary-monitor Fill configuration.

An explicitly opt-in native acceptance command is `Afterglow.exe --wallpaper-test C:\path\to\existing-folder\wallpaper-report.txt`. **This temporarily changes the wallpaper**, verifies every monitor's applied image, and restores the saved monitor images / slideshow configuration. Run it with Afterglow's automatic follow turned off. The ordinary `--self-test` remains read-only with respect to desktop settings.

`Afterglow.exe --transition-test C:\path\to\existing-folder\transition-report.txt` temporarily animates fixture wallpapers behind desktop icons, checks GPU texture pixels, monitor attachment, cancellation/retargeting, six rapid skips, final commit and cleanup, then restores the desktop. It also uses Desktop Duplication to sample actual displayed fixture colors and capture-update times while four 4K PNGs render concurrently. Leave part of the desktop visible on a monitor for this check; if no fixture area or capture access is available, the report explicitly marks that part unavailable and checks GPU readback instead. Cursor-only initial capture frames are excluded. Full desktop images are not stored or uploaded by this probe. Run it while the normal app is stopped. It requires interface animations, Explorer and a Direct3D 11 hardware device. A local `live-track.json` fixture is optional. Neither native acceptance command changes Afterglow preferences.

## MSI and lyric regression checks

Build the bundled installer from the source root:

~~~powershell
.\installer\build-installer.ps1 -SkipPublish -Version 2.1.1
~~~

This uses Python 3 and WiX 5.0.2 with its UI extension. The script can install the pinned WiX tool into a local .installer-tools folder. It generates installer authoring from the portable payload. End users do not need Python or WiX.

~~~powershell
.\portable\Afterglow.exe --lyrics-test C:\path\to\existing-folder\lyrics-report.txt
~~~

This temporarily creates a click-through text layer on the real Explorer desktop, checks shared-phrase opacity/texture reuse, atomic commits, interrupted movement, rapid line advances and reduced-motion cleanup, and closes the layer. The wallpaper image and saved preferences remain unchanged. The provider smoke check sends public Coldplay/Yellow metadata to LRCLIB; it never uses the user's playing song. Leave the desktop available for attachment tests.

The --ui-test, --motion-test and --setup-test commands accept a report path too. They use isolated default preferences and do not change playback, wallpaper or saved settings.
