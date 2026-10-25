---
name: solana-mobile-routing
description: >-
  Chainbound / Signal Jam 3D router for Solana Mobile + Seeker skills installed under
  .claude/skills. Use when the task is Android toolchain, emulators, APK install, dApp Store
  publish, Mobile Wallet Adapter on device, Seeker Genesis Token, .skr names, Seeker Connect,
  or deciding Unity C# wallet vs Expo companion vs device CLI. Prefer this before copying
  Expo/React Native patterns into Unity.
---

# Solana Mobile routing (Chainbound)

Upstream CLI skills live in **`.claude/skills/`** (installed via `npx skills add`). This file does **not** replace them. Open the matching `SKILL.md` there and follow it.

**This game client is Unity**, not Expo. Do not paste `@wallet-ui/react-native-*` or Expo MWA snippets into `Assets/Scripts/**`. Use **`solana-unity`** for C# wallet/sign/RPC. Use the Mobile skills for **device, CLI, publish, Seeker, and any future Expo companion**.

## Default stack for a task

| Task | Open first | Then |
|---|---|---|
| Unity C# wallet, signing, Core inventory, Stamp/Known | `.claude/skills/solana-unity/SKILL.md` | Solana MCP `solana-mobile-docs` for MWA concepts |
| Unity UI, safe area, PSG1 / Seeker handheld controls | `.claude/skills/unity-mobile-psg1-ui/SKILL.md` | — |
| `npx solana-mobile` doctor, emu, device, localnet, playground, APK onto a phone | `.claude/skills/solana-mobile/SKILL.md` | `references/cli.md` for flags |
| Expo/RN companion app wallet connect (not Unity) | `.claude/skills/solana-mobile-wallet/SKILL.md` | kit vs web3.js from that skill |
| Sign/publish Android **APK** to Solana dApp Store | `.claude/skills/solana-mobile-publishing/SKILL.md` | Unity `assembleRelease` still APK-only |
| Seeker Genesis Token gate / anti-Sybil | `.claude/skills/seeker-genesis-token/SKILL.md` | server verification, not Unity-only trust |
| `.skr` names in profiles / history | `.claude/skills/seeker-domains/SKILL.md` | — |
| Web page **on Seeker browser** → built-in wallet | `.claude/skills/seeker-connect/SKILL.md` | not the Unity MWA path |
| Privy + MWA on Expo Android | `.claude/skills/integration-privy/SKILL.md` | not Unity |
| Anchor / Kit / program / Surfpool | `.claude/skills/solana-dev/SKILL.md` | `solana-agent-skills` topic index |

## Hard rules for this repo

1. **Mobile-first outputs** — Android/Seeker/PSG1 is the ship target. Prefer device-reachable localnet (`solana-mobile localnet`) over host-only RPC when testing wallet handoff.
2. **MWA needs a real Android build.** Expo Go will not work. Unity Editor Play Mode is not an MWA device; test connect on an emulator with a wallet APK or a Seeker/Play device.
3. **Wallet APK on emulator:** `npx solana-mobile@latest device install fakewallet` after `emu start --tune`. “Connect does nothing” on emu is usually a missing wallet app.
4. **Do not invent MWA APIs.** After picking a skill, use Solana MCP (`list_sections` → `solana-mobile-docs`) for current protocol details.
5. **dApp Store:** APK only (`assembleRelease`), never `.aab`. Keystore and publisher wallet stay out of git (publishing skill).
6. **Ownership spine unchanged.** Known is not an NFT; Stamp is Metaplex Core. Mobile skills do not authorize Bubblegum move mints.

## CLI entry (authoritative flags in the skill)

```bash
npx solana-mobile@latest --help
npx solana-mobile@latest doctor
```

Keep `@latest`. Do not guess flags; read `.claude/skills/solana-mobile/references/cli.md`.

## Progressive disclosure

1. Choose a row in the table above.
2. Read that `SKILL.md` only.
3. Load `references/` files only when the skill points at a specific file.
4. Announce which skill you opened and why (one line).
