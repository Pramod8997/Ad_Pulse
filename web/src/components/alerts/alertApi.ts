import type {
  AlertAction,
  AlertApi,
  AlertDetail,
  AlertFilters,
  AlertListResponse,
  AlertSeverity,
  AlertStatus,
  AlertSummary,
  IncidentStatus,
  IncidentTimelineEntry,
} from './types'
import { apiClient, ApiClientError } from '../../api'

const alertsPath = '/app/alerts'

export class AlertApiError extends Error {
  readonly status: number
  readonly correlationId: string | null

  constructor(message: string, status: number, correlationId: string | null = null) {
    super(message)
    this.name = 'AlertApiError'
    this.status = status
    this.correlationId = correlationId
  }
}

type JsonObject = Record<string, unknown>

function asObject(value: unknown): JsonObject {
  return typeof value === 'object' && value !== null ? value as JsonObject : {}
}

function first(value: JsonObject, ...keys: string[]): unknown {
  for (const key of keys) {
    if (key in value) return value[key]
  }
  return undefined
}

function stringValue(value: unknown): string | null {
  return typeof value === 'string' && value.trim() !== '' ? value : null
}

function numberValue(value: unknown): number | null {
  if (typeof value === 'number' && Number.isFinite(value)) return value
  if (typeof value === 'string' && value.trim() !== '') {
    const parsed = Number(value)
    return Number.isFinite(parsed) ? parsed : null
  }
  return null
}

function metricValue(value: unknown): string | number | null {
  return typeof value === 'string' || (typeof value === 'number' && Number.isFinite(value)) ? value : null
}

function statusValue(value: unknown): AlertStatus {
  const status = String(value ?? '').toLowerCase()
  if (status === 'active' || status === 'reopened') return 'open'
  if (status === 'open' || status === 'acknowledged' || status === 'resolved') return status
  return 'unknown'
}

function incidentStatusValue(value: unknown): IncidentStatus {
  const status = String(value ?? '').toLowerCase()
  if (status === 'false-positive') return 'false_positive'
  if (status === 'new' || status === 'acknowledged' || status === 'investigating' || status === 'mitigated' || status === 'resolved' || status === 'false_positive') {
    return status
  }
  return 'new'
}

function severityValue(value: unknown): AlertSeverity {
  const severity = String(value ?? '').toLowerCase()
  if (severity === 'critical' || severity === 'high' || severity === 'medium' || severity === 'low' || severity === 'none') {
    return severity
  }
  return 'unknown'
}

function stringArray(value: unknown): string[] {
  return Array.isArray(value) ? value.filter((entry): entry is string => typeof entry === 'string' && entry.trim() !== '') : []
}

function summaryValue(raw: unknown): AlertSummary {
  const value = asObject(raw)
  const id = stringValue(first(value, 'id', 'alertId')) ?? ''
  const metricName = stringValue(first(value, 'metricName', 'metric_name', 'metric'))
  const dataSufficiency = String(first(value, 'dataSufficiency', 'data_sufficiency') ?? '').toLowerCase()
  return {
    id,
    title: stringValue(first(value, 'title', 'alertTitle', 'name')) ?? metricName ?? id,
    campaignId: stringValue(first(value, 'campaignId', 'campaign_id')),
    campaignName: stringValue(first(value, 'campaignName', 'campaign_name')),
    fingerprint: stringValue(first(value, 'fingerprint')),
    metricName,
    score: numberValue(first(value, 'score')),
    severity: severityValue(first(value, 'severity')),
    status: statusValue(first(value, 'status')),
    observed: metricValue(first(value, 'observed', 'observedValue', 'observed_value')),
    baseline: metricValue(first(value, 'baseline', 'baselineValue', 'baseline_value')),
    confidence: numberValue(first(value, 'confidence')),
    dataSufficiency: dataSufficiency === 'sufficient' || dataSufficiency === 'insufficient' ? dataSufficiency : 'unknown',
    detectedAt: stringValue(first(value, 'detectedAt', 'detected_at', 'createdAt', 'created_at')),
    updatedAt: stringValue(first(value, 'updatedAt', 'updated_at')),
    incidentStatus: incidentStatusValue(first(value, 'incidentStatus', 'incident_status')),
    ownerSubjectId: stringValue(first(value, 'ownerSubjectId', 'owner_subject_id')),
    assignedAt: stringValue(first(value, 'assignedAt', 'assigned_at')),
  }
}

