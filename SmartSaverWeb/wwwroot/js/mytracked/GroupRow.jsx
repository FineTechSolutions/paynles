/* global ExpandedGroupGrid */
function GroupRow({ title, items }) {
    const [isExpanded, setIsExpanded] = React.useState(false);
    const [groupItems, setGroupItems] = React.useState(items);

    // Keep local items in sync when parent updates
    React.useEffect(() => {
        setGroupItems(items);
    }, [items]);

    // === Meta data ===
    const itemCount = items?.length || 0;
    const firstItem = items && items.length > 0 ? items[0] : null;
    const timestamp =
        (firstItem && (firstItem.TimestampUtc || firstItem.timestampUtc)) || "";
    const dateObj = timestamp ? new Date(timestamp) : null;
    const formattedDate = dateObj ? dateObj.toLocaleString() : "Unknown date";

    let [datePart, timePart] = ["Unknown date", ""];
    if (typeof formattedDate === "string" && formattedDate.includes(",")) {
        [datePart, timePart] = formattedDate.split(",");
    } else {
        datePart = formattedDate || "Unknown date";
    }

    // === Preview images ===
    const previewSourceItem =
        items.find((p) => p.RecordType === "PrimaryProduct") || items[0] || null;
    const relatedItem = items.find((p) => p.RecordType === "RelatedProduct");

    const previewImage =
        (previewSourceItem && (previewSourceItem.ImageUrl || previewSourceItem.imageUrl)) ||
        "https://paynles.com/img/fallback.png";

    const relatedPreview =
        (relatedItem && (relatedItem.ImageUrl || relatedItem.imageUrl)) || null;

    // === Toggle Expand ===
    const handleRowClick = (e) => {
        if (e.target.type === "checkbox") return; // ignore checkbox clicks
        setIsExpanded((prev) => !prev);
    };

    return (
        <div className="group-row-wrapper">
            {/* === Group Header === */}
            <div
                className="group-row-card"
                data-group-id={firstItem && firstItem.GroupId}
                onClick={handleRowClick}
                style={{
                    cursor: "pointer",
                    display: "flex",
                    alignItems: "center",
                    padding: "10px",
                    borderBottom: "1px solid #e5e7eb",
                    background: "#fff",
                    borderRadius: "6px",
                }}
            >
                {/* Thumbnail images */}
                <div className="group-row-thumbnail" style={{ display: "flex", gap: "4px" }}>
                    {/* Primary */}
                    <div
                        style={{
                            width: "64px",
                            height: "64px",
                            borderRadius: "6px",
                            overflow: "hidden",
                            backgroundColor: "#fff",
                            display: "flex",
                            alignItems: "center",
                            justifyContent: "center",
                        }}
                    >
                        <img
                            src={previewImage}
                            alt="Primary"
                            style={{ width: "100%", height: "100%", objectFit: "contain" }}
                        />
                    </div>

                    {/* Related (optional) */}
                    {relatedPreview && (
                        <div
                            style={{
                                width: "64px",
                                height: "64px",
                                borderRadius: "6px",
                                overflow: "hidden",
                                backgroundColor: "#fff",
                                display: "flex",
                                alignItems: "center",
                                justifyContent: "center",
                            }}
                        >
                            <img
                                src={relatedPreview}
                                alt="Related"
                                style={{ width: "100%", height: "100%", objectFit: "contain" }}
                            />
                        </div>
                    )}
                </div>

                {/* Header info */}
                <div
                    className="group-row-main"
                    style={{ marginLeft: "12px", flexGrow: 1 }}
                >
                    <div className="group-row-title">
                        Imported on {datePart || "Unknown date"}
                    </div>
                    <div className="group-row-meta text-muted small">
                        {timePart ? `Saved at ${timePart.trim()}` : ""} • {itemCount} item
                        {itemCount !== 1 ? "s" : ""}
                    </div>
                </div>

                {/* Chevron */}
                <div
                    className={"group-row-chevron" + (isExpanded ? " expanded" : "")}
                    style={{
                        fontSize: "1.5rem",
                        transform: isExpanded ? "rotate(90deg)" : "rotate(0deg)",
                        transition: "transform 0.2s ease",
                    }}
                >
                    ›
                </div>
            </div>

            {/* === Collapsible content === */}
            <div
                className={
                    "group-expand-wrapper " + (isExpanded ? "expanded" : "collapsing")
                }
                style={{
                    marginLeft: "60px",
                    marginTop: "10px",
                    transition: "max-height 0.3s ease",
                }}
            >
                {isExpanded && (
                    <ExpandedGroupGrid
                        items={groupItems}
                        onItemsChange={(updated) => setGroupItems(updated)}
                    />
                )}
            </div>
        </div>
    );
}
