// Paynles content.js - auto-extract product info

function extractProductInfo() {
  let title = "";
  let price = "";

  if (window.location.hostname.includes("amazon")) {
    title = document.getElementById("productTitle")?.innerText.trim();
    price =
      document.getElementById("priceblock_ourprice")?.innerText.trim() ||
      document.getElementById("priceblock_dealprice")?.innerText.trim() ||
      document.querySelector("span.a-price .a-offscreen")?.innerText.trim();
  } else if (window.location.hostname.includes("walmart")) {
    title = document.querySelector("h1.prod-ProductTitle")?.innerText.trim();
    price = document.querySelector("span.price-characteristic")?.innerText.trim();
  }

  if (title || price) {
    console.log("📦 Paynles Product Detected:", { title, price });
    // You can store it for popup.js to use:
    chrome.storage.local.set({ paynlesProduct: { title, price } });
  }
}

extractProductInfo();
