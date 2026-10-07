(() => {
  "use strict";
  const channel = "afterglow-ytm-v1";
  let busy = false;
  let alive = true;
  window.addEventListener("message", async event => {
    if (!alive || busy || event.source !== window || event.origin !== location.origin || event.data?.channel !== channel || event.data.kind !== "state") return;
    busy = true;
    try {
      const result = await chrome.runtime.sendMessage({kind: "state", state: event.data.state});
      for (const cmd of result?.commands || []) window.postMessage({channel, kind: "command", action: cmd.action, value: cmd.value}, location.origin);
    } catch {
      // An extension reload invalidates old content scripts; a tab reload reconnects.
      if (!chrome.runtime?.id) alive = false;
    } finally { busy = false; }
  });
  setInterval(() => { if (alive && !busy) window.postMessage({channel, kind: "request"}, location.origin); }, 1000);
})();
