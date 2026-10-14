# Afterglow 2.1.1 verification

## Windows lyrics regression checks

- Release build completed without warnings or errors.
- 64 built-in integration/rendering checks passed. A separate optional local artwork fixture brought the local run to 65; that private fixture is excluded from the source and release.
- 18 browser-extension/setup checks passed.
- The real desktop lyric layer attached above the wallpaper, below icons, with click-through and no-activation styles on all connected displays.
- GPU readback contained visible glyphs over transparent pixels.
- Shared phrases kept their raster textures and starting emphasis. Each animated scene update submitted one compositor commit.
- Movement brightened the incoming phrase continuously. An interrupted move retargeted from its displayed position and opacity without uploading shared textures again.
- Fifteen rapid advances, 80 ms apart, kept departing phrase lifetimes bounded and returned to three retained glyphs.
- Reduced-motion updates settled immediately, with outgoing glyphs removed.
- The preview reused shared visuals at a stable per-phrase font size; long phrases wrapped fully.
- Small late clock reports did not move a playing lyric backward across its timestamp. Backward/forward seeks, pauses, rate changes, new songs and stale interpolation were checked.
- Closing the test layer left the Windows wallpaper path unchanged.

These checks exercise the Windows composition path on the connected PC. They do not establish frame-perfect behavior on every GPU, Explorer version, remote-desktop session or monitor configuration. LRCLIB timing and recording coverage remain separate limitations.

## Packaging and interface

The final packaged executable is checked with the lyric regression, navigation, window-motion and setup diagnostics before release. The MSI is a per-user x64 package with a stable upgrade identity, bundled runtimes, extension and docs. A 2.1.0 to 2.1.1 upgrade is checked on the existing installation, including pairing/configuration preservation.

Only Windows source and redistributable files are published. Per-PC pairing, settings, cached song metadata, build logs, private test fixtures and authentication screenshots are excluded. The shipped pairing.js has an empty key. The repository and release remain private. The app and MSI are unsigned.
