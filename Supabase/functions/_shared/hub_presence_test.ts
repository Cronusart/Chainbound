import { assertEquals } from "https://deno.land/std@0.224.0/assert/mod.ts";

Deno.test("hub migration gates enqueue and keeps tables service-role only", async () => {
  const source = await Deno.readTextFile(new URL("../../migrations/20261008073633_hub_presence_foundations.sql", import.meta.url));
  assertEquals(source.includes("CREATE TABLE IF NOT EXISTS public.hub_identities"), true);
  assertEquals(source.includes("ENABLE ROW LEVEL SECURITY"), true);
  assertEquals(source.includes("REVOKE ALL ON TABLE public.hub_identities FROM PUBLIC, anon, authenticated"), true);
  assertEquals(source.includes("GRANT ALL ON TABLE public.hub_identities TO service_role"), true);
  assertEquals(source.includes("v_gate := public.hub_enqueue_allowed(p_profile_id)"), true);
  assertEquals(source.includes("'error', v_gate"), true);
  assertEquals(source.includes("dev_exempt_enabled"), true);
  assertEquals(source.includes("SET search_path = ''"), true);
});

Deno.test("hub h0.1 migration locks defaults and hard-gates dev exemption", async () => {
  const dir = new URL("../../migrations/", import.meta.url);
  let found = "";
  for await (const entry of Deno.readDir(dir)) {
    if (!entry.isFile || !entry.name.endsWith(".sql")) continue;
    const text = await Deno.readTextFile(new URL(entry.name, dir));
    if (text.includes("hub_settings_cluster_known")) found = text;
  }
  assertEquals(found.includes("cluster IN ('', 'devnet')"), true);
  assertEquals(found.includes("multiplayer_unlocked = false"), true);
  assertEquals(found.includes("dev_exempt_enabled = false"), true);
  assertEquals(found.includes("v_cluster = 'devnet'"), true);
  assertEquals(found.includes("identity_in_use"), true);
  assertEquals(found.includes("SET search_path = ''"), true);
  assertEquals(found.includes("GRANT EXECUTE ON FUNCTION public.hub_enqueue_allowed(uuid) TO service_role"), true);
  assertEquals(found.includes("REVOKE ALL ON FUNCTION public.hub_link_identity_service(uuid, uuid, text) FROM PUBLIC, anon, authenticated"), true);
});
