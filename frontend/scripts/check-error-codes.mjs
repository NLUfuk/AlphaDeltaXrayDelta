// Every error code the API can throw must have Turkish text in the SPA's catalog.
//
// Why this exists: the catalog had drifted ~50 codes behind the backend, and the fallback for an
// unmapped code is the server's own `message` — a developer/log string written in English. So the
// failure mode was silent: no build error, no console warning, just an English sentence in front of a
// Turkish user, and only on the error path nobody clicks during testing. Grep is enough to catch it.
//
// Run: node scripts/check-error-codes.mjs   (wired into `npm run build`)
import { readFileSync, readdirSync, statSync } from 'node:fs'
import { join, dirname } from 'node:path'
import { fileURLToPath } from 'node:url'

const root = join(dirname(fileURLToPath(import.meta.url)), '..', '..')
const backend = join(root, 'src')
const catalogFile = join(root, 'frontend', 'src', 'lib', 'messages.ts')

function csFiles(dir) {
  return readdirSync(dir).flatMap((name) => {
    const path = join(dir, name)
    if (statSync(path).isDirectory()) return name === 'bin' || name === 'obj' ? [] : csFiles(path)
    return path.endsWith('.cs') ? [path] : []
  })
}

// `throw new SomethingException("code.name", "message")` — the only way a code reaches the client.
const thrown = new Set()
for (const file of csFiles(backend)) {
  const text = readFileSync(file, 'utf8')
  // No \b before "Exception" — it is a suffix ("UnauthorizedException"), so there is no word boundary.
  for (const m of text.matchAll(/Exception\(\s*"([a-z][a-z0-9_]*(?:\.[a-z][a-z0-9_]*)+)"/g)) thrown.add(m[1])
}

// A check that silently matches nothing passes forever. Fail loudly instead.
if (thrown.size === 0) {
  console.error(`Backend'de hic hata kodu bulunamadi (${backend}) — tarama bozuk.`)
  process.exit(1)
}

const catalog = readFileSync(catalogFile, 'utf8')
const mapped = new Set([...catalog.matchAll(/^\s*'([^']+)':\s*'/gm)].map((m) => m[1]))

const missing = [...thrown].filter((code) => !mapped.has(code)).sort()
if (missing.length > 0) {
  console.error(
    `\nBu hata kodlarinin Turkce karsiligi yok (frontend/src/lib/messages.ts -> ERRORS):\n` +
      missing.map((c) => `  - ${c}`).join('\n') +
      `\n\nEklenmezse kullaniciya sunucunun Ingilizce mesaji gosterilir.\n`,
  )
  process.exit(1)
}

console.log(`error codes ok (${thrown.size} kod, hepsi eslesmis)`)
