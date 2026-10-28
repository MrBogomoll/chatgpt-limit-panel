# Release conventions

One version identifies the source, manifests, built ZIP and public release. Current release build: **1.4.0**. The published Latest release is controlled separately on GitHub.

- MAJOR changes substantially alter the product.
- MINOR adds functionality.
- PATCH fixes defects.
- Prereleases use beta or rc identifiers before the stable version.
- Public notes describe user-visible changes without test counts or user-confirmation language.
- Windows archives use chatgpt-limit-panel-VERSION-windows-x64.zip with a SHA-256 sidecar.
- Publish complete assets before marking a release Latest; keep repository visibility unchanged and preserve installation preferences.
