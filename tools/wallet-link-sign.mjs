import fs from "fs";
import path from "path";
import { createRequire } from "module";
import { fileURLToPath } from "url";

const root = path.join(path.dirname(fileURLToPath(import.meta.url)), "..");
const require = createRequire(path.join(root, "package.json"));

const keypairPath = process.argv[2];
const messagePath = process.argv[3];
if (!keypairPath || !messagePath) {
  console.error("Usage: node tools/wallet-link-sign.mjs <keypair.json> <message.txt>");
  process.exit(1);
}

const nacl = require("tweetnacl");
const bs58mod = require("bs58");
const encode = typeof bs58mod.encode === "function" ? bs58mod.encode : bs58mod.default.encode;

const secret = Uint8Array.from(JSON.parse(fs.readFileSync(keypairPath, "utf8")));
if (secret.length !== 64) {
  console.error(`Expected 64-byte secret key, got ${secret.length}`);
  process.exit(1);
}
const msg = new Uint8Array(fs.readFileSync(messagePath));
const sig = nacl.sign.detached(msg, secret);
process.stdout.write(encode(sig));
