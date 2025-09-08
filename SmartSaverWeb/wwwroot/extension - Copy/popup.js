document.addEventListener("DOMContentLoaded", () => {
  const button = document.getElementById("send");

  if (!button) {
    console.error("❌ Could not find the send button");
    return;
  }

  button.addEventListener("click", async () => {
    console.log("✅ Paynles: Button clicked");

    try {
      let [tab] = await chrome.tabs.query({ active: true, currentWindow: true });

      if (!tab || !tab.url) {
        console.error("❌ No active tab URL found");
        return;
      }

      let url = new URL(tab.url);

      // Add ?tag=pnl_20 if Amazon and no tag already
      if (url.hostname.includes("amazon.") && !url.searchParams.has("tag")) {
        url.searchParams.append("tag", "pnl_20");
        console.log("🔗 Amazon URL updated:", url.href);
      }

      // Copy the modified URL to clipboard
      await navigator.clipboard.writeText(url.href);
      console.log("📋 Copied to clipboard:", url.href);

      // Send to backend (optional)
      const response = await fetch("https://paynles.com/api/track", {
        method: "POST",
        headers: {
          "Content-Type": "application/json"
        },
        body: JSON.stringify({ url: url.href })
      });

      console.log("📬 Sent to backend:", response.status);
      alert("✅ Copied to clipboard and sent to Paynles!");

    } catch (err) {
      console.error("❌ Error in popup.js:", err);
      alert("Error: " + err.message);
    }
  });
});
