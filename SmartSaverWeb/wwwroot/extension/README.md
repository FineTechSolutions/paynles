# Paynles Chrome Extension (Unlisted-ready)

This build is scoped to Amazon & Walmart product pages and uses minimal permissions (MV3).

## Files
- manifest.json — MV3 manifest with least-privilege settings
- background.js — placeholder service worker (no logic yet)
- content.js — extracts title/price/image/ASIN from Amazon/Walmart pages
- popup.html — small UI to track or flag a product
- popup.js — logic to send data to your API and manage local email
- /icons — required 16/48/128 icons

## Configure
- Update `AFFILIATE_TAG` in `popup.js` if needed.
- Update the `base` URL in `popup.js` to your API host.

## Test locally
1. Go to chrome://extensions → Enable **Developer mode**.
2. **Load unpacked** → select this folder.
3. Open an Amazon or Walmart product page → click the extension → Track.

## Submit to Chrome Web Store
1. Zip the folder contents.
2. In Developer Dashboard: Add new item → upload ZIP.
3. Set Visibility: **Unlisted** (or Private to your Workspace domain).
4. Fill Data Safety to match what the extension does.
5. Publish and share the private link.
