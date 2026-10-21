// Optional asset regeneration. Pass a checkout of the phosphor-icons CLI with its dependencies installed.
// Direct local reads avoid the CLI's package.json export-resolution fallback to a network download.
import { readFile, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { pathToFileURL, fileURLToPath } from 'node:url';

const checkout = process.argv[2];
if (!checkout) throw new Error('Usage: node tools/export-icons.mjs <phosphor-icons checkout>');
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const { applySvgOptions, svgToPngBuffer } = await import(pathToFileURL(path.join(checkout, 'dist/icon-service.js')));
for (const [name, target, size] of [
  ['tree-evergreen', 'src/Woodie/Resources/tree-evergreen-bold.png', 24],
  ['sliders-horizontal', 'src/Woodie/Resources/sliders-horizontal-bold.png', 24],
  ['stack', 'src/Woodie/Resources/stack-bold.png', 24],
  ['tree-evergreen', 'packaging/icon.png', 64],
]) {
  const source = await readFile(path.join(checkout, 'node_modules/@phosphor-icons/core/assets/bold', `${name}-bold.svg`), 'utf8');
  const svg = applySvgOptions(source, 'bold', '#875D3A', size);
  await writeFile(path.join(root, target), await svgToPngBuffer(svg, size));
  if (size === 24) await writeFile(path.join(root, 'src/Woodie/Resources', `${name}-bold.svg`), svg);
  console.log(target);
}
