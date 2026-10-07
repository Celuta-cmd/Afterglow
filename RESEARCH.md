# Research and design choices

Research completed before implementation, October 1, 2026.

| Reference | What informed this app |
| --- | --- |
| [MusWall: Music Album Wallpaper](https://play.google.com/store/apps/details?id=com.automan.albumwallpaper&hl=en_US) | Automatic artwork sync, adjustable blur and cover layout, image export, and an optional music widget. Its history, Android lock-screen integration, and spinning-CD mode were not carried into this Windows version. |
| [Lively Wallpaper](https://github.com/lively-community/lively) | Native Windows wallpaper lifecycle, tray operation, explicit user settings, and performance considerations. Afterglow uses the Windows static wallpaper API and does not require Lively. |
| [YouTube Music Desktop](https://github.com/ytmdesktop/ytmdesktop) | Desktop controls around the existing YouTube Music website. Afterglow connects to the user's regular browser rather than duplicating their Google sign-in in a webview. |
| [Apple Music Album Motion Guidelines](https://help.apple.com/itc/albummotionguide/en.lproj/static.html) | Artwork-driven atmosphere, coherent composition, and legibility. The design is inspired by those principles; it does not retrieve Apple's motion assets or reproduce its artwork delivery format. |

The 1.3 studio redesign uses the user's Mac Music reference images: a quiet sidebar, an album-focused header, light glass-like panels, restrained red actions, thumbnail choices and a compact bottom player. Dark appearance supplies the warmer visual treatment in the other reference. The Windows system typeface keeps text legible without bundling Apple's fonts.

Additional primary references reviewed for the redesign: [Apple's macOS design guidance](https://developer.apple.com/design/human-interface-guidelines/designing-for-macos/) encourages personalization and configurable windows; [Principles of great design](https://developer.apple.com/videos/play/wwdc2026/250/) discusses hierarchy through spacing, ordering and contrast; [the new design system](https://developer.apple.com/videos/play/wwdc2025/356/) emphasizes layout and grouping; [Music MiniPlayer](https://support.apple.com/guide/music/use-music-miniplayer-mus71d7dcfce/mac) provides a compact artwork-and-controls precedent. These ideas are adapted for a Windows wallpaper tool with real, relevant navigation and preferences.

Desktop fade correction: [Microsoft's layered-window documentation](https://learn.microsoft.com/en-us/windows/win32/winmsg/window-features#layered-windows) informs the native paint layer. [Lively's Explorer hosting implementation](https://github.com/rocksdanister/lively/blob/core-separation/src/Lively/Lively/Core/WinDesktopCore.cs) was inspected to understand the raised-desktop hierarchy and the requirement to keep Explorer's opaque WorkerW below the animated child. Afterglow's native GDI paint implementation was written independently; no Lively code is bundled. Tests now read back native painted pixels and verify ordering, rather than relying solely on WPF opacity clocks.

Technical references: [Chrome content scripts](https://developer.chrome.com/docs/extensions/reference/manifest/content-scripts), [extension network requests](https://developer.chrome.com/docs/extensions/develop/concepts/network-requests), and [WPF bitmap rendering](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/graphics-multimedia/how-to-create-a-bitmap-from-a-visual).

This is an original implementation. No code or artwork was copied from the reference apps. The bundled demonstration cover is drawn by the app. The live research/test cover was sourced from YouTube Music and is not bundled with the program.

## GPU transitions (1.4)

The desktop uses Windows DirectComposition and Direct3D 11 to animate shared full-resolution images. Primary references: [composition targets](https://learn.microsoft.com/en-us/windows/win32/api/dcomp/nf-dcomp-idcompositiondevice-createtargetforhwnd), [opacity animations](https://learn.microsoft.com/en-us/windows/win32/directcomp/how-to--animate-a-visual), and [composition architecture](https://learn.microsoft.com/en-us/windows/win32/directcomp/architecture-and-components). Desktop Duplication checks displayed fixture colors and update timestamps; initial cursor-only frames are excluded. Capture intervals are distinguished from physical panel refresh rates.

