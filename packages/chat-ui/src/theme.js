// The design tokens of spec/chat-ui.json as custom properties on the chat's root: chat.css reads
// only these, so the web chat and D3sk.ChatUi.Avalonia's ChatTheme paint from the same numbers.
import spec from "./generated/spec.js";

const T = spec.tokens;
const kebab = (name) => name.replace(/[A-Z0-9]+/g, (match) => `-${match.toLowerCase()}`);

/** Palette ids in the order Settings lists them. */
export const PALETTES = Object.freeze([...T.palettes.order]);

/** { name: value } for one palette: chrome colours, action colours, sizes and radii. */
export function themeProperties(paletteId) {
  const palette = T.palettes.values[paletteId] || T.palettes.values[T.palettes.default];
  const props = {};
  for (const [name, value] of Object.entries(palette)) props[`--${kebab(name)}`] = value;
  for (const [name, value] of Object.entries(T.actions)) props[`--${kebab(name)}`] = value;
  const type = T.typography;
  props["--font-small"] = `${type.small}px`;
  props["--font-body"] = `${type.body}px`;
  props["--font-heading"] = `${type.heading}px`;
  props["--font-code"] = `${type.code}px`;
  props["--line-height"] = String(type.lineHeight);
  props["--font-family"] = type.fontFamily;
  props["--mono-family"] = type.monoFamily;
  for (const [name, value] of Object.entries(T.radius)) props[`--radius-${kebab(name)}`] = `${value}px`;
  for (const [name, value] of Object.entries(T.spacing)) props[`--${kebab(name)}`] = `${value}px`;
  return props;
}

/** Sets a palette on `element` (call again to switch). An unknown id gives the default. */
export function applyTheme(element, paletteId) {
  const id = T.palettes.values[paletteId] ? paletteId : T.palettes.default;
  for (const [name, value] of Object.entries(themeProperties(id))) element.style.setProperty(name, value);
  element.dataset.theme = id;
  return id;
}

/** The colour of a brand's working mark. */
export function brandColor(brand) {
  return T.brands[brand] || T.brands.d3skhand;
}
