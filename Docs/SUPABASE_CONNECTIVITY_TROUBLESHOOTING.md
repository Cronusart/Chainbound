# Supabase Connectivity Troubleshooting

## "Cannot resolve destination host" in Unity

If you see this error in the Unity console when connecting to Supabase:

```
[SupabaseClient] Network error calling account_start_guest: Cannot resolve destination host
```

**Supabase is reachable** — the endpoint is up and the config is correct. The failure is DNS resolution happening inside Unity's runtime.

### What we verified

- **Endpoint**: `https://jhzqcbrzkausrrtcavuu.supabase.co/functions/v1/account_start_guest` is reachable
- **Edge function**: `account_start_guest` is deployed and ACTIVE
- **Config**: URL and anon key match the dashboard
- **System DNS**: `nslookup` resolves the host from your machine

### Likely causes

1. **Unity Editor startup timing** — DNS may resolve after the first request. Unity now retries automatically (see `SupabaseConfig.maxRetries`).
2. **Firewall / antivirus** — Blocking Unity.exe from making outbound HTTPS. Add an exception for Unity.
3. **VPN** — Some VPNs break DNS for Unity. Try disconnecting.
4. **IPv6 / network stack** — Unity occasionally has issues with IPv6. Try disabling IPv6 on your network adapter or using a different network.

### Quick fixes

1. **Retry** — Wait a few seconds and connect again. The retry logic (default 2 retries) may succeed on a subsequent attempt.
2. **Restart Unity** — Sometimes the network stack needs a fresh start.
3. **Run Unity as administrator** — Only if the above fails; can help with permission-related network issues.

### Config

- **maxRetries** (SupabaseConfig): Number of retries for transient DNS/connection failures. Default: 2 (3 total attempts).
