import { useQuery } from '@tanstack/react-query'
import { useEffect } from 'react'
import { api } from './api'

/** The three branding values every visitor may see (`GET /api/public/brand`). */
export type Brand = { systemName: string; primaryColor: string; logoUrl: string | null }

const FALLBACK: Brand = { systemName: 'Kanby', primaryColor: '#1f3bb3', logoUrl: null }

/**
 * The operator's system name for the shell, the sign-in screen and the browser tab. Anonymous on
 * purpose: the sign-in screen renders before there is a session, and reading `brand.system_name` from
 * `/api/settings` would have meant opening the whole super-admin settings surface to every user — the
 * reason the name sat hardcoded as "Kanby" until now (borç #45).
 *
 * Never in a loading state as far as callers are concerned: it falls back to the packaged brand, so the
 * wordmark paints on the first frame instead of flashing empty while the request is in flight.
 */
export function useBrand(): Brand {
  const { data } = useQuery({
    queryKey: ['brand'],
    queryFn: async () => (await api.get<Brand>('/public/brand')).data,
    staleTime: 10 * 60_000, // branding changes about once a year; do not re-fetch it per screen
  })
  const brand = data ?? FALLBACK

  // The tab title is part of the same brand. Kept here rather than in a screen so it cannot drift from
  // what the sidebar says.
  useEffect(() => { document.title = brand.systemName }, [brand.systemName])

  return brand
}
