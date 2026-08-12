import { useQuery } from '@tanstack/react-query'
import { api } from './api'

export type TicketReport = {
  companyId: string | null
  totalTickets: number
  byStatusCategory: { category: number; count: number }[]
  avgFirstResponseHours: number | null
  avgResolutionHours: number | null
  /** Denominators of the two averages above — different sets (answered vs. resolved), so neither
   *  number means anything on screen without the count it was taken over. */
  firstResponseCount: number
  resolutionCount: number
  /** `assignedToName` is null only when `assignedToId` is (unassigned) — the server resolves it. */
  staffLoad: { assignedToId: string | null; assignedToName: string | null; openCount: number }[]
  trend: { date: string; opened: number; closed: number }[]
  /** Null when the caller lacks ticket.value — the figures are withheld server-side, not hidden here. */
  revenue: RevenueSummary | null
  customers: CustomerBreakdownItem[]
}

/** Per-customer row: who was dealt with, how much, for how long, and what it was worth (Faz 41). */
export type CustomerBreakdownItem = {
  customerId: string
  name: string
  email: string
  ticketCount: number
  openCount: number
  wonCount: number
  lostCount: number
  /** Both null without ticket.value — withheld server-side, exactly like `revenue`. */
  wonTotal: number | null
  openTotal: number | null
  /** ELAPSED hours open→resolved, not worked hours: the system keeps no timesheets. */
  avgResolutionHours: number | null
  totalResolutionHours: number | null
  avgFirstResponseHours: number | null
  firstTicketAt: string
  lastTicketAt: string
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

/**
 * Downloads the report as a PDF through the authed client (a plain <a> can't send the Bearer header).
 * Replaced the CSV export in Faz 41: the old file was a raw ticket dump with bare GUIDs in it, while
 * the PDF carries the general stats, the money summary and the per-customer table with their labels.
 */
export async function downloadReportPdf(companyId: string | null) {
  const res = await api.get(reportPath(companyId, '/export.pdf'), { responseType: 'blob' })
  const url = URL.createObjectURL(res.data as Blob)
  const a = document.createElement('a')
  a.href = url
  a.download = companyId ? `rapor-${companyId}.pdf` : 'rapor-tum-sirketler.pdf'
  a.click()
  // Revoked on the next tick: revoking synchronously can beat the click in Safari and the download
  // silently produces an empty file.
  setTimeout(() => URL.revokeObjectURL(url), 0)
}
