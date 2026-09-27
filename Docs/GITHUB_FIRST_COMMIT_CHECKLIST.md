# GitHub first-commit checklist (Chainbound / Signal Jam 3D)

Use this before the first public push for hackathon judges. Do **not** push until an empty GitHub repo exists and you have reviewed `git status`.

## Goal

Ship a clean monorepo: Unity client + Supabase + optional Anchor backend. Omit generated Unity junk, secrets, local venvs, and scratch ops files.

## Before you commit

1. Confirm GitHub account is `Cronusart` (or the org you intend).
2. Create an **empty** public repo on GitHub (no README/license if you want a clean first push), e.g. `chainbound` or `signal-jam-3d`.
3. From the project root in PowerShell or Git Bash:
   - `git status`
   - `git check-ignore -v Library Builds .venv-library-card .env mcp.json Quarantine`
4. Spot-check that nothing secret appears in `git status` (no `.env`, `id.json`, keypairs, real `Config.json`, service-role keys).

## Include (should appear as untracked / to be added)

- `Assets/` (game content; runtime `Config.json` stays ignored)
- `Packages/`
- `ProjectSettings/`
- `Supabase/` (edge functions + SQL migrations)
- `backend/` (Anchor workspace; `target/` ignored)
- `Docs/` (curated docs; some internal handoff paths stay ignored)
- `tools/` (library-card renderer and helpers)
- `README.md`, `SECURITY_NOTES.md`, `.gitignore`, `.env.example`
- Root `package.json`, lockfiles, `deno.lock` if used for Edge tooling
- `Assets/StreamingAssets/Config.sample.json` (sample only)

## Exclude (must stay out)

### Already covered by `.gitignore`
- Unity: `Library/`, `Temp/`, `Obj/`, `Build/`, `Builds/`, `Logs/`, `UserSettings/`, `MemoryCaptures/`, `Recordings/`
- IDE: `.vs/`, `.idea/`, most `.vscode/` (optional `settings.json` allowed)
- JS: `node_modules/`, common build caches
- Rust: `backend/chainbound_game/target/`, `test-ledger/`, nested `.git/` / `.anchor/`
- Secrets: `.env*`, Solana `id.json` / `keypair.json`, certs/keystores, real `Config.json`
- Internal: `.cursor/`, `AGENTS.md`, `mcp.json`, selected Docs handoff/audit notes

### Added for hackathon-safe first push
- `.venv-library-card/` and other local Python venvs
- `.tmp-gate-*`, `.tmp-readlogs*`, `Temp_*.log` scratch ops files
- `.agents/`, `.utmp/` local agent/sandbox dirs
- `Quarantine/`, `Signal%20Jam/` duplicate/dead trees
- `*.apk` / `*.aab` / `*.ipa` if they appear outside `Builds/`

## Suggested first commit commands

```bash
cd "/path/to/Signal Jam 3D"
git add .gitignore README.md SECURITY_NOTES.md .env.example
git add Assets Packages ProjectSettings Supabase backend Docs tools
git add package.json package-lock.json deno.lock skills-lock.json
# Review:
git status
# Then:
git commit -m "Initial public hackathon snapshot: Unity client, Supabase, Anchor workspace"
git branch -M main
git remote add origin https://github.com/Cronusart/<REPO>.git
git push -u origin main
```

If `git add Assets` pulls surprising large binaries, stop and extend `.gitignore` before committing.

## After push

1. Paste the repo URL into Cursor / Grok Bot so cloud agents can see it (may need Cursor GitHub app access to the new repo).
2. Open the GitHub web UI once and confirm:
   - No `Library/` or `Builds/`
   - No `.env`
   - README renders
3. Optional: add a short `Docs/HACKATHON.md` judge path (main menu scene + Ownership Spine status) in a follow-up commit.

## Do not do on night one

- Force-push or history rewrite
- Commit wallet keypairs or Supabase service-role keys
- Bulk-rename Unity folders (breaks asset GUIDs)
- Squash/delete remote Supabase migration history from this checklist

## Ownership Spine note for judges (optional blurb)

Devnet Ownership Spine stamps Core assets whose metadata URI points at the public Supabase bucket `chainbound-ownership-metadata`. Library-card PNG + optional scanline GIF live under `stamped/`. Candy machine / settlement hardening may still be in progress; Unity ownership flags stay off for shipping builds unless explicitly enabled for Devnet demos.
