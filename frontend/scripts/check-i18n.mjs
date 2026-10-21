// Reports translation keys that exist in one language but not in all of de, en and tr, and empty texts.
// Usage: npm run i18n:check
import { readdirSync, readFileSync, statSync } from 'node:fs';
import { join, relative } from 'node:path';

const ROOT = new URL('../public/i18n', import.meta.url).pathname;
const LANGUAGES = ['de', 'en', 'tr'];

function flatten(object, prefix = '') {
  return Object.entries(object).flatMap(([key, value]) =>
    typeof value === 'object' && value !== null
      ? flatten(value, `${prefix}${key}.`)
      : [[`${prefix}${key}`, value]],
  );
}

function directories(dir) {
  const nested = readdirSync(dir)
    .map((name) => join(dir, name))
    .filter((path) => statSync(path).isDirectory())
    .flatMap(directories);
  return [dir, ...nested];
}

const problems = [];
for (const dir of directories(ROOT)) {
  const label = relative(ROOT, dir) || '(shared)';
  const texts = {};
  for (const language of LANGUAGES) {
    try {
      texts[language] = Object.fromEntries(
        flatten(JSON.parse(readFileSync(join(dir, `${language}.json`), 'utf8'))),
      );
    } catch (error) {
      problems.push(`${label}: ${language}.json cannot be read (${error.code ?? error.message})`);
      texts[language] = {};
    }
  }

  const allKeys = new Set(LANGUAGES.flatMap((language) => Object.keys(texts[language])));
  for (const key of [...allKeys].sort()) {
    for (const language of LANGUAGES) {
      if (!(key in texts[language])) problems.push(`${label}: "${key}" is missing in ${language}`);
      else if (String(texts[language][key]).trim() === '')
        problems.push(`${label}: "${key}" is empty in ${language}`);
    }
  }
}

if (problems.length > 0) {
  console.error(problems.join('\n'));
  process.exit(1);
}
console.log(`Translations consistent for ${LANGUAGES.join(', ')}.`);
