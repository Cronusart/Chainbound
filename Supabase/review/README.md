# Supabase production review

> Profile-native cutover: sections that inspect `public.players` describe the
> historical pre-cutover schema only. The current invariant is documented in
> `profile-native-ownership-audit.md`.

Use this checklist before treating the hosted project as **production-ready** and aligned with the account refactor (`Docs/Architecture/Account_Platform_Refactor.md`, `Supabase/migrations/README.md`).

Saved queries in the **SQL Editor** are not migrations. Only objects applied through the **migration pipeline** (or reconciled into new migration files) are reproducible across environments.

---

## 1. Migration history (remote vs repo)

**Goal:** Every change you rely on is represented under `Supabase/migrations/` and recorded on the database.

### 1a. What Supabase has recorded

Run in **SQL Editor** (postgres role):

```sql
select version, name
from supabase_migrations.schema_migrations
order by version;
```

Compare `name` values to the timestamped files in `Supabase/migrations/`. See `Supabase/migrations/README.md` for the expected order. If a row is missing, the remote database never applied that migration through the CLI (or history was repaired manually).

If that relation errors (role, pooler, or project without the table), use **Supabase MCP `list_migrations`** (see §6) or the dashboard migration history instead.

### 1b. CLI (when linked)

From a machine with the project linked (`supabase link`):

```bash
supabase migration list
supabase db push --dry-run
```

