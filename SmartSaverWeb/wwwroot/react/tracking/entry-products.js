import React from "https://esm.sh/react@18";
import { createRoot } from "https://esm.sh/react-dom@18/client";

import ProductsApp from "./products/App.js";

const rootEl = document.getElementById("tracking-root");

let rootInstance = null;

function rerender() {
    if (rootInstance) {
        rootInstance.render(
            React.createElement(ProductsApp)
        );
    }
}

window.rerender = rerender; // <-- make it global for ProductCards.js

if (!rootEl) {
    console.error("❌ tracking-root element not found");
} else {
    rootInstance = createRoot(rootEl);
    rerender();
}
