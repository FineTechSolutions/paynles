function MiniProductCard({ product, isSelected, onToggleSelect }) {
    const imageUrl = product.ImageUrl || product.imageUrl || "";
    const title = product.Title || product.title || "";
    const price =
        product.PriceSeen !== undefined && product.PriceSeen !== null
            ? product.PriceSeen
            : product.priceSeen;

    let base = product.BaseDomain || product.baseDomain || "";
    const code =
        product.MarketplaceProductCode || product.marketplaceProductCode || "";

    const handleCardClick = (e) => {
        if (e.target.type === "checkbox") return;

        if (!code) {
            console.warn("⚠️ Missing MarketplaceProductCode");
            return;
        }

        // Normalize domain
        base = base.replace(/^https?:\/\//, "").replace(/\/$/, "");
        if (!base) {
            if (
                product.MarketplaceCode &&
                product.MarketplaceCode.toLowerCase().includes("walmart")
            ) {
                base = "walmart.com";
            } else {
                base = "amazon.com";
            }
        }

        let path = `/dp/${code}`;
        if (base.includes("walmart")) path = `/ip/${code}`;
        else if (base.includes("homedepot")) path = `/p/${code}`;

        const url = `https://${base}${path}`;
        console.log("🌐 Opening:", url);
        window.open(url, "_blank", "noopener,noreferrer");
    };

    return (
        <div
            className="mini-product-card"
            onClick={handleCardClick}
            style={{
                position: "relative",
                cursor: "pointer",
                border: isSelected ? "2px solid #0d6efd" : "1px solid #e0e0e0",
                borderRadius: "6px",
                padding: "6px",
                margin: "6px",
                width: "160px",
                textAlign: "center",
                transition: "all 0.15s ease",
                backgroundColor: "#fff",
            }}
        >
            {/* Checkbox */}
            <input
                type="checkbox"
                checked={!!isSelected}
                onClick={(e) => e.stopPropagation()}
                onChange={(e) => {
                    e.stopPropagation();
                    onToggleSelect();
                }}
                style={{
                    position: "absolute",
                    top: "6px",
                    left: "6px",
                    zIndex: 10,
                    width: "18px",
                    height: "18px",
                }}
            />

            {/* Image */}
            <div
                style={{
                    height: "120px",
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "center",
                    marginBottom: "8px",
                }}
            >
                {imageUrl ? (
                    <img
                        src={imageUrl}
                        alt={title}
                        style={{ maxWidth: "100%", maxHeight: "100%", objectFit: "contain" }}
                    />
                ) : (
                    <span className="text-muted small">No image</span>
                )}
            </div>

            {/* Title */}
            <div
                title={title}
                style={{
                    fontSize: "0.85rem",
                    fontWeight: "500",
                    minHeight: "32px",
                    overflow: "hidden",
                    textOverflow: "ellipsis",
                    marginBottom: "4px",
                }}
            >
                {title || "Untitled"}
            </div>

            {/* Price */}
            {price != null && !isNaN(price) && (
                <div
                    style={{
                        fontSize: "0.9rem",
                        color: "#198754",
                        fontWeight: "600",
                    }}
                >
                    ${Number(price).toFixed(2)}
                </div>
            )}
        </div>
    );
}
