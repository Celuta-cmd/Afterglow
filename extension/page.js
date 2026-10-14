(() => {
  "use strict";
  const channel = "afterglow-ytm-v1";
  const text = node => node?.textContent?.trim() || "";
  function timeline(bar, video) {
    // With seamless playback, the media element can span several songs.
    // The player bar exposes the current song's relative clock instead.
    const slider = bar?.querySelector("#progress-bar");
    const now = slider?.getAttribute("aria-valuenow");
    const max = slider?.getAttribute("aria-valuemax");
    if (now != null && max != null && Number.isFinite(Number(now)) && Number.isFinite(Number(max)) && Number(max) > 0)
      return {position: Math.max(0, Number(now)), duration: Number(max), relative: true};
    const clock = text(bar?.querySelector(".time-info")).match(/(\d+(?::\d{2}){1,2})\s*\/\s*(\d+(?::\d{2}){1,2})/);
    const seconds = value => value.split(":").reduce((sum, part) => sum * 60 + Number(part), 0);
    if (clock) return {position: seconds(clock[1]), duration: seconds(clock[2]), relative: true};
    return {position: Number.isFinite(video?.currentTime) ? Math.max(0, video.currentTime) : 0,
      duration: Number.isFinite(video?.duration) ? Math.max(0, video.duration) : 0, relative: false};
  }
  function snapshot() {
    const bar = document.querySelector("ytmusic-player-bar");
    const video = document.querySelector("video");
    const clock = timeline(bar, video);
    const metadata = navigator.mediaSession?.metadata;
    const artwork = metadata?.artwork || [];
    const best = [...artwork].sort((a, b) => (parseInt(b.sizes) || 0) - (parseInt(a.sizes) || 0))[0]?.src;
    const image = bar?.querySelector("img.image, .thumbnail-image-wrapper img, img");
    const title = text(bar?.querySelector(".title")) || metadata?.title || "";
    // Prefer the artist link: the byline often also contains views and year.
    const artist = metadata?.artist || text(bar?.querySelector(".byline"))?.split(/\s*[•·]\s*/)[0] || text(bar?.querySelector(".byline a"));
    const source = best || image?.currentSrc || image?.src || "";
    // Request the CDN's larger cover without substituting art from another service.
    const cover = source.includes("googleusercontent.com") || source.includes("ggpht.com")
      ? source.replace(/=(?:w|s)\d+[^?]*/, "=w2400-h2400-l100-rj") : source;
    return {
      title, artist, album: metadata?.album || "", artwork: cover,
      playing: Boolean(video && !video.paused && !video.ended),
      position: clock.position,
      duration: clock.duration,
      playbackRate: Number.isFinite(video?.playbackRate) ? video.playbackRate : 1,
      url: location.href
    };
  }
  function command(action, value) {
    const bar = document.querySelector("ytmusic-player-bar");
    const video = document.querySelector("video");
    if (action === "toggle") {
      const button = bar?.querySelector("#play-pause-button");
      if (button) (button.querySelector("button") || button).click();
      else if (video) { if (video.paused) video.play().catch(() => {}); else video.pause(); }
    } else if (action === "previous" || action === "next") {
      const selector = action === "previous" ? ".previous-button, #previous-button" : ".next-button, #next-button";
      const button = bar?.querySelector(selector);
      if (button) (button.querySelector("button") || button).click();
    } else if (action === "seek" && video && Number.isFinite(value) && Number.isFinite(video.duration)) {
      const clock = timeline(bar, video);
      const desired = Math.max(0, Math.min(value, clock.duration));
      const offset = clock.relative ? video.currentTime - clock.position : 0;
      video.currentTime = Math.max(0, Math.min(offset + desired, video.duration));
    }
  }
  window.addEventListener("message", event => {
    if (event.source !== window || event.origin !== location.origin || event.data?.channel !== channel) return;
    if (event.data.kind === "request") {
      const state = snapshot();
      if (state.title) window.postMessage({channel, kind: "state", state}, location.origin);
    } else if (event.data.kind === "command") command(event.data.action, event.data.value);
  });
})();
