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
