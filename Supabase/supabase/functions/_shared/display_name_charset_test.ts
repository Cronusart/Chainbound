import { assertEquals } from "https://deno.land/std@0.224.0/assert/mod.ts";
import { isDisplayNameCharsetValid } from "./display_name_charset.ts";

Deno.test("display names accept digits and the allowed specials", () => {
  assertEquals(isDisplayNameCharsetValid("Nyx_2"), true);
  assertEquals(isDisplayNameCharsetValid("A.B"), true);
  assertEquals(isDisplayNameCharsetValid("O'Byte"), true);
  assertEquals(isDisplayNameCharsetValid("Signal-Fox"), true);
  assertEquals(isDisplayNameCharsetValid("A__B"), true);
  assertEquals(isDisplayNameCharsetValid("José"), true);
});

Deno.test("display names reject markup, trailing specials, and other glyphs", () => {
  assertEquals(isDisplayNameCharsetValid("<size>"), false);
  assertEquals(isDisplayNameCharsetValid("<size=40>Nyx"), false);
  assertEquals(isDisplayNameCharsetValid("Nyx."), false);
  assertEquals(isDisplayNameCharsetValid("Nyx_"), false);
  assertEquals(isDisplayNameCharsetValid("A  B"), false);
  assertEquals(isDisplayNameCharsetValid(".Nyx"), false);
  assertEquals(isDisplayNameCharsetValid("Nyx/2"), false);
});
