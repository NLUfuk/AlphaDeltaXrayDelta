import { Link, isRouteErrorResponse, useRouteError } from 'react-router-dom'
import { Logo } from '../ui/Logo'
import { Icon } from '../ui/primitives'

/**
 * The router's fallback, used twice: as the `*` route (URL matches nothing) and as the root
 * `errorElement` (a loader or a render threw). Without it React Router renders its own developer
 * screen — "Hey developer 👋 … ErrorBoundary" — which is a stack trace addressed to us, shown to
 * the customer.
 *
 * `useRouteError()` gives nothing when this renders as the plain catch-all route, which is what
 * separates the two cases: no error = wrong address, error = something broke. The technical detail
 * is kept, but small and below the fold: the person needs a way out first, and a sentence to quote
 * to support second.
 *
 * The empty check is falsy, not `=== undefined`: React Router hands the no-error case back as null,
 * so the strict version put every mistyped URL under "Bir şeyler ters gitti" (seen in the browser,
 * not in a type).
 */
export default function NotFound() {
  const error = useRouteError()
  const notFound = !error || (isRouteErrorResponse(error) && error.status === 404)
  const detail = error instanceof Error ? error.message : null

  return (
    <div className="flex min-h-screen items-center justify-center bg-canvas p-6">
      <div className="w-full max-w-sm space-y-4 rounded-lg bg-surface p-8 text-center shadow-card">
        <Logo className="justify-center" />
        <Icon name={notFound ? 'compass-off-outline' : 'alert-circle-outline'} className="text-4xl text-muted" />
        <h1 className="text-xl font-semibold text-ink">
          {notFound ? 'Sayfa bulunamadı' : 'Bir şeyler ters gitti'}
        </h1>
        <p className="text-sm text-muted">
          {notFound
            ? 'Aradığınız adres taşınmış ya da hiç var olmamış olabilir.'
            : 'Bu sayfa yüklenemedi. Sayfayı yenilemek çoğu durumda yeterli olur.'}
        </p>
        <Link
          to="/"
          className="inline-flex items-center justify-center gap-1.5 rounded-lg bg-primary px-4 py-2 text-sm font-medium text-white shadow-sm hover:bg-primary-hover"
        >
          <Icon name="view-dashboard-outline" />Panoya dön
        </Link>
        {!notFound && detail && <p className="pt-2 text-xs text-muted/80 break-words">{detail}</p>}
      </div>
    </div>
  )
}
