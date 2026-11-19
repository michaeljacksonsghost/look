(() => {
  const addBadge = () => {
    if (!document.documentElement || document.getElementById("look-extension-test-badge")) return;
    const badge = document.createElement("div");
    badge.id = "look-extension-test-badge";
    badge.style.cssText = "position:fixed;z-index:2147483647;right:12px;bottom:12px;display:flex;gap:8px;align-items:center;padding:7px 9px;background:#292a27;color:#f4f1e9;border:1px solid #8f9b83;border-radius:7px;font:11px/1.2 sans-serif";
    const label = document.createElement("span");
    label.textContent = "look extension test";
    const button = document.createElement("button");
    button.textContent = "Ping worker";
    button.style.cssText = "border:0;border-radius:5px;padding:5px 7px;background:#8f9b83;color:#20211e;cursor:pointer";
    const result = document.createElement("span");
    result.textContent = "Ready";
    button.addEventListener("click", () => {
      chrome.runtime.sendMessage({ type: "look-test-increment" }, response => {
        if (chrome.runtime.lastError) {
          result.textContent = "Error";
          return;
        }
        result.textContent = response ? "Worker: " + response.count : "No response";
      });
    });
    badge.append(label, button, result);
    document.documentElement.appendChild(badge);
  };
  if (document.documentElement) addBadge();
  else document.addEventListener("DOMContentLoaded", addBadge, { once: true });
})();
