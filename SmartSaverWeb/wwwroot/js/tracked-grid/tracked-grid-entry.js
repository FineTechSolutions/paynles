import React from "react";
import ReactDOM from "react-dom/client";

// Temporary React component until Replit UI is added
function TrackedGridApp() {
    return (
        <div style={{ padding: "20px", fontSize: "1.2rem" }}>
            🚀 React base page is working — ready for Replit UI
        </div>
    );
}

// Mount React
const root = ReactDOM.createRoot(document.getElementById("tracked-grid-root"));
root.render(<TrackedGridApp />);
