const els = {
    emailInput: document.getElementById('email'),
    saveEmailBtn: document.getElementById('saveEmail'),
    trackBtn: document.getElementById('trackBtn'),
    flagBtn: document.getElementById('flagBtn'),
    status: document.getElementById('status'),
    emailWrap: document.getElementById('emailWrap'),
};

const AFFILIATE_TAG = "paynles-20"; // adjust if different

function setStatus(msg, type = 'info') {
    els.status.textContent = msg;
    els.status.style.color = type === 'ok' ? '#065f46' : (type === 'error' ? '#991b1b' : '#111827');
}

async function getActiveTab() {
    const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
    return tab;
}

async function sendExtract(tabId) {
    return await chrome.tabs.sendMessage(tabId, { type: 'PAYNLES_EXTRACT' });
}

function ensureAffiliateParam(urlStr) {
    try {
        const url = new URL(urlStr);
        if (url.hostname.includes('amazon.')) {
            if (!url.searchParams.has('tag')) url.searchParams.set('tag', AFFILIATE_TAG);
        }
        return url.toString();
    } catch {
        return urlStr;
    }
}

async function loadEmail() {
    const { paynlesEmail } = await chrome.storage.local.get('paynlesEmail');
    if (paynlesEmail) els.emailInput.value = paynlesEmail;
}

async function saveEmail() {
    const email = els.emailInput.value.trim();
    await chrome.storage.local.set({ paynlesEmail: email });
    setStatus('Email saved.', 'ok');
}

// Add a clear button
(function addClear() {
    const clear = document.createElement('button');
    clear.textContent = 'Clear email';
    clear.className = 'ghost';
    clear.addEventListener('click', async () => {
        await chrome.storage.local.remove('paynlesEmail');
        els.emailInput.value = '';
        setStatus('Email cleared.', 'ok');
    });
    els.emailWrap.appendChild(clear);
})();

async function postJSON(url, body) {
    const res = await fetch(url, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(body)
    });
    if (!res.ok) {
        const text = await res.text().catch(() => '');
        throw new Error(`HTTP ${res.status}: ${text}`);
    }
    return await res.json().catch(() => ({}));
}

async function handleAction(kind) {
    try {
        setStatus('Reading page…');
        const tab = await getActiveTab();
        if (!tab || !/^https?:\/\//i.test(tab.url)) throw new Error('No eligible tab URL');

        const resp = await sendExtract(tab.id);
        if (!resp?.ok) throw new Error(resp?.error || 'Extraction failed');

        const data = resp.data || {};
        if (!data || (data.site !== 'amazon' && data.site !== 'walmart')) {
            throw new Error('This page is not a supported product page.');
        }

        // Normalize link + affiliate
        const trackedUrl = ensureAffiliateParam(data.url);

        // Payload common fields (local context)
        const payload = {
            site: data.site,
            title: data.title || '',
            price: data.price,
            priceText: data.priceText || '',
            imageUrl: data.imageUrl || '',
            asin: data.asin || '',
            url: trackedUrl,
            email: els.emailInput.value.trim() || null,
            action: kind, // 'track' | 'flag'
            ts: new Date().toISOString()
        };

        // Send to your endpoints (adjust base)
        const base = "https://paynles.com";

        // 1) Log click/intent
        await postJSON(`${base}/api/log`, {
            UserId: payload.email || 'anonymous',
            Asin: payload.asin || '',
            Url: trackedUrl,
            Title: payload.title || ''
        });

        // Build server DTO expected by your ASP.NET model
        const serverPayload = {
            Image: data.imageUrl || '',               // required by API
            DealState: kind === 'track' ? 1 : 0,      // 1=Tracked, 0=Flagged (adjust if enum/string on server)
            Asin: data.asin || '',
            Title: data.title || '',
            CurrentPrice: data.price ?? null,
            DealPrice: data.price ?? null,
            DomainId: data.site === 'amazon' ? 1 : (data.site === 'walmart' ? 2 : 0),
            Url: trackedUrl,
            IsPrimeEligible: false,
            Rating: 0,
            TotalReviews: 0
        };

        // 2) Add deal / track (server expects PascalCase keys)
        await postJSON(`${base}/api/add-deal`, serverPayload);

        setStatus(kind === 'track' ? 'Tracked ✓' : 'Flagged ✓', 'ok');
    } catch (e) {
        console.error(e);
        setStatus(String(e.message || e), 'error');
    }
}

els.saveEmailBtn.addEventListener('click', saveEmail);
els.trackBtn.addEventListener('click', () => handleAction('track'));
els.flagBtn.addEventListener('click', () => handleAction('flag'));

loadEmail().catch(console.error);
