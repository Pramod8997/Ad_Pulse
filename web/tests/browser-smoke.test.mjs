import assert from "node:assert/strict";
import { spawn } from "node:child_process";
import { mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { describe, after, before, test } from "node:test";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { setTimeout as delay } from "node:timers/promises";

const baseUrl = process.env.ADPULSE_BASE_URL;
const chromePath =
  process.env.CHROME_PATH ??
  "C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe";

const authCopy = /sign in|log in|unauthori[sz]ed|access denied|session expired/i;
const stateCopy = /loading|skeleton|error|unable|failed|invalid|no [a-z ]+|nothing|empty/i;

class CdpPage {
  constructor(socket) {
    this.socket = socket;
    this.nextId = 0;
    this.pending = new Map();
    this.events = new Map();

    socket.addEventListener("message", (event) => {
      const message = JSON.parse(String(event.data));
      if (message.id) {
        const pending = this.pending.get(message.id);
        if (!pending) return;
        this.pending.delete(message.id);
        if (message.error) pending.reject(new Error(JSON.stringify(message.error)));
        else pending.resolve(message.result);
        return;
      }

      for (const listener of this.events.get(message.method) ?? []) listener(message.params);
    });
  }

  static async connect(webSocketUrl) {
    const socket = new WebSocket(webSocketUrl);
    await new Promise((resolve, reject) => {
      socket.addEventListener("open", resolve, { once: true });
      socket.addEventListener("error", () => reject(new Error("Could not connect to Chrome DevTools")), {
        once: true,
      });
    });
    const page = new CdpPage(socket);
    await page.send("Page.enable");
    await page.send("Runtime.enable");
    return page;
  }

  send(method, params = {}) {
    const id = ++this.nextId;
    return new Promise((resolve, reject) => {
      this.pending.set(id, { resolve, reject });
      this.socket.send(JSON.stringify({ id, method, params }));
    });
  }

  on(method, listener) {
    const listeners = this.events.get(method) ?? new Set();
    listeners.add(listener);
    this.events.set(method, listeners);
    return () => listeners.delete(listener);
  }

  async goto(path) {
    const loaded = new Promise((resolve) => {
      const remove = this.on("Page.loadEventFired", () => {
        remove();
        resolve();
      });
    });
    await this.send("Page.navigate", { url: new URL(path, baseUrl).href });
    await Promise.race([loaded, delay(5000)]);
    await delay(150);
  }

  async evaluate(expression) {
    const result = await this.send("Runtime.evaluate", {
      expression,
      awaitPromise: true,
      returnByValue: true,
      userGesture: true,
    });
    if (result.exceptionDetails) throw new Error(result.exceptionDetails.text ?? "Browser evaluation failed");
    return result.result?.value;
  }

  async snapshot() {
    return this.evaluate(`(() => {
      const visible = (element) => {
        const style = getComputedStyle(element);
        return style.display !== "none" && style.visibility !== "hidden";
      };
      const name = (element) => (element.getAttribute("aria-label") ||
        element.getAttribute("title") || element.innerText || element.textContent || "")
        .replace(/\\s+/g, " ").trim();
      const controls = [...document.querySelectorAll("button,a,input,select,textarea,[role=button]")]
        .filter(visible).map((element) => ({
          name: name(element),
          tag: element.tagName,
          type: element.getAttribute("type") || "",
          disabled: element.matches(":disabled,[aria-disabled=true]"),
        }));
      const body = document.body?.innerText || "";
      const stateRegions = [...document.querySelectorAll(
        '[aria-busy=true],[role=status],[role=alert],[data-state],dialog,[role=dialog]'
      )].filter(visible).map(name);
      return {
        url: location.href,
        text: body,
        controls,
        stateRegions,
        hasForm: Boolean(document.querySelector("form")),
        hasAppShell: Boolean(document.querySelector('[data-app-shell],aside,nav[aria-label]')),
        hasDialog: Boolean(document.querySelector('dialog,[role=dialog]')),
        overflow: document.documentElement.scrollWidth > window.innerWidth + 1,
      };
    })()`);
  }

  async clickByText(pattern) {
    const source = pattern.source;
    return this.evaluate(`(() => {
      const matcher = new RegExp(${JSON.stringify(source)}, "i");
      const name = (element) => (element.getAttribute("aria-label") ||
        element.getAttribute("title") || element.innerText || element.textContent || "")
        .replace(/\\s+/g, " ").trim();
      const element = [...document.querySelectorAll("button,a,[role=button]")]
        .find((candidate) => matcher.test(name(candidate)));
      if (!element) return false;
      element.focus();
      element.click();
      return true;
    })()`);
  }

  async setViewport(width, height) {
    await this.send("Emulation.setDeviceMetricsOverride", {
      width,
      height,
      deviceScaleFactor: 1,
      mobile: true,
    });
  }

  async pressTab() {
    await this.send("Input.dispatchKeyEvent", { type: "keyDown", key: "Tab", code: "Tab" });
    await this.send("Input.dispatchKeyEvent", { type: "keyUp", key: "Tab", code: "Tab" });
    return this.evaluate(`(() => {
      const element = document.activeElement;
      return element ? {
        tag: element.tagName,
        name: (element.getAttribute("aria-label") || element.innerText || element.textContent || "")
          .replace(/\\s+/g, " ").trim(),
      } : null;
    })()`);
  }

  async pressEscape() {
    await this.send("Input.dispatchKeyEvent", { type: "keyDown", key: "Escape", code: "Escape" });
    await this.send("Input.dispatchKeyEvent", { type: "keyUp", key: "Escape", code: "Escape" });
  }

  async setInvalidFile(filePath) {
    await this.send("DOM.enable");
    const document = await this.send("DOM.getDocument", { depth: 0 });
    const result = await this.send("DOM.querySelector", {
      nodeId: document.root.nodeId,
      selector: 'input[type="file"]',
    });
    if (!result.nodeId) return false;
    await this.send("DOM.setFileInputFiles", { nodeId: result.nodeId, files: [filePath] });
    await this.evaluate(`document.querySelector('input[type="file"]').dispatchEvent(
      new Event("change", { bubbles: true })
    )`);
    return true;
  }

  close() {
    this.socket.close();
  }
}

let browser;
let profile;
let page;

async function openBrowser() {
  profile = await mkdtemp(join(tmpdir(), "adpulse-cdp-"));
  browser = spawn(chromePath, [
    "--headless=new",
    "--disable-gpu",
    "--disable-dev-shm-usage",
    "--no-first-run",
    "--no-default-browser-check",
    "--remote-allow-origins=*",
    "--remote-debugging-port=0",
    `--user-data-dir=${profile}`,
    "about:blank",
  ], { stdio: "ignore" });

  const activePortPath = join(profile, "DevToolsActivePort");
  let activePort;
  for (let attempt = 0; attempt < 100; attempt += 1) {
    try {
      activePort = (await readFile(activePortPath, "utf8")).trim().split(/\r?\n/);
      break;
    } catch {
      await delay(50);
    }
  }
  if (!activePort) throw new Error(`Chrome did not expose DevTools at ${chromePath}`);

  const [port, browserPath] = activePort;
  const targetResponse = await fetch(
    `http://127.0.0.1:${port}/json/new?${encodeURIComponent("about:blank")}`,
    { method: "PUT" },
  );
  if (!targetResponse.ok) throw new Error(`Chrome target creation failed: ${targetResponse.status}`);
  const target = await targetResponse.json();
  page = await CdpPage.connect(target.webSocketDebuggerUrl);
  return { port, browserPath };
}

async function closeBrowser() {
  page?.close();
  if (browser && !browser.killed) {
    browser.kill();
    await new Promise((resolve) => browser.once("exit", resolve));
  }
  if (profile) {
    for (let attempt = 0; attempt < 5; attempt += 1) {
      try {
        await rm(profile, { recursive: true, force: true });
        break;
      } catch (error) {
        if (attempt === 4) throw error;
        await delay(100);
      }
    }
  }
}

function smoke(name, callback) {
  if (!baseUrl) {
    return test(name, { skip: "ADPULSE_BASE_URL is not set; frontend app is not present in this checkout" }, () => {});
  }
  return test(name, callback);
}

function isProtectedRedirect(snapshot) {
  return /\/sign-in(?:[/?#]|$)/i.test(snapshot.url) || authCopy.test(snapshot.text);
}

describe("AdPulse browser smoke", () => {
  before(async () => {
    if (baseUrl) await openBrowser();
  });

  after(async () => {
    await closeBrowser();
  });

  smoke("protected navigation does not expose the application shell", async () => {
    for (const route of ["/app", "/app/campaigns", "/app/alerts", "/app/imports"]) {
      await page.goto(route);
      const snapshot = await page.snapshot();
      assert.ok(
        authCopy.test(snapshot.text) || !snapshot.hasAppShell,
        `${route} rendered a protected shell without an authenticated session`,
      );
    }
  });

  smoke("campaigns expose an accessible loading, error, empty, or ready state", async () => {
    await page.goto("/app/campaigns");
    const snapshot = await page.snapshot();
    if (isProtectedRedirect(snapshot)) return;
    assert.ok(
      snapshot.stateRegions.length > 0 || stateCopy.test(snapshot.text),
      "campaigns page has no observable state region or state copy",
    );
  });

  smoke("campaign forms never ask the browser to choose a tenant", async () => {
    await page.goto("/app/campaigns");
    const snapshot = await page.snapshot();
    if (isProtectedRedirect(snapshot) || !snapshot.hasForm) return;
    const tenantControls = await page.evaluate(`(() => [...document.querySelectorAll("form input,form select,form textarea")]
      .filter((element) => /tenant[_-]?id/i.test(
        [element.name, element.id, element.getAttribute("data-field")].filter(Boolean).join(" ")
      )).length)()`);
    assert.equal(tenantControls, 0, "campaign form exposed a tenant selector or tenant ID field");
  });

  smoke("alert acknowledge/resolve actions require visible confirmation", async () => {
    await page.goto("/app/alerts");
    const snapshot = await page.snapshot();
    if (isProtectedRedirect(snapshot)) return;
    const actionFound = await page.clickByText(/acknowledge|resolve|reopen/);
    if (!actionFound) {
      assert.match(snapshot.text, /no alerts|empty|nothing/i, "alerts page has no lifecycle action or empty state");
      return;
    }
    await delay(100);
    const afterClick = await page.snapshot();
    assert.ok(afterClick.hasDialog || /confirm|are you sure|reason|resolve this|acknowledge this/i.test(afterClick.text),
      "alert lifecycle action did not show a confirmation or reason prompt");
  });

  smoke("invalid import files produce a rejection state", async () => {
    await page.goto("/app/imports");
    const snapshot = await page.snapshot();
    if (isProtectedRedirect(snapshot)) return;
    const fixture = join(profile, "invalid-import.csv");
    await writeFile(fixture, "not,a,known,column\nthis is not a campaign row\n", "utf8");
    const fileInputFound = await page.setInvalidFile(fixture);
    if (!fileInputFound) {
      assert.match(snapshot.text, /no imports|empty|nothing/i, "imports page has no file input or empty state");
      return;
    }
    await delay(250);
    const afterFile = await page.snapshot();
    assert.ok(
      /invalid|reject|error|unsupported|column|fix/i.test(afterFile.text) ||
        afterFile.controls.some((control) => control.disabled),
      "invalid import did not expose a rejection message or disable confirmation",
    );
  });

  smoke("mobile shell remains keyboard reachable and avoids document overflow", async () => {
    await page.setViewport(390, 844);
    await page.goto("/app");
    const initial = await page.snapshot();
    if (isProtectedRedirect(initial) && !initial.hasAppShell) return;
    assert.equal(initial.overflow, false, "mobile shell horizontally overflows the viewport");
    const focused = await page.pressTab();
    assert.ok(focused?.tag, "Tab did not move focus to an interactive element");
    const menuFound = await page.clickByText(/menu|navigation|sidebar/);
    if (menuFound) {
      await delay(100);
      const open = await page.snapshot();
      assert.ok(open.hasAppShell && /overview|campaigns|alerts|imports/i.test(open.text),
        "mobile navigation did not expose labelled route links");
      await page.pressEscape();
    }
  });
});
