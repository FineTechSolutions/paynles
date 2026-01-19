import React from "https://esm.sh/react@18";
import ProductCards from "./components/ProductCards.js";
import { validateUser, getTrackedProducts } from "./api/productsApi.js";

// Affiliate tag appended to Amazon URLs (set later via config or backend)
const AMAZON_TAG = null; // e.g. "paynles-20"

// Adds Amazon affiliate tag safely without breaking non-Amazon URLs
function appendAmazonTag(url) {
    if (!url) return url;

    const isAmazon =
        url.includes("amazon.com") ||
        url.includes("amzn.to");

    if (!isAmazon || !AMAZON_TAG) return url;
    if (url.includes("tag=")) return url;

    return url.includes("?")
        ? `${url}&tag=${AMAZON_TAG}`
        : `${url}?tag=${AMAZON_TAG}`;
}
function getEmailFromUrl() {
    const params = new URLSearchParams(window.location.search);
    return params.get("email");
}
function clearEmailFromUrl() {
    const url = new URL(window.location.href);
    url.searchParams.delete("email");
    window.history.replaceState({}, "", url.pathname);
}

export default function ProductsApp() {
    // Holds products returned from the backend (minimal fields only)
    const [products, setProducts] = React.useState([]);

    // Controls initial page loading state
    const [loading, setLoading] = React.useState(true);

    // Captures API or session errors
    const [error, setError] = React.useState(null);

    // Loads session + products once on mount
    React.useEffect(() => {
        let isMounted = true; // prevents state updates after unmount

        async function load() {
            try {
                setLoading(true);

                // --------------------------------------------------
                // SESSION INITIALIZATION (RUN ONCE ONLY)
                // --------------------------------------------------
                // --------------------------------------------------
                // SESSION VALIDATION
                // --------------------------------------------------
                const emailFromUrl = getEmailFromUrl();

                if (emailFromUrl) {
                    // First-time entry via email link
                    await validateUser(emailFromUrl);
                    clearEmailFromUrl();
                } else {
                    // Refresh / existing session
                    await validateUser();
                }



                // Fetch tracked products from API
                const data = await getTrackedProducts();
                console.log("TRACKEDGRID raw API data:", data?.[0]); // 1-liner: verify lastSeenUtc exists
                // Minimal mapping to UI-safe shape (expanded later)
                // Map backend DTO → stable UI product shape (no calculations here)
                // Map backend DTO → stable UI product shape
                const mapped = (Array.isArray(data) ? data : []).map(p => ({
                    productId: p.productId,          // TrackedProducts.ProductId
                    asin: p.asin,                    // TrackedProducts.MarketplaceProductCode
                    title: p.title,                  // Product title
                    imageUrl: p.imageUrl,            // Product image (may be null)

                    currentPrice: p.currentPrice,    // TrackedProducts.PriceSeen
                    savedPrice: p.savedPrice,        // ProductNotifications.SavedPrice

                    lastSeenUtc: p.lastSeenUtc,      // TrackedProducts.TimestampUtc (LAST CHECKED)

                    alertBelow: p.alertBelow,         // Alert threshold
                    alertMode: p.alertMode            // percent_drop | fixed
                }));



                if (isMounted) {
                    setProducts(mapped);
                }
            } catch (err) {
                console.error("Failed to load products", err);

                if (!isMounted) return;

                // Auth / access error
                if (
                    err.status === 401 ||
                    err.status === 403 ||
                    String(err.message).toLowerCase().includes("email")
                ) {
                    setError("ACCESS_REQUIRED");
                } else {
                    setError(err.message || "Failed to load products");
                }
            }
 finally {
                if (isMounted) {
                    setLoading(false);
                }
            }
        }

        load();
        return () => { isMounted = false; };
    }, []);

    // Simple loading placeholder
    if (loading) {
        return React.createElement(
            "div",
            { className: "products-wrapper" },
            "Loading products…"
        );
    }

    // Simple error placeholder
    if (error === "ACCESS_REQUIRED") {
        return React.createElement(
            "div",
            { className: "products-wrapper access-required" },

            React.createElement("h3", null, "Access Required"),

            React.createElement(
                "p",
                null,
                "Tracking products requires an active Paynles account."
            ),

            React.createElement(
                "a",
                {
                    href: "/Home/Contact",
                    className: "contact-link"
                },
                "Contact us to request access"
            )
        );
    }

    if (error) {
        return React.createElement(
            "div",
            { className: "products-wrapper error" },
            error
        );
    }


    // Render product cards with real backend data
    return React.createElement(
        "div",
        { className: "products-wrapper" },
        React.createElement(ProductCards, { products })
    );
}
