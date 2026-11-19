(() => {
  const addBadge = () => {
    if (!document.documentElement || document.getElementById("look-extension-test-badge")) return;
    const badge = document.createElement("div");
    badge.id = "look-extension-test-badge";
    badge.textContent = "look test extension active";
    badge.style.cssText = "position:fixed;z-index:2147483647;right:12px;bottom:12px;padding:6px 9px;background:#292a27;color:#f4f1e9;border:1px solid #8f9b83;border-radius:7px;font:11px/1.2 sans-serif;pointer-events:none";
    document.documentElement.appendChild(badge);
  };
  if (document.documentElement) addBadge();
  else document.addEventListener("DOMContentLoaded", addBadge, { once: true });
})();
