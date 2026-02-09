import React from "https://esm.sh/react@18";
import ReactDOM from "https://esm.sh/react-dom@18/client";

export default function NotificationsPage() {
    const [items, setItems] = React.useState([]);
    const [selected, setSelected] = React.useState(null);
    const [loading, setLoading] = React.useState(true);
    const [error, setError] = React.useState("");

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

    if (loading) {
        return React.createElement("div", null, "Loading notifications…");
    }

    if (error) {
        return React.createElement("div", null, "Error: " + error);
    }
    let productUrl = null;

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
                        selected.notifications
                    )

                    : React.createElement(
                        "div",
                        { className: "email-empty" },
                        "Select a notification to preview the email"
                    )
            )
        )
    );
}

function DailyDigestEmail(email, items) {
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
                                        href: productUrl,
                                        target: "_blank",
                                        rel: "noopener noreferrer"
                                    },
                                    n.Title
                                )
                                : React.createElement(
                                    "div",
                                    { className: "product-title" },
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
                                    `Was $${n.SavedPrice}`
                                ),
                                React.createElement(
                                    "div",
                                    { className: "price-new" },
                                    `Now $${n.PriceSeen}`
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
                                )
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
