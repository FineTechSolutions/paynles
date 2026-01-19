// ===============================
// ProductDialogs.js
// Delete Dialog (Frontend-only)
// ===============================

import React from "https://esm.sh/react@18";

// Delete Dialog Component
export function DeleteDialog({ open, onClose, onConfirm, product }) {
    if (!open) return null;

    return React.createElement(
        "div",
        { className: "modal-overlay" },

        React.createElement(
            "div",
            { className: "modal-container" },

            // Title
            React.createElement(
                "h2",
                { className: "modal-title" },
                "Delete Product?"
            ),

            // Message
            React.createElement(
                "p",
                { className: "modal-message" },
                `Are you sure you want to remove "${product?.title}" from the list?`
            ),

            // Buttons
            React.createElement(
                "div",
                { className: "modal-actions" },

                // Cancel button
                React.createElement(
                    "button",
                    {
                        className: "modal-btn cancel-btn",
                        onClick: onClose
                    },
                    "Cancel"
                ),

                // Confirm button
                React.createElement(
                    "button",
                    {
                        className: "modal-btn delete-btn",
                        onClick: () => {
                            onConfirm(product.asin);
                            onClose();
                        }
                    },
                    "Delete"
                )
            )
        )
    );
}
// =====================================
// BEGIN INSERT: Share Dialog Component
// =====================================

export function ShareDialog({ open, onClose, product }) {
    if (!open) return null;

    const shareUrl = `https://www.amazon.com/dp/${product?.asin}`;

    return React.createElement(
        "div",
        { className: "modal-overlay" },

        React.createElement(
            "div",
            { className: "modal-container" },

            // Title
            React.createElement("h2", { className: "modal-title" }, "Share Product"),

            // Share link
            React.createElement(
                "p",
                { className: "modal-message" },
                "Use the link below to share this product:"
            ),

            React.createElement(
                "input",
                {
                    className: "share-input",
                    value: shareUrl,
                    readOnly: true,
                    onClick: e => e.target.select()
                }
            ),

            // Buttons
            React.createElement(
                "div",
                { className: "modal-actions" },

                React.createElement(
                    "button",
                    {
                        className: "modal-btn cancel-btn",
                        onClick: onClose
                    },
                    "Close"
                ),

                React.createElement(
                    "button",
                    {
                        className: "modal-btn copy-btn",
                        onClick: () => {
                            navigator.clipboard.writeText(shareUrl);
                            alert("Copied!");
                        }
                    },
                    "Copy Link"
                )
            )
        )
    );
}

// =====================================
// END INSERT: Share Dialog Component
// =====================================
// =====================================
// BEGIN INSERT: Tags Dialog Component
// =====================================

export function TagsDialog({ open, onClose, product, onUpdate }) {
    if (!open) return null;

    const currentTags = product?.tags || [];
    let inputValue = "";

    function addTag() {
        if (inputValue.trim() === "") return;
        const updated = [...currentTags, inputValue.trim()];
        onUpdate(product.asin, updated);
        rerender();
    }

    function removeTag(tag) {
        const updated = currentTags.filter(t => t !== tag);
        onUpdate(product.asin, updated);
        rerender();
    }

    return React.createElement(
        "div",
        { className: "modal-overlay" },

        React.createElement(
            "div",
            { className: "modal-container" },

            React.createElement("h2", { className: "modal-title" }, "Manage Tags"),

            // Tag list
            React.createElement(
                "div",
                { className: "tag-list" },
                currentTags.length === 0
                    ? React.createElement("div", { className: "tag-empty" }, "No tags yet....")
                    : currentTags.map(tag =>
                        React.createElement(
                            "div",
                            { key: tag, className: "tag-item" },

                            React.createElement("span", { className: "tag-label" }, tag),

                            React.createElement(
                                "button",
                                {
                                    className: "tag-remove-btn",
                                    onClick: () => removeTag(tag)
                                },
                                "✕"
                            )
                        )
                    )
            ),

            // Add tag input
            React.createElement(
                "div",
                { className: "tag-input-row" },

                React.createElement("input", {
                    className: "tag-input",
                    placeholder: "New tag...",
                    onInput: e => (inputValue = e.target.value)
                }),

                React.createElement(
                    "button",
                    {
                        className: "modal-btn add-btn",
                        onClick: addTag
                    },
                    "Add"
                )
            ),

            // Close button
            React.createElement(
                "div",
                { className: "modal-actions" },

                React.createElement(
                    "button",
                    {
                        className: "modal-btn cancel-btn",
                        onClick: onClose
                    },
                    "Close"
                )
            )
        )
    );
}

