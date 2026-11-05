// Copies offline copies of third-party assets (icons, fonts) into wwwroot so the app never needs the network.
import { cpSync, mkdirSync } from "node:fs";

const copies = [
  ["node_modules/@fortawesome/fontawesome-free/css/all.min.css", "wwwroot/lib/fontawesome/css/all.min.css"],
  ["node_modules/@fortawesome/fontawesome-free/webfonts/fa-solid-900.woff2", "wwwroot/lib/fontawesome/webfonts/fa-solid-900.woff2"],
  ["node_modules/@fortawesome/fontawesome-free/webfonts/fa-regular-400.woff2", "wwwroot/lib/fontawesome/webfonts/fa-regular-400.woff2"],
  ["node_modules/@fortawesome/fontawesome-free/webfonts/fa-brands-400.woff2", "wwwroot/lib/fontawesome/webfonts/fa-brands-400.woff2"],
  ["node_modules/@fontsource-variable/bricolage-grotesque/files/bricolage-grotesque-latin-wght-normal.woff2", "wwwroot/fonts/bricolage-grotesque-latin-wght-normal.woff2"],
  ["node_modules/@fontsource-variable/dm-sans/files/dm-sans-latin-wght-normal.woff2", "wwwroot/fonts/dm-sans-latin-wght-normal.woff2"],
];

for (const [from, to] of copies) {
  mkdirSync(to.substring(0, to.lastIndexOf("/")), { recursive: true });
  cpSync(from, to);
}
