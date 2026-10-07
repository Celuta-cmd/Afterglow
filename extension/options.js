"use strict";
const input = document.getElementById("token");
const status = document.getElementById("status");
const button = document.getElementById("save");
if (!globalThis.chrome?.storage?.local) {
  input.disabled = true;
  button.disabled = true;
  status.textContent = "Install the extension first: open edge://extensions or chrome://extensions, enable Developer mode, then Load unpacked and choose Afterglow's extension folder. Open this page from the installed extension's toolbar icon.";
} else {
chrome.storage.local.get("token").then(value => { input.value = value.token || ""; });
button.addEventListener("click", async () => {
  const token = input.value.trim();
  if (!/^[A-Fa-f0-9]{48}$/.test(token)) {status.textContent = "Paste the full 48-character pairing key from the Windows app.";return;}
  button.disabled = true;
  try {
    await chrome.storage.local.set({token});
    const response = await fetch("http://127.0.0.1:47836/health", {headers: {"X-Afterglow-Token": token}, signal: AbortSignal.timeout(4000)});
    if (response.status === 401) throw new Error("The key does not match. Copy the key from this PC's Afterglow app.");
    if (!response.ok) throw new Error("Connection returned " + response.status + ". Restart Afterglow and try again.");
    status.textContent = "Connected ✓ Reload YouTube Music, then play a song.";
  } catch (error) {
    status.textContent = error.message.includes("key") ? error.message : "Cannot reach Afterglow. Open the Windows app, then try again. " + error.message;
  } finally {button.disabled = false;}
});
}