function timelineValue(value: unknown): IncidentTimelineEntry[] {
  if (!Array.isArray(value)) return []
  return value.flatMap((entry) => {
    const item = asObject(entry)
    const id = stringValue(item.id)
    const action = stringValue(item.action)
    if (!id || !action) return []
    return [{
      id,
      occurredAt: stringValue(item.occurredAt ?? item.occurred_at),
      actorSubjectId: stringValue(item.actorSubjectId ?? item.actor_subject_id),
      action,
      note: stringValue(item.note),
      fromIncidentStatus: incidentStatusValueOrNull(item.fromIncidentStatus ?? item.from_incident_status),
      toIncidentStatus: incidentStatusValueOrNull(item.toIncidentStatus ?? item.to_incident_status),
      ownerSubjectId: stringValue(item.ownerSubjectId ?? item.owner_subject_id),
    }]
  })
}

function incidentStatusValueOrNull(value: unknown): IncidentStatus | null {
  if (value === null || value === undefined || value === '') return null
  return incidentStatusValue(value)
}

function detailValue(raw: unknown): AlertDetail {
  const value = asObject(raw)
  const summary = summaryValue(value)
  const dataSufficiency = String(first(value, 'dataSufficiency', 'data_sufficiency') ?? '').toLowerCase()
  return {
    ...summary,
    explanation: stringValue(first(value, 'explanation', 'description', 'reason')),
    ruleId: stringValue(first(value, 'ruleId', 'rule_id')),
    scoringVersion: stringValue(first(value, 'scoringVersion', 'scoring_version')),
    evidenceWindowStart: stringValue(first(value, 'evidenceWindowStart', 'evidence_window_start')),
    evidenceWindowEnd: stringValue(first(value, 'evidenceWindowEnd', 'evidence_window_end')),
    confidence: numberValue(first(value, 'confidence')),
    dataSufficiency: dataSufficiency === 'sufficient' || dataSufficiency === 'insufficient' ? dataSufficiency : 'unknown',
    reasonCodes: stringArray(first(value, 'reasonCodes', 'reason_codes')),
    recommendedChecks: stringArray(first(value, 'recommendedChecks', 'recommended_checks')),
    resolutionSummary: stringValue(first(value, 'resolutionSummary', 'resolution_summary')),
    falsePositiveReason: stringValue(first(value, 'falsePositiveReason', 'false_positive_reason')),
    resolvedAt: stringValue(first(value, 'resolvedAt', 'resolved_at')),
    resolvedBySubjectId: stringValue(first(value, 'resolvedBySubjectId', 'resolved_by_subject_id')),
    timeline: timelineValue(first(value, 'timeline')),
  }
}

function listValue(raw: unknown, response?: Response): AlertListResponse {
  const envelope = asObject(raw)
  const rawItems = Array.isArray(raw) ? raw : first(envelope, 'items', 'alerts', 'data')
  const items = Array.isArray(rawItems) ? rawItems.map(summaryValue).filter((item) => item.id !== '') : []
  const totalCount = numberValue(first(envelope, 'totalCount', 'total_count', 'total'))
  return {
    items,
    totalCount,
    hasMore: first(envelope, 'hasMore', 'has_more', 'hasNextPage', 'has_next_page') === true,
    nextCursor: stringValue(first(envelope, 'nextCursor', 'next_cursor')),
    lastSuccessfulRefresh: stringValue(first(envelope, 'lastSuccessfulRefresh', 'last_successful_refresh')),
    partial: first(envelope, 'partial', 'isPartial', 'is_partial') === true,
    stale: first(envelope, 'stale', 'isStale', 'is_stale') === true,
    correlationId: response?.headers.get('x-correlation-id') ?? stringValue(first(envelope, 'correlationId', 'correlation_id')),
  }
}

function apiBase(value?: string): string {
  if (value !== undefined) return value.replace(/\/$/, '')
  return (import.meta.env.VITE_API_BASE_URL ?? '').replace(/\/$/, '')
}

function queryString(filters: AlertFilters, cursor: string | null | undefined): string {
  const params = new URLSearchParams({ limit: '25' })
  if (filters.status) params.set('status', filters.status)
  if (filters.severity) params.set('severity', filters.severity)
  if (filters.metric.trim()) params.set('metric', filters.metric.trim().toLowerCase())
  if (filters.campaign.trim()) params.set('campaignId', filters.campaign.trim())
  if (filters.dateFrom) params.set('from', filters.dateFrom)
  if (filters.dateTo) params.set('to', filters.dateTo)
  if (filters.incidentStatus) params.set('incidentStatus', filters.incidentStatus)
  if (filters.owner.trim()) params.set('ownerSubjectId', filters.owner.trim())
  if (cursor) params.set('cursor', cursor)
  const encoded = params.toString()
  return encoded ? `?${encoded}` : ''
}

