// Dark-mode toggle: flips .dark on <html> (index.css re-maps every color token under it) and remembers
// the choice. initTheme() runs before React renders (main.tsx) so there is no light flash on load.
const KEY = 'crm.theme'

export function initTheme(): void {
  const saved = localStorage.getItem(KEY)
  const dark = saved ? saved === 'dark' : window.matchMedia('(prefers-color-scheme: dark)').matches
  document.documentElement.classList.toggle('dark', dark)
}

export function isDark(): boolean {
  return document.documentElement.classList.contains('dark')
}

export function toggleTheme(): boolean {
  const dark = !isDark()
  document.documentElement.classList.toggle('dark', dark)
  localStorage.setItem(KEY, dark ? 'dark' : 'light')
  return dark
}
