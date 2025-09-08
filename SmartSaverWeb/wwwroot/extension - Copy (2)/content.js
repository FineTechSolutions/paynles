// Paynles content script: extract title, price, image, and ASIN (Amazon)
(function () {
    function textOrNull(el) {
        return el ? el.textContent.trim() : null;
    }

    function toNumber(priceText) {
        if (!priceText) return null;
        // Remove currency symbols and non-numeric (keep comma and dot)
        let t = priceText.replace(/[^\d.,\-]/g, '').trim();
        // If it has both comma and dot, pick the last one as decimal
        // Normalize thousand separators
        if (t.indexOf(',') > -1 && t.indexOf('.') > -1) {
            if (t.lastIndexOf('.') > t.lastIndexOf(',')) {
                t = t.replace(/,/g, ''); // commas as thousands
            } else {
                t = t.replace(/\./g, '').replace(',', '.'); // dots as thousands
            }
        } else if (t.indexOf(',') > -1 && t.indexOf('.') === -1) {
            // Only comma present -> treat as decimal
            t = t.replace(',', '.');
        }
        const n = parseFloat(t);
        return isNaN(n) ? null : n;
    }

    function extractAmazon() {
        const title = textOrNull(document.querySelector('#productTitle, #titleSection #title'));
        // Price candidates
        const priceCandidates = [
            '.a-price .a-offscreen',
            '#corePrice_feature_div .a-price .a-offscreen',
            '#sns-base-price .a-offscreen',
            '#priceblock_ourprice',
            '#priceblock_dealprice',
            '#priceblock_saleprice'
        ];
        let priceText = null;
        for (const sel of priceCandidates) {
            const el = document.querySelector(sel);
            if (el && el.textContent.trim()) { priceText = el.textContent.trim(); break; }
        }
        const price = toNumber(priceText);

        // Primary image
        const img = document.querySelector('#imgTagWrapperId img, #landingImage, img#ebooksImgBlkFront, #main-image-container img');
        const imageUrl = img ? (img.currentSrc || img.src) : null;
        // Fallback: og:image meta if primary selector fails
        let og = document.querySelector('meta[property="og:image"], meta[name="og:image"]');
        const ogImg = og ? og.getAttribute('content') : null;
        const finalImage = imageUrl || ogImg || null;

        // ASIN detection from URL or page
        const asinUrlMatch = (location.href.match(/\/dp\/([A-Z0-9]{10})/) || [])[1];
        let asin = asinUrlMatch || null;
        if (!asin) {
            const asinNode = document.querySelector('#ASIN, input[name=ASIN]');
            asin = asinNode ? asinNode.value : null;
        }

        return { site: 'amazon', title, price, priceText, imageUrl: finalImage, asin, url: location.href };
    }

    function extractWalmart() {
        const title = textOrNull(document.querySelector('h1[data-automation-id="product-title"], h1[itemprop="name"], h1'));
        // Walmart price variants
        const priceCandidates = [
            'span[data-automation-id="product-price"]',
            'div[data-automation-id="price-section"] [itemprop="price"]',
            'span[itemprop="price"]',
            'div[data-automation-id="price"] .visuallyhidden',
            'span[class*="price-characteristic"]'
        ];
        let priceText = null;
        for (const sel of priceCandidates) {
            const el = document.querySelector(sel);
            if (el && el.textContent.trim()) { priceText = el.textContent.trim(); break; }
            if (el && el.getAttribute('content')) { priceText = el.getAttribute('content'); break; }
        }
        const price = toNumber(priceText);

        // Image
        const img = document.querySelector('img[alt][src*="walmartimages"], img[loading][src]');
        const imageUrl = img ? (img.currentSrc || img.src) : null;
        // Fallback: og:image meta if primary selector fails
        let og = document.querySelector('meta[property="og:image"], meta[name="og:image"]');
        const ogImg = og ? og.getAttribute('content') : null;
        const finalImage = imageUrl || ogImg || null;

        return { site: 'walmart', title, price, priceText, imageUrl: finalImage, url: location.href };
    }

    function extract() {
        const host = location.hostname;
        if (host.includes('amazon.')) return extractAmazon();
        if (host.includes('walmart.com')) return extractWalmart();
        return { site: 'unknown', url: location.href };
    }

    chrome.runtime.onMessage.addListener((msg, sender, sendResponse) => {
        if (msg && msg.type === 'PAYNLES_EXTRACT') {
            try {
                const data = extract();
                sendResponse({ ok: true, data });
            } catch (e) {
                sendResponse({ ok: false, error: e?.message || String(e) });
            }
            return true; // async
        }
    });
})();
