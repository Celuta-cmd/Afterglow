# Verification - Afterglow 1.4.0

GPU transition release, October 7, 2026.

- The self-contained Windows x64 executable built with zero C# or C++ warnings/errors and includes .NET / WPF / ASP.NET Core 8.0.30. The statically linked native graphics helper is embedded in the executable and uses Windows graphics APIs.
- **40 integration/rendering checks passed**, including native 4K artwork detail, real source dimensions, four styles, portrait/ultrawide layouts, authenticated bridge/control routing, saved configurations and backup recovery. The optional YouTube Music fixture downloaded successfully.
- **10 extension checks passed**. The extension remains version 1.1.0 and requires no reinstall for this update.
- **15 native transition checks passed** on the final executable. GPU windows attached behind icons on all three active monitors. Tests covered intermediate texture colors, interruption, exact endpoints, six rapid skips, PNG commit, idle retention, cleanup and restoration.
- Unlike the earlier paint-only checks, the final test also sampled exposed fixture pixels from the composed desktop using Desktop Duplication. It captured **484 desktop updates**, with **zero color errors or backward color jumps**, during a two-second fade while four 4K PNGs rendered concurrently. Capture-update intervals were **4.4 ms mean, 5.3 ms at the 95th percentile, and 41.8 ms maximum**. These are desktop capture update times, not a guarantee of physical panel refresh rates or performance on other hardware. The exposed fixture was on one monitor; attachment was checked on all three.
- The same stress test recorded 68 UI heartbeats with a maximum gap of 46.5 ms. Desktop animation continued while the app's composition worker was occupied by the capture probe.
- Native acceptance restored the original monitor images/slideshow settings afterward and did not write user preferences. Source and portable packages exclude private settings, pairing keys, downloaded artwork, local test fixtures, compiler output and desktop capture buffers.

The desktop now uploads two original-resolution images and asks DirectComposition to animate opacity. It has no CPU loop that rebuilds a full wallpaper every frame. All monitor visuals share textures and one animation start. Interrupted fades are flattened into an opaque GPU snapshot before retargeting. The completed layer stays idle between songs. PNG encoding overlaps the fade and is awaited before committing the final wallpaper.

The studio and desktop use a gentle two-second smooth-step curve. Source artwork quality, lossless PNGs, theme and saved configuration behavior are preserved.

Desktop transitions require a running Explorer desktop, enabled interface animations and a Direct3D 11 hardware device. Unavailable composition or high contrast falls back to direct PNG application. Explorer hosting is undocumented and can change with Windows updates. The first application over an unrelated wallpaper is direct.

Installed Desktop acceptance: the updated executable launched as version 1.4.0. The Test desktop fade action hid the studio, completed the GPU fade to a sample and back, and returned its success status. The original settings file remained identical to the pre-upgrade copy; no error log was created. The music feed showed Waiting for connection during this UI check; no playback command was sent.

