const result = document.getElementById("result");
document.getElementById("increment").addEventListener("click", () => {
  chrome.runtime.sendMessage({ type: "look-test-increment" }, response => {
    if (chrome.runtime.lastError) {
      result.textContent = chrome.runtime.lastError.message;
      return;
    }
    result.textContent = response ? `Worker replies: ${response.count}` : "No response";
  });
});
