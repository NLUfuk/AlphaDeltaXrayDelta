import { useQuery } from '@tanstack/react-query'
import { api } from './api'

export type TicketReport = {
  companyId: string | null
  totalTickets: number
  byStatusCategory: { category: number; count: number }[]
  avgFirstResponseHours: number | null
  avgResolutionHours: number | null
  staffLoad: { assignedToId: string | null; openCount: number }[]
  byCategory: { categoryId: string | null; count: number }[]
  trend: { date: string; opened: number; closed: number }[]
  /** Null when the caller lacks ticket.value — the figures are withheld server-side, not hidden here. */
  revenue: RevenueSummary | null
}

export type RevenueSummary = {
  /** Currency of every amount below (Settings finance.currency). */
  currency: string
  wonTotal: number
  wonCount: number
  lostTotal: number
  lostCount: number
  openTotal: number
  openCount: number
  /** Tickets with no amount yet. Kept apart from zero-value ones on purpose. */
  unpricedCount: number
  /** Null until something has closed — 0/0 is undefined, not a 0% win rate. */
  winRateByCount: number | null
  winRateByValue: number | null
  /** Realised / estimated across won tickets carrying both figures. */
  forecastAccuracy: number | null
  trend: { month: string; won: number; lost: number }[]
}

// Global for super admin, otherwise the caller's company. Path decided here so screens stay dumb.
function reportPath(companyId: string | null, suffix = '') {
  return companyId ? `/reports/company/${companyId}${suffix}` : `/reports/global${suffix}`
}

export function useReport(companyId: string | null) {
  return useQuery({
    queryKey: ['report', companyId],
    queryFn: async () => (await api.get<TicketReport>(reportPath(companyId))).data,
  })
}

/** Downloads the CSV through the authed client (a plain <a> can't send the Bearer header). */
export async function downloadCsv(companyId: string | null) {
  const res = await api.get(reportPath(companyId, '/export.csv'), { responseType: 'blob' })
  const url = URL.createObjectURL(res.data as Blob)
  const a = document.createElement('a')
  a.href = url
  a.download = companyId ? `report-${companyId}.csv` : 'report-global.csv'
  a.click()
  URL.revokeObjectURL(url)
}