// =====================================
// END INSERT: Tags Dialog Component
// =====================================
// =====================================
// BEGIN INSERT: Comments Dialog Component
// =====================================

export function CommentsDialog({ open, onClose, product, onAddComment }) {
    if (!open) return null;

    const comments = product?.comments || [];
    let newComment = "";

    return React.createElement(
        "div",
        { className: "modal-overlay" },

        React.createElement(
            "div",
            { className: "modal-container comments-modal" },

            // Title
            React.createElement("h2", { className: "modal-title" }, "Comments"),

            // Comment list
            React.createElement(
                "div",
                { className: "comments-list" },
                comments.length === 0
                    ? React.createElement("div", { className: "comment-empty" }, "No comments yet.")
                    : comments.map((c, idx) =>
                        React.createElement(
                            "div",
                            { key: idx, className: "comment-item" },
                            React.createElement("div", { className: "comment-text" }, c)
                        )
                    )
            ),

            // Input box
            React.createElement(
                "textarea",
                {
                    className: "comment-input",
                    placeholder: "Write a comment...",
                    onInput: e => (newComment = e.target.value)
                }
            ),

            // Buttons
            React.createElement(
                "div",
                { className: "modal-actions" },

                React.createElement(
                    "button",
                    {
                        className: "modal-btn cancel-btn",
                        onClick: onClose
                    },
                    "Close"
                ),

                React.createElement(
                    "button",
                    {
                        className: "modal-btn add-btn",
                        onClick: () => {
                            if (newComment.trim() === "") return;
                            onAddComment(product.asin, newComment.trim());
                            rerender();
                        }
                    },
                    "Add Comment"
                )
            )
        )
    );
}

// =====================================
// END INSERT: Comments Dialog Component
// =====================================
// =====================================
// BEGIN INSERT: Price History Dialog Component
// =====================================

export function PriceHistoryDialog({ open, onClose, product }) {
    if (!open) return null;

    // Fallback history if not present
    const history = product?.history || [
        { date: "2024-12-01", price: product.price },
        { date: "2024-12-10", price: product.price },
        { date: "2024-12-20", price: product.price }
    ];

    return React.createElement(
        "div",
        { className: "modal-overlay" },

        React.createElement(
            "div",
            { className: "modal-container history-modal" },

            React.createElement("h2", { className: "modal-title" }, "Price History"),

            // List of prices
            React.createElement(
                "div",
                { className: "history-list" },
                history.map((entry, idx) =>
                    React.createElement(
                        "div",
                        { key: idx, className: "history-row" },
                        React.createElement("span", { className: "history-date" }, entry.date),
                        React.createElement("span", { className: "history-price" }, `$${parseFloat(entry.price).toFixed(2)}`)
                    )
                )
            ),

            // Close button
            React.createElement(
                "div",
                { className: "modal-actions" },

                React.createElement(
                    "button",
                    {
                        className: "modal-btn cancel-btn",
                        onClick: onClose
                    },
                    "Close"
                )
            )
        )
    );
}

// =====================================
// END INSERT: Price History Dialog Component
// =====================================
// =====================================
// BEGIN INSERT: Alert Settings Dialog
// =====================================

