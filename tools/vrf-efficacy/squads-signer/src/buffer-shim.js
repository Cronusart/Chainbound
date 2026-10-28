// Gives bundled Node-style code (Squads SDK -> beet -> assert -> util) the Buffer and process
// globals it expects in the browser.
export { Buffer } from "buffer";

export const process = {
  env: { NODE_ENV: "production" },
  browser: true,
  version: "",
  versions: {},
  nextTick: (fn, ...args) => queueMicrotask(() => fn(...args)),
};
