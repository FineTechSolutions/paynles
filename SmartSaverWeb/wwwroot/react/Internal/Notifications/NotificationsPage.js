import React from "https://esm.sh/react@18";
import ReactDOM from "https://esm.sh/react-dom@18/client";

export default function NotificationsPage() {
    const [items, setItems] = React.useState([]);
    const [selected, setSelected] = React.useState(null);
    const [loading, setLoading] = React.useState(true);
    const [error, setError] = React.useState("");
    const [shareOpen, setShareOpen] = React.useState(false);
    const [shareDraft, setShareDraft] = React.useState({ subject: "", body: "" });
    React.useEffect(() => {
        fetch("/internal/notifications/send-queue")
            .then(r => r.json())
            .then(data => {
                setItems(Array.isArray(data) ? data : []);
                setLoading(false);
            })
            .catch(err => {
                console.error(err);
                setError(String(err));
                setLoading(false);
            });
    }, []);

    const dailyEmails = React.useMemo(() => {
        const map = {};

        items.forEach(n => {
            if (!map[n.Email]) {
                map[n.Email] = [];
            }
            map[n.Email].push(n);
        });

        return Object.keys(map).map(email => ({
            email,
            notifications: map[email]
        }));
    }, [items]);
    function openShare(n, productUrl) {
        const savedAt = Number(n.SavedPrice || 0);
        const nowPrice = Number(n.PriceSeen || 0);
        const requestedPercent = Number(n.DropValue || 0);

        const dropPercent =
            savedAt > 0 && nowPrice > 0
                ? Math.round(((savedAt - nowPrice) / savedAt) * 100)
                : null;

        const subject = `Price dropped: ${n.Title}`;
        const body =
            `Hey!\n\n` +
            `I saved this item at $${n.SavedPrice} and set an alert for ${requestedPercent}% drop.\n` +
            (dropPercent === null
                ? `It just triggered, and the current price is $${n.PriceSeen}.\n\n`
                : `It’s now down ${dropPercent}% since I saved it, and the current price is $${n.PriceSeen}.\n\n`) +
            (productUrl ? `Link: ${productUrl}\n\n` : "") +
            `- Sent via Paynles`;

        setShareDraft({ subject, body });
        setShareOpen(true);
    }
    if (loading) {
        return React.createElement("div", null, "Loading notifications…");
    }

    if (error) {
        return React.createElement("div", null, "Error: " + error);
    }
  

    return React.createElement(
        "div",
        { className: "internal-notifications" },

        React.createElement(
            "div",
            { className: "notifications-layout" },
            // LEFT PANEL — DAILY EMAILS
            React.createElement(
                "div",
                { className: "notifications-list" },

                React.createElement(
                    "h3",
                    null,
                    `Daily Emails (${dailyEmails.length})`
                ),

                dailyEmails.map(batch =>
                    React.createElement(
                        "div",
                        {
                            key: batch.email,
                            className:
                                "notification-card" +
                                (selected?.email === batch.email ? " selected" : ""),
                            onClick: () => setSelected(batch)
                        },

                        React.createElement(
                            "div",
                            { className: "title" },
                            batch.email
                        ),

                        React.createElement(
                            "div",
                            { className: "meta" },
                            `Today • ${batch.notifications.length} alert${batch.notifications.length > 1 ? "s" : ""
                            }`
                        ),

                        React.createElement(
                            "div",
                            { className: "rule" },
                            "Daily Digest"
                        )
                    )
                )
            ),


            // RIGHT PANEL — EMAIL PREVIEW
            React.createElement(
                "div",
                { className: "email-preview" },
                selected
                    ? DailyDigestEmail(
                        selected.email,
                        selected.notifications,
                        openShare
                    )

                    : React.createElement(
                        "div",
                        { className: "email-empty" },
                        "Select a notification to preview the email"
                    )
            )
        ),
        shareOpen
            ? React.createElement(
                "div",
                { className: "share-overlay", onClick: () => setShareOpen(false) },
                React.createElement(
                    "div",
                    { className: "share-modal", onClick: e => e.stopPropagation() },

                    React.createElement("h3", null, "Share"),

                    React.createElement("label", null, "Subject"),
                    React.createElement("input", {
                        className: "share-input",
                        value: shareDraft.subject,
                        onChange: e =>
                            setShareDraft(v => ({ ...v, subject: e.target.value }))
                    }),

                    React.createElement("label", null, "Body"),
                    React.createElement("textarea", {
                        className: "share-textarea",
                        rows: 8,
                        value: shareDraft.body,
                        onChange: e =>
                            setShareDraft(v => ({ ...v, body: e.target.value }))
                    }),

                    React.createElement(
                        "div",
                        { className: "share-actions" },

                        React.createElement(
                            "div",
                            { className: "share-left-actions" },

                            React.createElement(
                                "button",
                                {
                                    type: "button",
                                    className: "share-secondary",
                                    onClick: () => {
                                        navigator.clipboard.writeText(
                                            shareDraft.subject + "\n\n" + shareDraft.body
                                        );
                                    }
                                },
                                "Copy"
                            ),

                            React.createElement(
                                "button",
                                {
                                    type: "button",
                                    className: "share-secondary",
                                    onClick: () => {
                                        window.open(
                                            "mailto:?subject=" +
                                            encodeURIComponent(shareDraft.subject) +
                                            "&body=" +
                                            encodeURIComponent(shareDraft.body)
                                        );
                                    }
                                },
                                "Open in Email"
                            )
                        ),

                        React.createElement(
                            "button",
                            {
                                type: "button",
                                className: "share-primary",
                                onClick: () => setShareOpen(false)
                            },
                            "Done"
                        )
                    )

                )
            )
            : null

    );
}


