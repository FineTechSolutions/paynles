import React from "https://esm.sh/react@18";
import mockProducts from "../api/MockData.js";
import { MessageCircle } from "https://esm.sh/lucide-react@0.395.0";
import { Eye, Pencil, Share2, LineChart, Tag, Trash2 }

    from "https://esm.sh/lucide-react@0.395.0";
// BEGIN INSERT: Dialog import
import { DeleteDialog } from "./ProductDialogs.js";
// END INSERT

// BEGIN INSERT: ShareDialog import
import { ShareDialog } from "./ProductDialogs.js";
// END INSERT
// BEGIN INSERT: TagsDialog import
import { TagsDialog } from "./ProductDialogs.js";
// END INSERT
// BEGIN INSERT: CommentsDialog import
import { CommentsDialog } from "./ProductDialogs.js";
// END INSERT
// BEGIN INSERT: PriceHistoryDialog import
import { PriceHistoryDialog } from "./ProductDialogs.js";
// END INSERT
// BEGIN INSERT: AlertDialog import
import { AlertDialog } from "./ProductDialogs.js";
// END INSERT
// BEGIN INSERT: DetailsDialog import
import { DetailsDialog } from "./ProductDialogs.js";
// END INSERT

// BEGIN INSERT: Local state handling for delete dialog
// We store which product is selected and whether dialog is open
let deleteDialogProduct = null;
// BEGIN INSERT: Share dialog state
let shareDialogProduct = null;
// Local mutable product list for instant UI updates (no reload)
let currentProducts = [];
function openShareDialog(product) {
    shareDialogProduct = product;
    rerender();
}

function closeShareDialog() {
    shareDialogProduct = null;
    rerender();
}
// END INSERT
// BEGIN INSERT: Tags dialog state
let tagsDialogProduct = null;

function openTagsDialog(product) {
    tagsDialogProduct = product;
    rerender();
}

function closeTagsDialog() {
    tagsDialogProduct = null;
    rerender();
}

function updateProductTags(asin, updatedTags) {
    currentProducts = currentProducts.map(p =>
        p.asin === asin ? { ...p, tags: updatedTags } : p
    );
}
// END INSERT

// BEGIN INSERT: Comments dialog state
let commentsDialogProduct = null;

function openCommentsDialog(product) {
    commentsDialogProduct = product;
    rerender();
}

function closeCommentsDialog() {
    commentsDialogProduct = null;
    rerender();
}
// BEGIN INSERT: Price history dialog state
let historyDialogProduct = null;

function openHistoryDialog(product) {
    historyDialogProduct = product;
    rerender();
}

function closeHistoryDialog() {
    historyDialogProduct = null;
    rerender();
}
// END INSERT

function addProductComment(asin, commentText) {
    currentProducts = currentProducts.map(p =>
        p.asin === asin
            ? { ...p, comments: [...(p.comments || []), commentText] }
            : p
    );
}
// END INSERT

function openDeleteDialog(product) {
    // Direct delete without confirmation dialog
    handleDelete(product.productId); // call delete immediately
}

function closeDeleteDialog() {
    deleteDialogProduct = null;
    rerender();
}

function removeProductFromList(asin, products) {
    return products.filter(p => p.asin !== asin);
}
// END INSERT

// END INSERT
// BEGIN INSERT: Alert dialog state
let alertDialogProduct = null;

function openAlertDialog(product) {
    alertDialogProduct = product;
    rerender();
}

function closeAlertDialog() {
    alertDialogProduct = null;
    rerender();
}
// Normalizes backend product → UI product shape
function normalizeProduct(p) {
    return {
        // Identity
        asin: p.asin || p.Asin || p.ASIN || null,
        productId: p.productId || p.ProductId || null,

        // Display
        title: p.title || p.Title || "(Untitled)",
        imageUrl: p.imageUrl || p.ImageUrl || null,

        // Pricing (may be null initially)
        price: p.currentPrice ?? p.price ?? p.CurrentPrice ?? null, // Prefer currentPrice from API
        lastSeenUtc: p.lastSeenUtc || p.LastSeenUtc || null, // 1-liner: timestamp of last price check
        savedPrice: p.savedPrice ?? p.SavedPrice ?? null,

        // Raw link (already Amazon-tagged upstream)
        productUrl: p.productUrl || p.Url || p.ProductUrl || null,

        // Alerts / misc (optional, safe defaults)
        alertMode: p.alertMode ?? null,
        alertBelow: p.alertBelow ?? null,
        percentChange: p.percentChange ?? null,

        // Keep original for debugging
        _raw: p
    };
}
function saveAlertSettings(asin, mode, value) {
    currentProducts = currentProducts.map(p =>
        p.asin === asin
            ? { ...p, alertMode: mode, alertBelow: value }
            : p
    );
}
// END INSERT
// BEGIN INSERT: Details dialog state
let detailsDialogProduct = null;

