"use strict";
importScripts("pairing.js");
chrome.runtime.onInstalled.addListener(async details => {
  const existing = await chrome.storage.local.get("token");
  const prepared = globalThis.AFTERGLOW_SETUP_TOKEN;
  if (!existing.token && /^[A-Fa-f0-9]{48}$/.test(prepared || ""))
    await chrome.storage.local.set({token: prepared});
  if (details.reason === "install") await chrome.runtime.openOptionsPage();
});
const endpoint = "http://127.0.0.1:47836";
chrome.action.onClicked.addListener(() => chrome.runtime.openOptionsPage());
chrome.runtime.onMessage.addListener((message, sender, reply) => {
  if (message.kind !== "state" || !sender.tab || !sender.url?.startsWith("https://music.youtube.com/")) return;
  (async () => {
    const {token} = await chrome.storage.local.get("token");
    if (!token || !message.state || typeof message.state.title !== "string") return {commands: []};
    const state = {...message.state, source: String(sender.tab.id)};
    const response = await fetch(endpoint + "/state", {
      method: "POST", headers: {"Content-Type": "application/json", "X-Afterglow-Token": token},
      body: JSON.stringify(state), signal: AbortSignal.timeout(3500)
    });
    if (!response.ok) throw new Error(String(response.status));
    return response.json();
  })().then(reply).catch(() => reply({commands: []}));
  return true;
});
