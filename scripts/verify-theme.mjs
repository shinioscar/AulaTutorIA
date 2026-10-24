import { readFileSync, readdirSync, mkdirSync, writeFileSync } from 'node:fs';
import { resolve, dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const source = join(root, 'src/AulaTutor.Web/src');
const tokenPath = join(source, 'styles/theme-tokens.css');
const css = readFileSync(tokenPath, 'utf8');
const darkStart = css.indexOf(":root[data-theme='dark']");
if (darkStart < 0) throw new Error('Falta la definición explícita del tema Dark');
const declarations = value => Object.fromEntries([...value.matchAll(/--([\w-]+)\s*:\s*([^;]+);/g)].map(m => [m[1], m[2].trim()]));
const light = declarations(css.slice(0, darkStart));
const dark = { ...light, ...declarations(css.slice(darkStart)) };
const luminance = hex => {
  const values = hex.slice(1).match(/.{2}/g).map(x => parseInt(x, 16) / 255).map(x => x <= .04045 ? x / 12.92 : ((x + .055) / 1.055) ** 2.4);
  return values[0] * .2126 + values[1] * .7152 + values[2] * .0722;
};
const color = (tokens, name, seen = new Set()) => {
  if (seen.has(name)) throw new Error(`Ciclo en token ${name}`);
  const value = tokens[name];
  if (/^#[\da-f]{6}$/i.test(value ?? '')) return value;
  const alias = /^var\(--([\w-]+)\)$/.exec(value ?? '');
  if (alias) return color(tokens, alias[1], new Set([...seen, name]));
  throw new Error(`Token de color inválido: ${name}`);
};
const pairs = [
  ['text-primary','background',4.5], ['text-primary','surface',4.5], ['text-primary','surface-secondary',4.5],
  ['text-secondary','background',4.5], ['text-secondary','surface',4.5], ['text-secondary','surface-secondary',4.5],
  ['primary','surface',4.5], ['primary','surface-secondary',4.5], ['primary','surface-hover',4.5],
  ['on-primary','primary',4.5], ['on-primary','primary-hover',4.5], ['on-primary','primary-active',4.5],
  ['secondary-text','surface',4.5], ['secondary-text','surface-secondary',4.5], ['accent','surface',4.5],
  ['success','success-surface',4.5], ['warning','warning-surface',4.5], ['warning','background',4.5],
  ['danger','danger-surface',4.5], ['on-danger','danger',4.5], ['on-danger','danger-hover',4.5], ['info','info-surface',4.5],
  ['text-primary','chat-tutor',4.5], ['text-primary','chat-student',4.5], ['text-secondary','chat-tutor',4.5], ['text-secondary','chat-student',4.5],
  ['code-text','code-background',4.5], ['code-inline-text','code-inline-background',4.5],
  ['focus','background',3], ['focus','surface',3], ['focus','surface-secondary',3], ['focus','surface-hover',3],
  ['control-border','surface',3], ['control-border','surface-secondary',3], ['progress-fill','progress-track',3],
  // UX-2 chart segments are decorative with a text table, but remain visible on their surface.
  ['chart-primary','surface',3], ['chart-accent','surface',3], ['success','surface',3],
  ['warning','surface',3], ['chart-primary','progress-track',3]
];
const results = [];
for (const [theme,tokens] of [['light',light],['dark',dark]]) {
  for (const [foreground,background,minimum] of pairs) {
    const a=luminance(color(tokens,foreground)), b=luminance(color(tokens,background));
    const ratio=(Math.max(a,b)+.05)/(Math.min(a,b)+.05);
    results.push({theme,foreground,background,minimum,ratio,passed:ratio>=minimum});
  }
}
const files = directory => readdirSync(directory,{withFileTypes:true}).flatMap(entry => entry.isDirectory() ? files(join(directory,entry.name)) : [join(directory,entry.name)]);
for (const path of files(source).filter(path => /\.(css|ts)$/.test(path))) {
  const text=readFileSync(path,'utf8');
  if (path !== tokenPath && /#[\da-f]{3,8}\b|\brgba?\(|\bhsla?\(/i.test(text)) throw new Error(`Color literal fuera de tokens: ${path}`);
  for (const match of text.matchAll(/var\(--([\w-]+)/g)) if (!(match[1] in light)) throw new Error(`Token no definido: ${match[1]}`);
}
const failures=results.filter(result=>!result.passed);
if (process.argv.includes('--report')) {
  const output=join(root,'artifacts/ui-0'); mkdirSync(output,{recursive:true});
  writeFileSync(join(output,'contrast.json'),JSON.stringify(results,null,2)+'\n');
}
if (failures.length) { for(const f of failures) console.error(`${f.theme}: ${f.foreground}/${f.background} = ${f.ratio.toFixed(3)} < ${f.minimum}`); process.exitCode=1; }
else console.log(`Tema: ${results.length} comprobaciones de contraste aprobadas; colores centralizados y referencias válidas.`);
