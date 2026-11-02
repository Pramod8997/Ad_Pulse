export type AlertStatus = 'open' | 'acknowledged' | 'resolved' | 'unknown'
export type AlertSeverity = 'critical' | 'high' | 'medium' | 'low' | 'none' | 'unknown'
export type AlertAction = 'acknowledge' | 'resolve' | 'reopen'

export interface AlertFilters {
  status: AlertStatus | ''
  severity: AlertSeverity | ''
  metric: string
  campaign: string
  dateFrom: string
  dateTo: string
}

export interface AlertSummary {
  id: string
  title: string
  campaignId: string | null
  campaignName: string | null
  fingerprint: string | null
  metricName: string | null
  score: number | null
  severity: AlertSeverity
  status: AlertStatus
  observed: string | number | null
  baseline: string | number | null
  confidence: number | null
  dataSufficiency: 'insufficient' | 'sufficient' | 'unknown'
  detectedAt: string | null
  updatedAt: string | null
}

export interface AlertDetail extends AlertSummary {
  explanation: string | null
  ruleId: string | null
  scoringVersion: string | null
  evidenceWindowStart: string | null
  evidenceWindowEnd: string | null
  confidence: number | null
  dataSufficiency: 'insufficient' | 'sufficient' | 'unknown'
  reasonCodes: string[]
  recommendedChecks: string[]
}

export interface AlertListResponse {
  items: AlertSummary[]
  totalCount: number | null
  hasMore: boolean
  nextCursor: string | null
  lastSuccessfulRefresh: string | null
  partial: boolean
  stale: boolean
  correlationId: string | null
}

export interface AlertApi {
  list(filters: AlertFilters, cursor?: string | null): Promise<AlertListResponse>
  get(id: string): Promise<AlertDetail>
  mutate(id: string, action: AlertAction, reason: string): Promise<AlertDetail | null>
}
