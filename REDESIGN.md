# Afterglow 2.0 design

The existing WPF application and native DirectComposition wallpaper engine are retained. This redesign follows the supplied macOS philosophy brief while using Windows capabilities and legally reusable, original vector icons.

## Shell and content

Five destinations replace the always-visible inspector: Wallpaper Studio, Appearance, Lyrics, Playback, and Connection. Playback persists across views. The sidebar can collapse and remembers an explicit choice; narrow windows adapt without overwriting that choice. Each view retains its scroll container. Controls remain connected to the existing renderer, lyrics provider, player commands, and saved configuration.

The studio gives the wallpaper preview and style choices priority. Initial connection has a clear action; a sample is labeled as a style preview. Lyrics have their own album-and-text stage while the live desktop layer remains available. Appearance settings use grouped forms with immediate numeric feedback and debounced rendering.

## Design system

Semantic light/dark/system colors, title/body/caption styles, compact button tiers, consistent radii, native system typography, and original 24-point vector glyphs replace scattered visual treatments. No Apple fonts, SF Symbols, or Apple artwork are bundled. Windows 11 uses native Mica in the shell where supported, with an opaque content area and a neutral fallback on older Windows. High-contrast colors and selected text use Windows system brushes.

Page motion is short opacity/translation, sidebar width changes are restrained, and hover feedback uses surface emphasis rather than scaling every control. Minimize, maximize, restore, cover fades, and lyric scrolling preserve their existing behavior. Windows reduced motion is honored, including when a previously-created desktop compositor is reused.

## Fixes and accessibility

Paused sources allow a 75-second freshness window for browser timer throttling; actively playing sources retain the eight-second limit. Search has a clear action only when populated. Keyboard navigation includes Ctrl+1 through Ctrl+5, Ctrl+B, Ctrl+F, Ctrl+comma, and Ctrl+Shift+M. Playback seeking supports keyboard arrows and page/home/end keys. Icon buttons have names and tooltips, focus remains visible, dialogs dismiss with Escape, and the pairing key is hidden until deliberately revealed or copied.

## References

- macOS design: https://developer.apple.com/design/human-interface-guidelines/designing-for-macos/
- Sidebars: https://developer.apple.com/design/human-interface-guidelines/sidebars
- Toolbars: https://developer.apple.com/design/human-interface-guidelines/toolbars
- Materials: https://developer.apple.com/design/human-interface-guidelines/materials
- AppKit design presentation: https://developer.apple.com/videos/play/wwdc2025/310/

These references inform the interface; Afterglow remains an independent Windows application.
