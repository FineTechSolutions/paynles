/* global MiniProductCard */
function ExpandedGroupGrid({ items, onItemsChange }) {
    const [selectedItems, setSelectedItems] = React.useState([]);

    // Normalize ids from server (handles PascalCase or camelCase)
    const getId = (p) => p.ProductId || p.productId || p.Id || p.id || null;

    console.log("ExpandedGroupGrid items:", items.map((p) => getId(p)));

    const toggleItemSelection = (id) => {
        if (!id) return; // no id, nothing to toggle

        setSelectedItems((prev) => {
            const newSelection = prev.includes(id)
                ? prev.filter((x) => x !== id)
                : [...prev, id];

            console.log("🧩 selectedItems now:", newSelection);
            return newSelection;
        });
    };
    // === API HELPERS (DEBUG MODE) ===
    const callApi = async (endpoint, productIds) => {
        try {
            console.log(`🔹 [DEBUG] Calling ${endpoint}`, productIds);
            const response = await fetch(`/api/MyTracked/${endpoint}`, {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify(productIds),
            });
            const data = await response.json();
            console.log(`✅ [DEBUG] Response from ${endpoint}:`, data);
            return data;
        } catch (err) {
            console.error(`❌ [DEBUG] ${endpoint} failed:`, err);
            return null;
        }
    };

    const handleKeepSelected = async () => {
        if (selectedItems.length === 0) return;
        const result = await callApi("keep", selectedItems);
        if (result && result.success) {
            const remaining = items.filter((p) => !selectedItems.includes(getId(p)));
            setSelectedItems([]);
            onItemsChange && onItemsChange(remaining);
        }
    };

    const handleDeleteSelected = async () => {
        if (selectedItems.length === 0) return;
        const result = await callApi("dispose", selectedItems);
        if (result && result.success) {
            const remaining = items.filter((p) => !selectedItems.includes(getId(p)));
            setSelectedItems([]);
            onItemsChange && onItemsChange(remaining);
        }
    };

    return (
        <div>
            {selectedItems.length > 0 && (
                <div
                    style={{
                        marginBottom: "8px",
                        padding: "6px 10px",
                        background: "#eef1f6",
                        borderRadius: "6px",
                        display: "flex",
                        justifyContent: "flex-end",
                        gap: "8px",
                    }}
                >
                    <button
                        type="button"
                        className="btn btn-sm btn-success"
                        onClick={handleKeepSelected}
                    >
                        Keep Selected
                    </button>
                    <button
                        type="button"
                        className="btn btn-sm btn-danger"
                        onClick={handleDeleteSelected}
                    >
                        Delete Selected
                    </button>
                </div>
            )}


            <div className="mini-card-grid">
                {items.map((p, index) => {
                    const id = getId(p);
                    const keyParts = [id, p.MarketplaceProductCode, p.Title || p.title, index].filter(Boolean);
                    const safeKey = keyParts.join("_");

                    return (
                        <MiniProductCard
                            key={safeKey}
                            product={p}
                            isSelected={selectedItems.includes(id)}
                            onToggleSelect={() => toggleItemSelection(id)}
                        />
                    );
                })}
            </div>
        </div>
    );
}
