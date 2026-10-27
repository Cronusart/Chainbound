// Letters already drawn by the static Pixeloid Sans atlas, plus digits and the
// small set of specials used in game names. < and > stay out so a name cannot
// be a TextMesh Pro rich-text tag.
const DISPLAY_NAME_CHARSET =
  /^[A-Za-z0-9\u00C0-\u00D6\u00D8-\u00F6\u00F8-\u00FF](?:[A-Za-z0-9\u00C0-\u00D6\u00D8-\u00F6\u00F8-\u00FF '._-]*[A-Za-z0-9\u00C0-\u00D6\u00D8-\u00F6\u00F8-\u00FF])?$/u;

export function isDisplayNameCharsetValid(value: string): boolean {
  const name = value.trim();
  return name.length > 0 && !name.includes("  ") && DISPLAY_NAME_CHARSET.test(name);
}