async function readResponse(response: Response): Promise<unknown> {
  const text = await response.text()
  if (!text) return null
  try {
    return JSON.parse(text) as unknown
  } catch {
    return null
  }
}

async function request(base: string, path: string, init?: RequestInit): Promise<{ value: unknown; response: Response }> {
  const response = await fetch(`${base}${path}`, {
    credentials: 'include',
    ...init,
    headers: {
      Accept: 'application/json',
      ...(init?.body ? { 'Content-Type': 'application/json' } : {}),
      ...init?.headers,
    },
  })
  const value = await readResponse(response)
  if (!response.ok) {
    const error = asObject(value)
    const message = stringValue(first(error, 'title', 'detail', 'message')) ?? `Alert request failed (${response.status})`
    throw new AlertApiError(message, response.status, response.headers.get('x-correlation-id') ?? stringValue(first(error, 'correlationId', 'correlation_id')))
  }
  return { value, response }
}

async function sharedRequest(path: string, options: { method?: 'GET' | 'POST' | 'PATCH'; body?: unknown } = {}): Promise<unknown> {
  try {
    return await apiClient.request<unknown>(path, options)
  } catch (error) {
    if (error instanceof ApiClientError) {
      throw new AlertApiError(error.message, error.status ?? 0, error.correlationId)
    }
    throw error
  }
}

export function createAlertApi(baseUrl?: string): AlertApi {
  const base = apiBase(baseUrl)
  const useSharedClient = baseUrl === undefined
  return {
    async list(filters, cursor) {
      if (useSharedClient) return listValue(await sharedRequest(`${alertsPath}${queryString(filters, cursor)}`))
      const result = await request(base, `${alertsPath}${queryString(filters, cursor)}`)
      return listValue(result.value, result.response)
    },
    async get(id) {
      if (useSharedClient) {
        const value = await sharedRequest(`${alertsPath}/${encodeURIComponent(id)}`)
        const payload = asObject(value)
        return detailValue(first(payload, 'alert', 'data') ?? value)
      }
      const result = await request(base, `${alertsPath}/${encodeURIComponent(id)}`)
      const payload = asObject(result.value)
      return detailValue(first(payload, 'alert', 'data') ?? result.value)
    },
    async mutate(id, action, reason) {
      if (useSharedClient) {
        const value = await sharedRequest(`${alertsPath}/${encodeURIComponent(id)}/${action}`, { method: 'POST', body: { confirm: true, reason } })
        if (value === null) return null
        const payload = asObject(value)
        return detailValue(first(payload, 'alert', 'data') ?? value)
      }
      const result = await request(base, `${alertsPath}/${encodeURIComponent(id)}/${action}`, {
        method: 'POST',
        body: JSON.stringify({ confirm: true, reason }),
      })
      if (result.value === null) return null
      const payload = asObject(result.value)
      return detailValue(first(payload, 'alert', 'data') ?? result.value)
    },
    async changeIncident(id, status, summary) {
      const body = { status, summary, confirm: true }
      const value = useSharedClient
        ? await sharedRequest(`${alertsPath}/${encodeURIComponent(id)}/incident`, { method: 'PATCH', body })
        : (await request(base, `${alertsPath}/${encodeURIComponent(id)}/incident`, { method: 'PATCH', body: JSON.stringify(body) })).value
      if (value === null) return null
      const payload = asObject(value)
      return detailValue(first(payload, 'alert', 'data') ?? value)
    },
    async assign(id, ownerSubjectId) {
      const body = { ownerSubjectId, confirm: true }
      const value = useSharedClient
        ? await sharedRequest(`${alertsPath}/${encodeURIComponent(id)}/assignment`, { method: 'PATCH', body })
        : (await request(base, `${alertsPath}/${encodeURIComponent(id)}/assignment`, { method: 'PATCH', body: JSON.stringify(body) })).value
      if (value === null) return null
      const payload = asObject(value)
      return detailValue(first(payload, 'alert', 'data') ?? value)
    },
    async addNote(id, note) {
      const body = { note }
      const value = useSharedClient
        ? await sharedRequest(`${alertsPath}/${encodeURIComponent(id)}/notes`, { method: 'POST', body })
        : (await request(base, `${alertsPath}/${encodeURIComponent(id)}/notes`, { method: 'POST', body: JSON.stringify(body) })).value
      if (value === null) return null
      const payload = asObject(value)
      return detailValue(first(payload, 'alert', 'data') ?? value)
    },
  }
}
