// Whose mark spins under a reply being written (hub transcript.js workingMark): small original
// drawings that evoke each assistant, not their trademarks. Claude's ✻ cycle comes from CSS.
import { brandColor } from "../theme.js";

const SVG = "http://www.w3.org/2000/svg";
let gradients = 0;

function svgNode(doc, tag, attributes, children = []) {
  const node = doc.createElementNS(SVG, tag);
  for (const [key, value] of Object.entries(attributes)) node.setAttribute(key, String(value));
  node.append(...children);
  return node;
}

/** The working glyph for a brand (claude, gemini, openai, grok, copilot, mlx, colibri, cursor, d3skhand). */
export function workingMark(doc, brand) {
  const holder = doc.createElement("span");
  holder.className = `working-glyph brand-${brand}`;
  holder.style.setProperty("--brand-color", brandColor(brand));
  if (brand === "claude") return holder;
  const s = (attributes, children) => svgNode(doc, attributes[0], attributes[1], children);
  const stroke = { fill: "none", stroke: "currentColor", "stroke-width": 1.8, "stroke-linecap": "round", "stroke-linejoin": "round" };
  let shapes;
  if (brand === "gemini") {
    const id = `d3sk-gemini-gradient-${(gradients += 1)}`;
    shapes = [
      s(["defs", {}], [s(["linearGradient", { id, x1: 0, y1: 1, x2: 1, y2: 0 }], [
        s(["stop", { offset: "0%", "stop-color": "#4285f4" }]),
        s(["stop", { offset: "55%", "stop-color": "#9b72cb" }]),
        s(["stop", { offset: "100%", "stop-color": "#d96570" }]),
      ])]),
      s(["path", { fill: `url(#${id})`, d: "M12 1.5C12 8.4 15.6 12 22.5 12 15.6 12 12 15.6 12 22.5 12 15.6 8.4 12 1.5 12 8.4 12 12 8.4 12 1.5Z" }]),
    ];
  } else if (brand === "openai") {
    shapes = [0, 60, 120, 180, 240, 300].map((angle) =>
      s(["ellipse", { ...stroke, "stroke-width": 1.5, cx: 12, cy: 7.4, rx: 3.3, ry: 5.2, transform: `rotate(${angle} 12 12)` }]));
  } else if (brand === "grok") {
    shapes = [s(["circle", { ...stroke, cx: 12, cy: 12, r: 7.5 }]), s(["path", { ...stroke, d: "M4 20 20 4" }])];
  } else if (brand === "copilot") {
    shapes = [
      s(["rect", { ...stroke, x: 2.5, y: 6.5, width: 19, height: 12, rx: 6 }]),
      s(["ellipse", { class: "mark-eye", fill: "currentColor", cx: 9, cy: 12.5, rx: 1.4, ry: 2.2 }]),
      s(["ellipse", { class: "mark-eye", fill: "currentColor", cx: 15, cy: 12.5, rx: 1.4, ry: 2.2 }]),
    ];
  } else if (brand === "mlx") {
    shapes = [s(["rect", { ...stroke, x: 3.5, y: 3.5, width: 17, height: 17, rx: 3 }]), s(["path", { ...stroke, d: "M8 16V8.5L12 12l4-3.5V16" }])];
  } else if (brand === "colibri") {
    shapes = [
      s(["rect", { ...stroke, x: 3.5, y: 3.5, width: 17, height: 17, rx: 3 }]),
      s(["path", { ...stroke, d: "M8 15c0-3.3 2-5.5 4.5-5.5 1.4 0 2.5.7 3 1.8L20.5 9.5 16 13c-.6 1.8-2.3 3-4.3 3H8Z" }]),
    ];
  } else if (brand === "cursor") {
    shapes = [
      s(["path", { ...stroke, "stroke-width": 1.6, d: "M12 2.5 20.5 7.25v9.5L12 21.5 3.5 16.75v-9.5Z" }]),
      s(["path", { ...stroke, "stroke-width": 1.6, d: "M12 12v9.5M12 12 3.5 7.25M12 12l8.5-4.75" }]),
    ];
  } else {
    shapes = [s(["path", { fill: "currentColor", d: "M7.5 4.6c0-1.2 1.3-1.9 2.3-1.3l10.6 7.4c.9.6.9 2 0 2.6L9.8 20.7c-1 .6-2.3-.1-2.3-1.3Z" }])];
  }
  holder.append(svgNode(doc, "svg", { viewBox: "0 0 24 24", width: 16, height: 16, "aria-hidden": "true", focusable: "false" }, shapes));
  return holder;
}
