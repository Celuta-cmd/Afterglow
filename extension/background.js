"use strict";
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