Use the [Supabase CLI](https://supabase.com/docs/guides/cli) help for your installed version (`supabase migration --help`). Linking typically uses the **access token** from the dashboard, not the database user password.

---

## 2. Account model objects (post–refactor)

After **`20260505023400_account_identity_model.sql`**, the following should exist in **`public`** (names only; confirm columns in Table Editor or `information_schema`):

| Object | Purpose |
|--------|---------|
| `player_profiles` | Canonical profile (`player_profile_id`, …) |
| `profile_identities` | Provider identities linked to a profile |
| `profile_wallets` | Wallet rows linked to a profile |
| `entitlement_ledger` | Purchase / entitlement rows |
| `account_link_events` | Audit-style link events |

**Sanity query:**

```sql
select table_name
from information_schema.tables
where table_schema = 'public'
  and table_name in (
    'player_profiles',
    'profile_identities',
    'profile_wallets',
    'entitlement_ledger',
    'account_link_events',
    'players',
    'chainlings'
  )
order by table_name;
```

If `players` exists, confirm **`player_profile_id`** exists when `20260505023400_account_identity_model.sql` has been applied:

```sql
select column_name, data_type
from information_schema.columns
where table_schema = 'public'
  and table_name = 'players'
  and column_name = 'player_profile_id';
```

If `chainlings` exists, confirm **`player_profile_id`** when you rely on profile-scoped party data.

---

## 3. Schema drift (dashboard-only SQL)

**Problem:** DDL run only from saved SQL Editor snippets (for example `wallet_lower` on `players`) may **not** appear in `supabase_migrations.schema_migrations` and may **not** exist in git.

**Remediation:**

1. Inventory live-only objects (compare `pg_dump --schema-only` from remote to a fresh local apply of migrations, or use `supabase db diff` when the CLI is linked).
2. Add a **new** idempotent migration under `Supabase/migrations/` that contains the missing DDL (`CREATE OR REPLACE`, `IF NOT EXISTS`, guarded `DO` blocks).
3. Apply through your normal pipeline (`db push` / CI), not only by re-running the snippet.

---

## 4. Row Level Security (RLS)

Migration `20260505023400_account_identity_model.sql` enables **RLS** on: `player_profiles`, `profile_identities`, `profile_wallets`, `entitlement_ledger`, `account_link_events`.

**Check:**

```sql
select c.relname as table_name, c.relrowsecurity as rls_enabled
from pg_class c
join pg_namespace n on n.oid = c.relnamespace
where n.nspname = 'public'
  and c.relkind = 'r'
  and c.relname in (
    'player_profiles',
    'profile_identities',
    'profile_wallets',
    'entitlement_ledger',
    'account_link_events'
  )
order by 1;
```

Confirm **policies** match your threat model: clients should use **Edge Functions** with the **service role** on the server; avoid exposing service-role keys in the game client. Review `Docs/Architecture/Account_Platform_Refactor.md` (Security Defaults).

---

## 5. Edge Functions and secrets

Deploy the functions under `Supabase/functions/` so names match this repo. Current function entrypoints include:

**Account / profile (canonical model)**  
`account_start_guest`, `account_get_session`, `account_restore_identity`, `account_link_identity`, `account_link_wallet`, `account_unlink_wallet`, `profile_set_display_name`, `profile_set_intro_complete`, `profile_commit_starter`, `profile_get_party`, `entitlement_record`, `entitlement_validate_and_grant`

Wallet-only profile endpoints were retired in the 20260824 profile-native cutover.
Wallets are linked to an already authenticated profile.

**Required secrets (typical)**  
`SUPABASE_URL`, `SUPABASE_SERVICE_ROLE_KEY`

**Development-only toggles (must be false or unset in production unless explicitly intended)**  
`ALLOW_UNVERIFIED_WALLET_LINKS`, `ALLOW_UNVERIFIED_IDENTITY_LINKS`, `ALLOW_UNVALIDATED_ENTITLEMENT_GRANTS` (see `Supabase/functions/_shared/account_helpers.ts` and `entitlement_validate_and_grant`.)

In the dashboard: **Project Settings → Edge Functions → Secrets**.

---

## 6. Automated audit via Supabase MCP (Cursor)

**Yes — most of this review can be driven through the official Supabase MCP** once **`mcp_auth`** has succeeded for **`user-supabase`** in Cursor. An agent (or you) can call tools in a fixed order; humans still interpret results and approve production.

### 6a. What the MCP can automate

| Step | MCP tool | What it does |
|------|-----------|----------------|
| Migration tracking (hosted) | **`list_migrations`** | Lists migrations registered with the Supabase project (Management API). **May be empty** if schema was built only from SQL Editor / external dumps and never through `apply_migration` or linked CLI history. |
| Table inventory + RLS flag | **`list_tables`** | Lists `public` tables with `rls_enabled` and row counts. Compare to §2 expected tables. |
| Custom checks | **`execute_sql`** | Run the §2 / §4 `information_schema` / `pg_catalog` queries (expected tables, `player_profile_id`, RLS on account tables). **Do not** use this for ad-hoc DDL in production; use **`apply_migration`** for DDL. |
| Edge Function deploy parity | **`list_edge_functions`** | Compare returned `slug` list to the folders under `Supabase/functions/` in git. |
| Security / performance lint | **`get_advisors`** | Call with `type: "security"` and `type: "performance"`. Surfaces RLS gaps, missing policies, `search_path` on functions, etc. Each item includes a **remediation** URL. |
| Optional deep read | **`get_edge_function`**, **`get_logs`**, **`search_docs`** | Inspect a deployed function body, logs, or docs links. |

Other exposed tools (branching, **`apply_migration`**, **`deploy_edge_function`**, type generation) support **remediation**, not only read-only audit.

### 6b. Suggested automated runbook (copy for an agent prompt)

1. Authenticate: **`mcp_auth`** on server **`user-supabase`** (`{}`).
2. **`list_migrations`** — note count vs git `Supabase/migrations/*.sql`.
3. **`list_tables`** — assert presence of tables from §2 after `20260505023400_account_identity_model.sql`; note missing `player_profiles` / `profile_*` / `entitlement_ledger` / `account_link_events` as **refactor gap**.
4. **`execute_sql`** — run §2 sanity query and `players.player_profile_id` / `chainlings.player_profile_id` checks.
5. **`list_edge_functions`** — assert every slug required by `Account_Platform_Refactor.md` and this repo exists (account + profile + entitlement + legacy `player_*`).
6. **`get_advisors`** — `security` then `performance`; triage ERROR before WARN/INFO.

### 6c. What MCP does *not* replace

- **Secret values** and dashboard-only toggles — verify in **Project Settings → Edge Functions → Secrets** (MCP does not substitute a secrets audit).
- **“Production-ready” sign-off** — still human judgment after advisors and drift review.
- **`supabase_migrations.schema_migrations`** via **`execute_sql`** may fail depending on role or database; prefer **`list_migrations`** when available.

Official overview: [Supabase MCP](https://supabase.com/docs/guides/getting-started/mcp).

---

## 7. Sign-off

Before launch, explicitly confirm:

- [ ] Hosted migration history matches git (`schema_migrations` SQL and/or MCP **`list_migrations`**, timestamped chain in `Supabase/migrations/README.md` minimum for account layer).
- [ ] No critical DDL exists only in SQL Editor saves.
- [ ] Edge Functions deployed and secrets set; dev-only env flags off in production.
- [ ] RLS enabled on new account tables; access path documented (Edge Functions + service role).

For runtime architecture, keep `Docs/Architecture/Account_Platform_Refactor.md` as the source of truth for Unity and provider TODOs.

---

## Profile-native ownership

For the canonical ownership invariants and retired endpoints, see
[`profile-native-ownership-audit.md`](profile-native-ownership-audit.md).
