// Themes: a name on <html data-theme>, and index.css re-maps the same colour tokens under it. That is
// the whole mechanism — no JS runs while you use the app, nothing re-renders, no palette is computed.
// A theme costs one block of CSS variables (~15 lines), which is why adding four of them does not make
// the app any slower: the browser resolves custom properties during the paint it was doing anyway.
//
// `.dark` is still toggled on <html> for the dark-family themes, because `color-scheme` (native form
// controls, scrollbars) and Tailwind's `dark:` variant read it.
const KEY = 'crm.theme'

export const THEMES = [
  { id: 'light', label: 'Açık', dark: false },
  { id: 'dark', label: 'Koyu', dark: true },
  { id: 'midnight', label: 'Gece mavisi', dark: true },
  { id: 'sand', label: 'Sıcak kum', dark: false },
  { id: 'forest', label: 'Zümrüt', dark: false },
] as const

export type ThemeId = (typeof THEMES)[number]['id']

function isKnown(value: string | null): value is ThemeId {
  return THEMES.some((t) => t.id === value)
}

export function applyTheme(id: ThemeId): void {
  const theme = THEMES.find((t) => t.id === id) ?? THEMES[0]
  document.documentElement.dataset.theme = theme.id
  document.documentElement.classList.toggle('dark', theme.dark)
  localStorage.setItem(KEY, theme.id)
}

/** Runs before React renders (main.tsx) so there is no flash of the wrong palette. The stored value is
 *  the theme id; 'light'/'dark' were the only two before, and both are still valid ids, so nothing has
 *  to be migrated. Unknown/absent falls back to the system preference. */
export function initTheme(): void {
  const saved = localStorage.getItem(KEY)
  if (isKnown(saved)) return applyTheme(saved)
  applyTheme(window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light')
}

export function currentTheme(): ThemeId {
  const id = document.documentElement.dataset.theme ?? null
  return isKnown(id) ? id : 'light'
}
