---
name: solana-agent-skills
description: >-
  Index and workflow for Solana Foundation–maintained agent skills (tooling, tokens,
  frontend, payments, security, testing). Use when the user mentions Solana Agent Skills,
  `npx skills add`, solana-dev-skill, Anchor/Rust/Node version matrix, Codama/IDL,
  @solana/kit vs web3.js, Token-2022 confidential transfers, Commerce Kit / Solana Pay,
  program security review, LiteSVM/Mollusk/Surfpool testing, or RPC/GLIBC/Anchor errors
  and wants authoritative upstream guidance beyond this repo.
---

# Solana Agent Skills (Signal Jam 3D)

This project skill does **not** duplicate the Foundation corpus. It tells the agent **when** to rely on the official bundle and **which topical area** applies.

## Install the upstream bundle

Foundation-maintained skills for agents building on Solana:

```bash
npx skills add https://github.com/solana-foundation/solana-dev-skill
```

After install, open the added skill files from your skills directory (personal or project, per your `npx skills` setup) and follow them for full procedures, checklists, and links.

## How to use this skill in Cursor

1. If the task matches a **topic row** below, state that you are using **solana-agent-skills** and the matching topic.
2. If the user’s machine has the bundle installed, read the **corresponding** upstream skill file(s) for step-by-step content; do not invent version numbers or security rules from memory.
3. If the bundle is **not** installed, give the install command above and continue with best-effort help, or ask the user to install for authoritative matrices and checklists.

## Topic index (route requests here)

| Topic | Category | Use when the user is dealing with… |
| --- | --- | --- |
| **Common Errors & Solutions** | Reference | GLIBC issues, Anchor version conflicts, RPC errors, environment/tooling failures. |
| **Version Compatibility Matrix** | Reference | Matching Anchor, Solana CLI, Rust, and Node.js to avoid toolchain drift. |
| **Confidential Transfers** | Tokens | Token-2022 extension for private/encrypted token balances. |
| **Frontend with framework-kit** | Frontend | React/Next.js apps, Wallet Standard–first connection, single client instance, small client footprint. |
| **IDL & Client Code Generation** | Tooling | Codama and type-safe clients from IDLs (avoid hand-maintained serializers). |
| **Kit ↔ web3.js Interop** | Tooling | Bridging `@solana/kit` and legacy `@solana/web3.js` during incremental migration. |
| **Payments & Commerce** | Payments | Checkout, payment buttons, QR payment requests (Commerce Kit, Solana Pay). |
| **Curated Resources** | Reference | Official docs, learning platforms, tooling references, community links. |
| **Security Checklist** | Security | Pre-deploy review—account validation, signers, common attack vectors (program + client). |
| **Testing Strategy** | Testing | Pyramid—LiteSVM (fast unit tests), Mollusk (isolated instruction checks), Surfpool (integration / realistic state). |

## Signal Jam 3D coordination

- **Unity / C# game client** (wallet, Solana.Unity-SDK, in-game txs): prefer the project’s **`solana-unity`** skill when present (`AGENTS.md`); use this skill for **web stacks, programs, and shared Solana concepts** that the Foundation bundle covers in depth.
- **Android / Seeker / dApp Store / `npx solana-mobile`:** the Solana Mobile skill pack is already installed at **`.claude/skills/solana-mobile*`** (plus Seeker / Privy). Route via **`.claude/skills/solana-mobile-routing/SKILL.md`**. That pack is **not** the Foundation `solana-dev-skill` URL below.
- **Installed Foundation playbook copy:** `.claude/skills/solana-dev/SKILL.md` (mirrored from `.agents/skills/solana-dev`).
- **Repo scripts and placement**: still follow `CLAUDE.md (Project structure)` and existing `Assets/Scripts/Solana/**` patterns when editing this game’s code.

## Maintenance

When the Foundation repo’s skill list or install URL changes, update the **Install** section and **Topic index** table to match the upstream README.
