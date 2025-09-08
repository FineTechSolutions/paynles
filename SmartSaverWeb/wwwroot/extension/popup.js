const statusEl = document.getElementById('status');
const emailInput = document.getElementById('emailInput');
const saveEmailBtn = document.getElementById('saveEmailBtn');
const trackBtn = document.getElementById('trackBtn');
const viewProductsLink = document.getElementById('viewProductsLink');
const addOrdersBtn = document.getElementById('btnAddOrders');

function setStatus(msg) { statusEl.textContent = msg || ''; }

chrome.storage.local.get(['paynlesEmail'], (res) => {
    if (res.paynlesEmail) emailInput.value = res.paynlesEmail;
});

saveEmailBtn.addEventListener('click', () => {
    const email = (emailInput.value || '').trim();
    chrome.storage.local.set({ paynlesEmail: email }, () => {
        setStatus(email ? 'Email saved.' : 'Email cleared.');
    });
});

viewProductsLink.addEventListener('click', (e) => {
    e.preventDefault();
    const email = emailInput.value.trim();
    if (!email) {
        setStatus('Please enter an email to see your products.');
        return;
    }
    const url = `https://paynles.com/MyProducts?email=${encodeURIComponent(email)}`;
    chrome.tabs.create({ url });
});

async function getActiveTab() {
    const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
    return tab;
}

function pageExtractor() {
    function asinFromUrl(u) {
        try {
            const url = new URL(u);
            const m = url.pathname.match(/\/dp\/([A-Z0-9]{10})/i) ||
                url.pathname.match(/\/gp\/product\/([A-Z0-9]{10})/i);
            return m ? m[1].toUpperCase() : null;
        } catch { return null; }
    }
    const url = location.href;
    const asin = asinFromUrl(url);
    const titleEl = document.querySelector('#productTitle') || document.querySelector('h1');
    const title = (titleEl?.textContent || document.title || '').trim();

    const priceEl = document.querySelector('.a-price .a-offscreen') || document.querySelector('#price_inside_buybox');
    let priceSeen = null;
    if (priceEl) {
        const priceText = priceEl.textContent.replace(/[^0-9.]/g, '');
        if (priceText) {
            priceSeen = parseFloat(priceText);
        }
    }

    const imageEl = document.querySelector('#landingImage');
    const imageUrl = imageEl ? imageEl.src : null;

    return { url, asin, title, priceSeen, imageUrl };
}

function withAmazonAffiliate(u) {
    try {
        const url = new URL(u);
        if (!/\.amazon\./i.test(url.hostname)) return u;
        url.searchParams.set('tag', 'paynles-20');
        return url.toString();
    } catch { return u; }
}

async function copyToClipboard(text) {
    try { await navigator.clipboard.writeText(text); }
    catch {
        const ta = document.createElement('textarea');
        ta.value = text; document.body.appendChild(ta); ta.select();
        try { document.execCommand('copy'); } catch { }
        document.body.removeChild(ta);
    }
}

trackBtn.addEventListener('click', async () => {
    setStatus('Tracking page...');
    try {
        const tab = await getActiveTab();
        const [inj] = await chrome.scripting.executeScript({ target: { tabId: tab.id }, func: pageExtractor });
        const { url, asin, title, priceSeen, imageUrl } = inj.result || {};

        if (!asin) {
            setStatus('No Amazon product (ASIN) found on this page.');
            return;
        }

        const email = (emailInput.value || '').trim();
        const affiliateUrl = withAmazonAffiliate(url);

        await copyToClipboard(affiliateUrl);

        const textLogPayload = {
            userId: email, email: email, asin: asin, title: title,
            url: affiliateUrl, priceSeen: priceSeen, source: 'ChromeExtension-Text'
        };
        const jsonLogPayload = {
            email: email, asin: asin, title: title, priceSeen: priceSeen, imageUrl: imageUrl, recordType: "Primary"
        };

        const textLogUrl = 'https://paynles.com/api/log';
        const jsonLogUrl = 'https://paynles.com/api/products/log';

        const [textLogResponse, jsonLogResponse] = await Promise.all([
            fetch(textLogUrl, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(textLogPayload)
            }),
            fetch(jsonLogUrl, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(jsonLogPayload)
            })
        ]);

        if (textLogResponse.ok && jsonLogResponse.ok) {
            setStatus('Link copied & all logs updated ✓');
        } else {
            let errorMsg = 'One or more logs failed:';
            if (!textLogResponse.ok) errorMsg += `\nText Log: ${textLogResponse.status}`;
            if (!jsonLogResponse.ok) errorMsg += `\nJSON Log: ${jsonLogResponse.status}`;
            setStatus(errorMsg);
        }

    } catch (e) {
        console.error(e);
        setStatus('Error: Failed to fetch. Is the server running and CORS enabled?');
    }
});

// 🔧 Fallback injection helper
async function ensureContentScript(tabId) {
    try {
        await chrome.tabs.sendMessage(tabId, { type: "PING" });
        return true;
    } catch {
        try {
            await chrome.scripting.executeScript({
                target: { tabId },
                files: ["content.js"]
            });
            await new Promise(r => setTimeout(r, 150));
            await chrome.tabs.sendMessage(tabId, { type: "PING" });
            return true;
        } catch (e) {
            console.error("❌ Injection failed:", e);
            return false;
        }
    }
}
document.getElementById("btnUploadHtml").addEventListener("click", async () => {
    const email = emailInput.value.trim();
    if (!email) return alert("Email is required.");

    const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });

    chrome.scripting.executeScript({
        target: { tabId: tab.id },
        func: () => document.documentElement.outerHTML
    }, async ([result]) => {
        if (!result || !result.result) {
            alert("Could not extract HTML");
            return;
        }

        const response = await fetch("https://paynles.com/api/products/upload-html", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({
                email,
                html: result.result
            })
        });

        if (response.ok) {
            alert("HTML uploaded successfully.");
        } else {
            alert("Upload failed.");
        }
    });
});

// ✅ Add Ordered Items button with fallback injection
addOrdersBtn.addEventListener("click", async () => {
    const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
    if (!tab || !tab.id) return;

    const ok = await ensureContentScript(tab.id);
    if (!ok) {
        alert("Could not connect to content script. Make sure you're on an Amazon orders page.");
        return;
    }

    chrome.tabs.sendMessage(tab.id, { type: "GET_ASINS" }, (response) => {
        if (chrome.runtime.lastError) {
            console.error("❌ Error connecting to content script:", chrome.runtime.lastError.message);
            alert("Could not connect to content script. Make sure you're on an Amazon orders page.");
            return;
        }

        console.log("✅ Response from content script:", response);

        if (!response || !response.items || !response.items.length) {
            alert("No ASINs found on this page.");
            return;
        }

        alert(`${response.items.length} ASIN(s) found!`);
        console.table(response.items);

//        const API_BASE = "https://localhost:7290";
        const API_BASE = "https://paynles.com"; // ← use your actual app URL
        const email = (emailInput.value || '').trim();

        fetch(`${API_BASE}/api/products/add-to-my-list`, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({
                mode: "me",
                recordType: "OrderHistory",
                datetime: new Date().toISOString(),
                email,
                ipAddress: "",
                items: response.items
            })
        })
            .then(res => res.ok
                ? alert("Items successfully submitted.")
                : alert("Server error."))
            .catch(err => {
                console.error("Failed to submit:", err);
                alert("Connection error.");
            });
    });
});
