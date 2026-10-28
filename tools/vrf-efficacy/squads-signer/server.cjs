"use strict";
// Builds the signing page and serves it on 127.0.0.1 only. For the Seeker, forward the port over
// USB (`adb reverse tcp:8787 tcp:8787`) and open http://localhost:8787 on the phone; localhost is a
// secure context, which Mobile Wallet Adapter needs.
//
//   node tools/vrf-efficacy/squads-signer/server.cjs [--port 8787]
const fs = require("fs");
const http = require("http");
const path = require("path");
const esbuild = require("esbuild");

const root = __dirname;
const portArg = process.argv.indexOf("--port");
const port = portArg > 0 ? Number(process.argv[portArg + 1]) : 8787;

async function build() {
  await esbuild.build({
    entryPoints: [path.join(root, "src/app.js")],
    bundle: true,
    format: "iife",
    platform: "browser",
    target: "es2020",
    outfile: path.join(root, "dist/app.js"),
    inject: [path.join(root, "src/buffer-shim.js")],
    define: { global: "globalThis", "process.env.NODE_ENV": '"production"' },
    logLevel: "warning",
  });
}

const files = {
  "/": ["index.html", "text/html; charset=utf-8"],
  "/index.html": ["index.html", "text/html; charset=utf-8"],
  "/dist/app.js": ["dist/app.js", "text/javascript; charset=utf-8"],
};

build().then(() => {
  http.createServer((req, res) => {
    const entry = files[new URL(req.url, "http://localhost").pathname];
    if (!entry) {
      res.writeHead(404).end("not found");
      return;
    }
    res.writeHead(200, { "content-type": entry[1], "cache-control": "no-store" });
    fs.createReadStream(path.join(root, entry[0])).pipe(res);
  }).listen(port, "127.0.0.1", () => console.log(`signing page: http://localhost:${port}`));
}).catch((error) => {
  console.error(`build failed: ${error.message}`);
  process.exitCode = 1;
});
