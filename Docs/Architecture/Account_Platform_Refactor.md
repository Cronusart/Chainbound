# Account and Platform Refactor

Signal Jam uses one profile-native account and gameplay ownership model. The
server-issued `player_profile_id` is canonical; wallets and platform identities
are optional links to that profile.

## Runtime Model

- `player_profile_id` is the canonical game profile id.
- Wallet public keys are optional wallet identities, not account primary keys.
- Google Play, Apple, Seeker, guest, and editor mock identities are represented
  through provider interfaces.
- `PlatformContext` is the single runtime source for build/platform capability
  checks. Gameplay and menu code should not branch on scattered platform defines.
  The shipping Android target is the Solana dApp Store (Seeker): `GameServices`
  carries `PlatformBuildProfile_SolanaDappStore`, and Android Player Settings
  define `SIGNALJAM_BUILD_SOLANA_DAPP_STORE` so inference matches if the profile
  is missing.
- Restore Profile is one button. `AccountRestorePolicy` picks the mechanism:
  Seeker always proves wallet ownership (`wallet_restore_profile`); Play/Apple
  resume a platform identity (`account_restore_identity`) once those SDKs are
  wired. Google Play Games is intentionally not used on the Seeker APK.
- `GameSessionContext` carries canonical account metadata plus optional linked
  wallet data for intro/combat scene consumers.

## Main Menu Flow

`MainMenuSequenceController2` remains the visual/animation controller.
`MainMenuFlowOrchestrator` owns account actions:

- Continue as guest.
- Restore profile (wallet proof on Seeker; platform identity on Play/Apple).
- Connect wallet.
- Link connected wallet to the current profile.

All guest, recoverable identity, and wallet-link flows use
`AccountSessionService` and `IPlayerProfileGateway`. Wallet-only profile lookup
is intentionally unsupported: authenticate a profile first, then link or restore
the wallet. The Bonk Dog Restore button on Seeker is that explicit restore: it
opens MWA, signs a challenge, and moves this install onto the profile already
bound to the wallet. It does not merge guest progress.

## Backend Model

Migration `20260505023400_account_identity_model.sql` adds:

- `player_profiles`
- `profile_identities`
- `profile_wallets`
- `entitlement_ledger`
- `account_link_events`

Migrations `20260824010000` through `20260824020000` complete the profile-native
cutover. `chainlings`, move inventory, lifetime stats, sessions, achievements,
vending tickets, and upgrade events are owned directly by `player_profile_id`.
The pre-release `players` table and `legacy_player_id` bridge no longer exist.

## Security Defaults

- Client code calls Edge Functions only.
- Service-role key remains server-side.
- Wallet link requires explicit consent and is rejected unless wallet proof
  verification is configured or `ALLOW_UNVERIFIED_WALLET_LINKS=true` is set for
  development.
- Google/Apple identity link is rejected unless server-side validation is wired
  or `ALLOW_UNVERIFIED_IDENTITY_LINKS=true` is set for development.

## SDK Wiring TODOs

- Google Play Games sign-in token provider.
- Sign in with Apple token provider.
- Play Billing validator.
- App Store receipt validator.
- Seeker / Seed Vault wallet provider and signature verification. **Client path
  is live** via `SolanaWalletAdapterProvider` → MWA. The project-owned identity
  URI and relative icon are configured and pass the Android release validator.
  Remaining work is the physical-Seeker authorization/message-signing matrix and
  production server-side verification (devnet currently uses the wallet-restore
  proof in `wallet_restore_profile`).
- External iOS wallet/deeplink provider.
- Rewarded ad provider and validation callback.
