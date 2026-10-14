# Optional synced lyrics

Enable **Show synced lyrics** in Afterglow's settings, then apply the wallpaper or enable Follow the playing song. Your choice and timing adjustment are saved with your other settings. Lyrics start disabled, including for older saved configurations.

The live layout places the artwork on the left and large timed lyrics on the right. Portrait displays place lyrics below the cover. The active line stays clear; neighboring lines are dimmed. Line changes move each retained phrase and change its emphasis over 650 ms. Shared phrases keep one visual and a stable text size, avoiding the ghosting and dimming from overlapping whole-panel fades. Seeking retargets the displayed positions and opacity. Desktop changes commit atomically to prevent a frame with partially updated text. The desktop text uses a small DirectComposition layer beneath icons and above the wallpaper. Only this text area updates when lines change, so Afterglow does not encode or upload a whole wallpaper for each lyric. Long active phrases wrap downward in full, with no ellipsis. Unusually long phrases reduce their text size to fit. The preview softens the top and bottom edges; the desktop clips the small text layer at its boundaries. Closing to tray keeps it running; Exit closes the live layer.

When enabled, Afterglow queries https://lrclib.net/api/get with the playing song's title, artist, album, and duration. It sends no pairing key, Google credentials, or artwork. LRCLIB also sees the normal connection IP. Turn the feature off to cancel requests and remove lyrics. Successful results are held in memory for an hour, unavailable results for five minutes, with a 64-song limit. No lyrics cache or song history is saved to disk.

## What to expect

- Line timing, not Apple Music's word or syllable highlighting. No estimated word timing is fabricated.
- Some songs have no synced lyrics. Static-only lyrics are not scrolled with invented timing.
- Strict title/artist matching and a two-second duration tolerance reduce wrong recordings. Live, remixed, uploaded, and music-video versions may be rejected or may still have different introductions.
- The saved timing slider ranges from -5 to +5 seconds. Positive values advance the displayed lyrics. It cannot correct a recording that gradually drifts.
- Pause, seek, and playback speed use the extension's timing reports. Reload the updated extension and YouTube Music tab for speed reporting. Older extensions work at normal speed.
- Small late timing packets are prevented from nudging the playing lyric clock backward; backward corrections of 0.75 seconds or more still reset it as a seek. Tiny backward seeks while playing can therefore be absorbed by this jitter tolerance. Browser timer throttling, buffering and audio output delay can affect timing. Interpolation stops after 2.5 seconds without a new report, and the layer hides when the connection is stale.
- Windows Explorer restarts and unusual wallpaper tools can interfere with live layers. The app retries attachment; app preview remains available if desktop attachment fails.
- Live lyrics require Afterglow to be running and its wallpaper to be active. Exported PNGs contain the artwork and background layout, not scrolling lyrics. The layout leaves space for the live layer.
- Lyrics work in both Ambient and Gallery. Lyrics mode takes precedence over the selected style's cover placement, including colors-only styles. Turning it off restores that style on the next apply. If Afterglow currently owns the wallpaper, toggling lyrics updates it immediately.

## Research and design

Apple documents timed lyrics with a full-text fallback when timings are unavailable: https://support.apple.com/en-gb/108960

Apple's provider guidance stresses that lyric timing must match the exact audio version: https://itunespartner.apple.com/music/support/5218-submit-lyrics

BitChord provides Apple-like animation and word/syllable timing using multiple providers: https://github.com/kushagrasinghx/BitChord

LRCLIB is an open synchronized-lyrics service: https://lrclib.net/docs and https://github.com/tranxuanthang/lrclib

This implementation follows the visual direction in the supplied reference and these documented behaviors. No Apple or BitChord implementation code or lyric files are bundled.
