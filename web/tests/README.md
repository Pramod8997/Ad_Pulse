# Frontend smoke tests

`browser-smoke.test.mjs` is a dependency-free fallback for this checkout. It uses Node's built-in test runner and the Chrome DevTools Protocol, so it does not add or assume a package manifest. The tests exercise protected navigation, semantic UI states, tenant omission in campaign forms, alert confirmation, invalid import rejection, and mobile keyboard behavior.

Run the portable suite against a running frontend with:

```powershell
$env:ADPULSE_BASE_URL = "http://localhost:5173"
node --test web/tests/browser-smoke.test.mjs
```

Set `CHROME_PATH` when Chrome is installed somewhere other than the default Windows path. With no `ADPULSE_BASE_URL`, the tests are reported as skipped so the Phase 1 checkout remains runnable; the frontend platform agent must run them after the Vue app and its test dependency are available.
