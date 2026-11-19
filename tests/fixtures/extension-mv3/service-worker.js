chrome.runtime.onInstalled.addListener(async () => {
  await chrome.storage.local.set({ installed: true, count: 0 });
});

chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
  if (message?.type !== "look-test-increment") return;
  chrome.storage.local.get({ count: 0 }).then(async ({ count }) => {
    const next = count + 1;
    await chrome.storage.local.set({ count: next });
    sendResponse({ count: next });
  });
  return true;
});
