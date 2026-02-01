// /wwwroot/react/tracking/products/api/productsApi.js
//
// Minimal, stable API wrapper for Track Products grid.
// Keep this file boring + predictable so React stays clean.

const API_BASE = ""; // same-origin

async function requestJson(url, options = {}) {
    const res = await fetch(API_BASE + url, {
        method: options.method || "GET",
        headers: {
            "Accept": "application/json",
            ...(options.headers || {})
        },
        credentials: "include", // IMPORTANT: session cookie
        body: options.body || undefined
    });

    // Try to parse JSON when possible (some errors may be HTML)
    const contentType = res.headers.get("content-type") || "";
    const isJson = contentType.includes("application/json");

    let payload = null;
    if (isJson) {
        try { payload = await res.json(); } catch { payload = null; }
    } else {
        // fallback: text for debugging
        try { payload = await res.text(); } catch { payload = null; }
    }

    if (!res.ok) {
        const message =
            (payload && typeof payload === "object" && (payload.message || payload.error)) ||
            (typeof payload === "string" && payload.slice(0, 300)) ||
            `Request failed: ${res.status} ${res.statusText}`;

        const err = new Error(message);
        err.status = res.status;
        err.payload = payload;
        throw err;
    }

    return payload;
}

/**
 * Calls GET /api/trackedgrid/validate-user
 * Expectation: server sets session / returns basic identity info.
 */
export async function validateUser(email) {
    const url = email
        ? `/api/trackedgrid/validate-user?email=${encodeURIComponent(email)}`
        : `/api/trackedgrid/validate-user`;

    return requestJson(url);
}

/**
 * Calls GET /api/trackedgrid/products
 * Returns raw server payload (we map in App.js next step).
 */
export async function getTrackedProducts() {
    return requestJson("/api/trackedgrid/products");
}
// POST /api/trackedgrid/products/delete
export async function deleteTrackedProduct(productId) {
    return requestJson("/api/trackedgrid/products/delete", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ productId }) // must match backend DTO
    });
}
/**
 * Optional: If you later add endpoints like:
 * GET /api/trackedgrid/products/{productId}/notifications
 * or GET /api/trackedgrid/notifications
 * we can add wrappers here without touching UI.
 */

// export async function getProductNotifications(productId) {
//   return requestJson(`/api/trackedgrid/products/${encodeURIComponent(productId)}/notifications`);
// }
