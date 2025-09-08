// ✅ Only run on Amazon order pages
if (window.location.href.includes("order-history") || window.location.href.includes("your-orders")) {

    console.log("🔥 content.js LOADED");
    console.log("📨 content.js ready to receive messages");

    // ✅ Normalize price
    function parsePrice(text) {
        if (!text) return null;
        const clean = text.replace(/[^0-9.,]/g, '').replace(',', '.');
        const val = parseFloat(clean);
        return isNaN(val) ? null : val;
    }

    // ✅ Extract orders
    function extractOrdersFromPage() {
        const seen = new Set();
        const results = [];

        const asinLinks = document.querySelectorAll("a[href*='/dp/']");
        asinLinks.forEach(link => {
            const href = link.href;
            const match = href.match(/\/dp\/([A-Z0-9]{10})/);
            if (!match) return;

            const asin = match[1];
            if (seen.has(asin)) return;
            seen.add(asin);

            const container = link.closest("div");

            const titleEl =
                container?.querySelector("a[href*='/dp/'] span") ||
                container?.querySelector("span.a-text-bold") ||
                container?.querySelector("h4");

            const title = titleEl?.textContent?.trim().slice(0, 100) || "";

            const priceEl =
                container?.querySelector(".a-price .a-offscreen") ||
                container?.querySelector(".a-color-price") ||
                container?.querySelector("span[class*='price']") ||
                container?.querySelector("span[aria-label*='$']");

            const price = parsePrice(priceEl?.textContent || "");

            const imgEl = container?.querySelector("img");
            const image = imgEl?.src || null;

            results.push({ asin, title, price, image });
        });

        return results;
    }

    // ✅ Message listener
    chrome.runtime.onMessage.addListener((msg, sender, sendResponse) => {
        if (msg?.type !== "GET_ASINS") return;

        console.log("📩 GET_ASINS received");

        const seen = new Set();
        const items = [];

        const section = document.evaluate(
            "/html/body/div[1]/section/div[1]",
            document,
            null,
            XPathResult.FIRST_ORDERED_NODE_TYPE,
            null
        ).singleNodeValue;

        if (!section) {
            console.warn("❌ Could not locate section via XPath");
            sendResponse({ items: [] });
            return true;
        }

        const links = section.querySelectorAll("a[href*='/dp/']");

        links.forEach(link => {
            const match = link.href.match(/\/dp\/([A-Z0-9]{10})/i);
            if (!match) return;

            const asin = match[1];
            if (seen.has(asin)) return;
            seen.add(asin);

            // 🔍 Use exact title selector
            const titleAnchor = document.querySelector(`.yohtmlc-product-title a[href*="${asin}"]`);
            const title = titleAnchor?.textContent?.trim() || "(no title)";

            const imgEl = link.querySelector("img");
            const image = imgEl?.src || null;

            const price = null; // ❗ Price not found in uploaded structure

            items.push({
                asin,
                title,
                price,
                image
            });
        });

        console.log("📦 Extracted:", items);
        sendResponse({ items });
        return true;
    });








}