export function AlertDialog({ open, onClose, product, onSave }) {
    if (!open) return null;

    let mode = product?.alertMode || "percent_drop";
    let value = product?.alertBelow || "10";

    return React.createElement(
        "div",
        { className: "modal-overlay" },

        React.createElement(
            "div",
            { className: "modal-container alert-modal" },

            React.createElement("h2", { className: "modal-title" }, "Alert Settings"),

            // Select alert type
            React.createElement(
                "div",
                { className: "alert-row" },
                React.createElement("label", { className: "alert-label" }, "Alert Type"),
                React.createElement(
                    "select",
                    {
                        className: "alert-select",
                        defaultValue: mode,
                        onChange: e => (mode = e.target.value)
                    },
                    React.createElement("option", { value: "percent_drop" }, "% drop from saved"),
                    React.createElement("option", { value: "percent_average" }, "% below average"),
                    React.createElement("option", { value: "fixed" }, "Fixed price ($)")
                )
            ),

            // Value entry
            React.createElement(
                "div",
                { className: "alert-row" },
                React.createElement("label", { className: "alert-label" },
                    mode === "fixed" ? "Alert when below ($)" : "Alert when drops by (%)"
                ),
                React.createElement("input", {
                    type: "number",
                    className: "alert-input",
                    defaultValue: value,
                    onInput: e => (value = e.target.value)
                })
            ),

            // Action buttons
            React.createElement(
                "div",
                { className: "modal-actions" },

                React.createElement(
                    "button",
                    {
                        className: "modal-btn cancel-btn",
                        onClick: onClose
                    },
                    "Cancel"
                ),

                React.createElement(
                    "button",
                    {
                        className: "modal-btn add-btn",
                        onClick: () => {
                            onSave(product.asin, mode, value);
                            rerender();
                        }
                    },
                    "Save"
                )
            )
        )
    );
}

// =====================================
// END INSERT: Alert Settings Dialog
// =====================================
// =====================================
// BEGIN INSERT: Details Dialog Component
// =====================================

export function DetailsDialog({ open, onClose, product }) {
    if (!open) return null;

    const amazonUrl = `https://www.amazon.com/dp/${product?.asin}`;

    return React.createElement(
        "div",
        { className: "modal-overlay" },

        React.createElement(
            "div",
            { className: "modal-container details-modal" },

            // Title
            React.createElement("h2", { className: "modal-title" }, "Product Details"),

            // Fields
            React.createElement(
                "div",
                { className: "details-grid" },

                React.createElement("div", { className: "details-label" }, "Title"),
                React.createElement("div", { className: "details-value" }, product.title),

                React.createElement("div", { className: "details-label" }, "Price"),
                React.createElement("div", { className: "details-value" }, `$${parseFloat(product.price).toFixed(2)}`),

                React.createElement("div", { className: "details-label" }, "Saved Price"),
                React.createElement("div", { className: "details-value" }, `$${parseFloat(product.savedPrice).toFixed(2)}`),

                React.createElement("div", { className: "details-label" }, "ASIN"),
                React.createElement("div", { className: "details-value" }, product.asin),

                React.createElement("div", { className: "details-label" }, "Alert Rule"),
                React.createElement("div", { className: "details-value" }, `${product.alertMode}: ${product.alertBelow}`)
            ),

            // Amazon link
            React.createElement(
                "a",
                {
                    href: amazonUrl,
                    target: "_blank",
                    rel: "noopener noreferrer",
                    className: "details-link"
                },
                "View on Amazon"
            ),

            // Close button
            React.createElement(
                "div",
                { className: "modal-actions" },
                React.createElement(
                    "button",
                    {
                        className: "modal-btn cancel-btn",
                        onClick: onClose
                    },
                    "Close"
                )
            )
        )
    );
}

// =====================================
// END INSERT: Details Dialog Component
// =====================================
