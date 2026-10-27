# Chainbound Legal Documents

This folder contains Chainbound's public legal documents, published by SunOrc Games.

| Document | File | Public URL (once committed to `main`) |
|---|---|---|
| Terms of Use | [`TERMS_OF_USE.md`](./TERMS_OF_USE.md) | https://github.com/Cronusart/Chainbound/blob/main/Docs/Legal/TERMS_OF_USE.md |
| Privacy Policy | [`PRIVACY_POLICY.md`](./PRIVACY_POLICY.md) | https://github.com/Cronusart/Chainbound/blob/main/Docs/Legal/PRIVACY_POLICY.md |

Use these URLs in the Solana dApp Store publisher form fields **"Terms of Use"** and **"Privacy Policy."**

**Before publishing:** fill in every `[[DOUBLE BRACKET]]` placeholder and set the effective date. When you change a document, update its "Last updated" date.

## Optional: cleaner URLs with GitHub Pages

For friendlier, store-style links, you can serve these files with GitHub Pages:

1. In the repo, go to **Settings → Pages**.
2. Under **Build and deployment**, choose **Deploy from a branch**, branch `main`, folder `/docs` (or set up a small Pages workflow if you want to keep the `Docs/` folder name as-is, since Pages' branch option expects lowercase `/docs` or root).
3. GitHub Pages renders Markdown via Jekyll, giving URLs like:
   - `https://cronusart.github.io/Chainbound/Legal/TERMS_OF_USE`
   - `https://cronusart.github.io/Chainbound/Legal/PRIVACY_POLICY`
4. Optionally add a custom domain (for example `chainbound.game/terms`) in the Pages settings.

The GitHub `blob` URLs above work fine without Pages; Pages just gives a cleaner page without the GitHub repo UI around it.
