# Security Notes

## Repository Policy
- No private keys, seed phrases, service-role secrets, or private API tokens should be committed.
- Runtime values are injected from local config/env, not hardcoded in gameplay code.

## Config Injection Approach
- Unity runtime RPC override:
  - `Assets/StreamingAssets/Config.json` (local, gitignored)
  - or environment variable `CHAINBOUND_SOLANA_RPC_ENDPOINT`
- Supabase Edge Functions:
  - `SUPABASE_URL`
  - `SUPABASE_SERVICE_ROLE_KEY`
  - kept in env/secrets manager, never in source.

## Local Scanning
- Quick pattern scan:
  - `rg -n -S \"mnemonic|seed phrase|BEGIN PRIVATE KEY|service_role|api[_-]?key|password\\s*[:=]|secret\\s*[:=]\" Assets Supabase backend`
- Filename scan:
  - `rg --files -g '.env*' -g 'id.json' -g 'keypair.json' -g '*.pem' -g '*.p12' -g '*.keystore'`

## If A Secret Is Found
1. Revoke/rotate the credential immediately.
2. Remove it from current files and replace with env/config placeholders.
3. If committed in git history, run a history rewrite with `git filter-repo` and force-push.
