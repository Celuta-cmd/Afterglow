# Afterglow

Your current YouTube Music album cover, turned into a Windows wallpaper.

I wanted my desktop to match whatever I was listening to, with the soft colors and big album art you get in a fullscreen music player. Afterglow follows the song playing on the YouTube Music website and builds a wallpaper around its cover.

![Afterglow synced lyrics](docs/lyrics.png)

## What it does

- Album art over a soft background made from the cover's colors.
- Four layouts, plus controls for blur, dimming, cover size and track text.
- GPU crossfades when the song changes, including on multiple monitors.
- Display-sized, 1080p or 4K wallpapers and lossless PNG exports.
- Optional play/pause, previous, next and seek controls, plus a floating mini-player.
- Saved settings, light and dark themes, tray support and an option to restore your previous wallpaper.
- Optional line-synced lyrics next to the cover, with wrapped phrases, smooth movement and a saved timing adjustment.
- A sidebar-based studio and guided browser setup after installing the MSI.

It's free and open source under the MIT license.

## Try it

You'll need Windows 10 or 11 on an x64 PC, and Chrome or Edge.

1. Download **Afterglow-2.1.1-x64.msi** from [Releases](https://github.com/Celuta-cmd/Afterglow/releases/latest), then run the installer.
2. Leave **Open Afterglow and set up your browser** selected. The first-run guide lets you choose Chrome or Edge and prepares a private, pre-paired extension folder.
3. In the browser's Extensions page, enable **Developer mode**, choose **Load unpacked**, and select the folder copied by the guide.
4. Refresh your YouTube Music tab, play a song, and finish setup. Choose a style in Wallpaper Studio and enable **Follow the playing song** for automatic updates.
5. For lyrics, enable **Show synced lyrics** in Lyrics. They appear beside the cover on the desktop as well as in the app.

The runtime is included. The installer and app are currently unsigned, so Windows may show a publisher warning. The browser still requires the unpacked-extension approval clicks; the app does not bypass them. Existing users can reopen the setup guide from Connection.

Keep Afterglow and the music tab running. Closing the studio leaves it in the tray. An MSI upgrade keeps your settings and pairing; see [INSTALL.md](INSTALL.md) for maintenance and updating an already-loaded extension.

## A note on how it was built

This was mostly vibe-coded with Codex, then iterated on using actual Windows desktop tests. The first fades were pretty choppy, so the current version uses Windows GPU composition instead of rebuilding a whole wallpaper every frame. Lyrics now keep one visual per phrase rather than fading between overlapping copies of the whole text panel.

It's a hobby project, and there are probably rough edges I haven't hit yet. If you try it, I'd love to know whether setup makes sense and how the fades look on your monitor setup. Bug reports and small fixes are welcome.

## How the connection works

The extension reads the current song and artwork from your YouTube Music tab and talks to Afterglow through an authenticated server on `127.0.0.1:47836`. Playback controls go back to the paired tab. It uses your existing browser session and doesn't handle Google passwords or cookies.

Artwork comes from the YouTube/Google image hosts. The app asks for a larger cover when one is available, but a small original still has limited detail. Wallpaper text and layout render at the selected resolution.

Optional lyrics come from LRCLIB. When enabled, a lookup sends the song's title, artist, album and duration to that service, which also sees the connection IP. Lyrics are cached only in memory. Turning lyrics off stops those lookups; see [LYRICS.md](LYRICS.md) for timing and privacy details.

Preferences stay in local app data. Wallpaper images and restoration backups are in **Pictures > Afterglow**. There are no analytics, subscriptions or cloud accounts for Afterglow.

## A few limits

This works with the YouTube Music website, not a phone app or a separate desktop player. Music videos can supply video thumbnails instead of album covers. Desktop fades need a Direct3D 11 hardware device and Windows interface animations; if that isn't available, the finished wallpaper applies directly. Explorer's desktop behavior can change after Windows updates.

Lyrics are line-synced, without word-by-word timing. Some recordings have no matching lyrics, and live or alternate versions can have different timing. The lyric view works in Ambient and Gallery and wraps long phrases instead of adding an ellipsis. Exported PNGs contain the cover/background layout, without live lyrics.

Afterglow applies one wallpaper using Fill across your displays. It doesn't yet offer separate album layouts for each monitor. Artwork rights belong to their respective owners. This project isn't affiliated with Google, YouTube or Apple.

## Build or contribute

The Windows source is in this repository and the release's source archive. See [BUILD.md](BUILD.md) for the .NET 8, C++ and MSI build requirements. The app source is in `src/Afterglow`, the graphics helper is in `src/Afterglow/Native`, and the browser extension is in `extension`.

[Verification notes](VERIFICATION.md) describe what was checked and the limits of those checks. [Research notes](RESEARCH.md) cover the apps and Windows APIs that informed the design.