function openDetailsDialog(product) {
    detailsDialogProduct = product;
    rerender();
}

function closeDetailsDialog() {
    detailsDialogProduct = null;
    rerender();
}
// END INSERT
async function handleDelete(productId) {
    try {
        // Call backend delete endpoint
        const res = await fetch("/api/trackedgrid/products/delete", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            credentials: "include",
            body: JSON.stringify({ productId }) // API contract
        });

        if (!res.ok) throw new Error("Delete failed");

        // Remove locally (no reload)
        currentProducts = currentProducts.filter(p => p.productId !== productId);
        rerender();

    } catch (err) {
        console.error("Delete failed:", err); // debug only
    }
}

export default function ProductCards({ products })   {
    //// BEGIN INSERT: local product list state
    //if (!window.productCards_state) {
    //    window.productCards_state = mockProducts.slice(); // clone
    //}
    //let currentProducts = window.productCards_state;
    //// END INSERT

    // ====================================
    // BEGIN REPLACE RETURN BLOCK
    // ====================================
    // TEMP: use real products from App.js (mock restored later)
    // Initialize local list once from props
    //list once from props
    if (currentProducts.length === 0 && Array.isArray(products)) {
        currentProducts = products.map(normalizeProduct);
    }


    return React.createElement(
        "div",
        null,

        // GRID
        React.createElement(
            "div",
            { className: "product-grid" },
            currentProducts.map(product => {
                // ------------------------------
                // SAFE PRICE NORMALIZATION
                // ------------------------------
                const saved = Number(product.savedPrice);
                const price = Number(product.price);

                // Only calculate change if numbers are valid
                const change =
                    typeof product.percentChange === "number"
                        ? product.percentChange
                        : (Number.isFinite(price) && Number.isFinite(saved) && saved !== 0)
                            ? ((price - saved) / saved) * 100
                            : null;

                // ------------------------------
                // IMAGE FALLBACK (SAFE)
                // ------------------------------
                // Build canonical Amazon affiliate link from ASIN (source of truth)
                const amazonUrl = product.asin
                    ? `https://www.amazon.com/dp/${product.asin}?tag=paynles-20`
                    : null;
                const isPlaceholderImage =
                    !product.imageUrl ||
                    product.imageUrl.startsWith("data:image/svg");

                const imageSrc = isPlaceholderImage
                    ? "/images/no-image.png"   // make sure this file exists
                    : product.imageUrl;

                return React.createElement(
                    "div",
                    { key: product.asin, className: "product-card" },

                    React.createElement("div", { className: "status-bar" }),

                    // Product image (clickable only when ASIN exists)
                    React.createElement(
                        "div",
                        { className: "image-wrap" },

                        amazonUrl
                            ? React.createElement(
                                "a",
                                {
                                    href: amazonUrl,                 // Open Amazon with affiliate tag
                                    target: "_blank",
                                    rel: "noopener noreferrer"
                                },
                                React.createElement("img", {
                                    src: imageSrc,
                                    alt: product.title,
                                    loading: "lazy",
                                    className: "product-image"
                                })
                            )
                            : React.createElement("img", {
                                src: imageSrc,                       // Fallback: image without link
                                alt: product.title,
                                loading: "lazy",
                                className: "product-image"
                            })
                    ),
                    React.createElement(
                        "div",
                        { className: "info" },

                        React.createElement(
                            "div",
                            { className: "title" },
                            React.createElement(
                                "a",
                                {
                                    href: product.productUrl,
                                    target: "_blank",
                                    rel: "noopener noreferrer"
                                },
                                product.title
                            )
                        ),


                        React.createElement(
                            "div",
                            { className: "badges" },
                            React.createElement("span", { className: "badge badge-normal" }, "Normal"),
                            React.createElement(
                                "span",
                                { className: "badge badge-change" },
                                Number.isFinite(change)
                                    ? `${change >= 0 ? "+" : ""}${change.toFixed(1)}% from saved`
                                    : "Price change unavailable"
                            )

                        ),
                        React.createElement(
                            "div",
                            { className: "prices" },

                            // Last time the current price was checked (TrackedProducts.TimestampUtc)
                            React.createElement(
                                "div",
                                { className: "last-seen" },
                                product.lastSeenUtc
                                    ? `Checked ${new Date(product.lastSeenUtc).toLocaleDateString()}`
                                    : "Checked unavailable"
                            ),

                            // Current price (TrackedProducts.PriceSeen)
                            React.createElement(
                                "div",
                                { className: "price" },
                                Number.isFinite(price) ? `$${price.toFixed(2)}` : "—"
                            ),

                            // Saved price at time item was added (ProductNotifications.SavedPrice)
                            React.createElement(
                                "div",
                                { className: "saved" },
                                Number.isFinite(saved)
                                    ? `Saved at $${saved.toFixed(2)}`
                                    : "Saved price unavailable"
                            )
                        ),




                        React.createElement(
                            "div",
                            { className: "actions" },
                            // BEGIN INSERT: Details icon click
                            React.createElement(Eye, {
                                size: 16,
                                onClick: () => openDetailsDialog(product)
                            }),
                            // END INSERT

                            // BEGIN INSERT: Alert icon click
                            React.createElement(Pencil, {
                                size: 16,
                                onClick: () => openAlertDialog(product)
                            }),
                            // END INSERT

                            // BEGIN INSERT: Share icon click
                            React.createElement(Share2, {
                                size: 16,
                                onClick: () => openShareDialog(product)
                            }),
                            // END INSERT

                            // BEGIN INSERT: Price history icon click
                            React.createElement(LineChart, {
                                size: 16,
                                onClick: () => openHistoryDialog(product)
                            }),
                            // END INSERT

                            // BEGIN INSERT: Tag icon click
                            React.createElement(Tag, {
                                size: 16,
                                onClick: () => openTagsDialog(product)
                            }),
                            // END INSERT
                            // BEGIN INSERT: Comments icon click
                            React.createElement(MessageCircle, {
                                size: 16,
                                onClick: () => openCommentsDialog(product)
                            }),
                            // END INSERT

                            React.createElement(Trash2, {
                                size: 16,
                                onClick: () => openDeleteDialog(product)
                            })
                        )
                        ,
                    )
                );
            })
        ),

        // BEGIN INSERT: ShareDialog render
     
        React.createElement(ShareDialog, {
            open: shareDialogProduct !== null,
            onClose: closeShareDialog,
            product: shareDialogProduct
        })
        // END INSERT
        // BEGIN INSERT: TagsDialog render
        ,
        React.createElement(TagsDialog, {
            open: tagsDialogProduct !== null,
            onClose: closeTagsDialog,
            product: tagsDialogProduct,
            onUpdate: updateProductTags
        })
        // END INSERT
        // BEGIN INSERT: CommentsDialog render
        ,
        React.createElement(CommentsDialog, {
            open: commentsDialogProduct !== null,
            onClose: closeCommentsDialog,
            product: commentsDialogProduct,
            onAddComment: addProductComment
        })
        // END INSERT
        // BEGIN INSERT: PriceHistoryDialog render
        ,
        React.createElement(PriceHistoryDialog, {
            open: historyDialogProduct !== null,
            onClose: closeHistoryDialog,
            product: historyDialogProduct
        })
        // END INSERT
        // BEGIN INSERT: AlertDialog render
        ,
        React.createElement(AlertDialog, {
            open: alertDialogProduct !== null,
            onClose: closeAlertDialog,
            product: alertDialogProduct,
            onSave: saveAlertSettings
        })
        // END INSERT
        // BEGIN INSERT: DetailsDialog render
        ,
        React.createElement(DetailsDialog, {
            open: detailsDialogProduct !== null,
            onClose: closeDetailsDialog,
            product: detailsDialogProduct
        })
        // END INSERT

    );

    // ====================================
    // END REPLACE RETURN BLOCK
    // ====================================

}