function DailyDigestEmail(email, items, openShare) {

    return React.createElement(
        "div",
        { className: "email-canvas" },

        // Header
        React.createElement(
            "div",
            { className: "email-header" },
            React.createElement(
                "div",
                { className: "email-to" },
                "To ",
                email
            ),
            React.createElement(
                "div",
                { className: "email-subject" },
                `Your Daily Price Drops (${items.length})`
            )
        ),

        // Body
        React.createElement(
            "div",
            { className: "email-content" },

            React.createElement(
                "h2",
                null,
                "Here’s your daily Paynles price drop summary 💸"
            ),

            React.createElement(
                "p",
                { className: "digest-intro" },
                `We found ${items.length} price drop${items.length > 1 ? "s" : ""} for you today.`
            ),

            // Product list
            React.createElement(
                "div",
                { className: "digest-list" },
                items.map(n => {
                    const productUrl =
                        n.MarketplaceName === "Amazon US"
                            ? "https://www.amazon.com/dp/" +
                            n.MarketplaceProductCode +
                            "?tag=paynles-20"
                            : null;
                    const savedAt = Number(n.SavedPrice || 0);
                    const nowPrice = Number(n.PriceSeen || 0);

                    const dropPercent =
                        savedAt > 0 && nowPrice > 0
                            ? Math.round(((savedAt - nowPrice) / savedAt) * 100)
                            : null;

                    const requestedPercent = Number(n.DropValue || 0); // user requested threshold (Y%)
                    return React.createElement(
                        "div",
                        { key: n.NotificationId, className: "digest-item" },

                        React.createElement(
                            "div",
                            { className: "product-image" },
                            n.ImageUrl
                                ? React.createElement("img", {
                                    src: n.ImageUrl,
                                    alt: n.Title
                                })
                                : React.createElement(
                                    "div",
                                    { className: "image-placeholder" },
                                    "No image"
                                )
                        ),

                        React.createElement(
                            "div",
                            { className: "product-info" },
                            productUrl
                                ? React.createElement(
                                    "a",
                                    {
                                        className: "product-title product-title-link",
                                        title: n.Title,
                                        href: productUrl,
                                        target: "_blank",
                                        rel: "noopener noreferrer"
                                    },
                                    n.Title
                                )
                                : React.createElement(
                                    "div",
                                    { className: "product-title", title: n.Title },
                                    n.Title
                                ),



                            React.createElement(
                                "div",
                                { className: "product-asin" },
                                `${n.MarketplaceName} • ASIN: ${n.MarketplaceProductCode}`
                            ),

                            React.createElement(
                                "div",
                                { className: "price-box" },
                                React.createElement(
                                    "div",
                                    { className: "price-old" },
                                    `Saved At $${n.SavedPrice}`
                                ),
                                React.createElement(
                                    "div",
                                    { className: "price-new" },
                                    `Now $${n.PriceSeen}`
                                )
                            ),
                            React.createElement(
                                "div",
                                { className: "rule-summary" },

                                // user intent
                                React.createElement(
                                    "div",
                                    { className: "rule-line" },
                                    `You saved it with the intention to be notified when it drops at least ${requestedPercent}% below your saved price.`
                                ),

                                // actual outcome
                                React.createElement(
                                    "div",
                                    { className: "rule-line" },
                                    dropPercent === null
                                        ? "Drop since saved: (unavailable)"
                                        : `Now it has dropped ${dropPercent}% since you saved it.`
                                )
                            ),

                            productUrl
                                ? React.createElement(
                                    "a",
                                    {
                                        className: "cta-button",
                                        href: productUrl,
                                        target: "_blank",
                                        rel: "noopener noreferrer"
                                    },
                                    "View Deal"
                                )
                                : React.createElement(
                                    "div",
                                    { className: "cta-button disabled" },
                                    "View Deal"
                                ),
                            React.createElement(
                                "button",
                                {
                                    type: "button",
                                    className: "share-button",
                                    onClick: () => openShare(n, productUrl)
                                },
                                "Share this finding with your friend"
                            ),

                        )
                    );
                })

            ),

            React.createElement(
                "p",
                { className: "email-footnote" },
                "You’ll receive at most one of these emails per day to avoid inbox overload."
            )
        )
    );
}


NotificationsPage.mount = function (el) {
    const root = ReactDOM.createRoot(el);
    root.render(React.createElement(NotificationsPage));
};
