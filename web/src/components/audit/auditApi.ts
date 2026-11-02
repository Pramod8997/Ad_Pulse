import { apiClient, ApiClientError } from '../../api'
import type { AuditEvent } from './AuditTable.vue'

export interface AuditListQuery {
  limit: number
  cursor?: string
  from?: string
  to?: string
  actorSubjectId?: string
  action?: string
  entityType?: string
}

export interface AuditPage {
  items: AuditEvent[]
  nextCursor: string | null
  hasMore: boolean
  limit: number
  updatedAt?: string
  stale?: boolean
  partial?: boolean
}

export type AuditApiError = ApiClientError

function stringValue(value: unknown): string | null {
  return typeof value === 'string' && value.length > 0 ? value : null
}

function asRecord(value: unknown): Record<string, unknown> {
  return typeof value === 'object' && value !== null ? value as Record<string, unknown> : {}
}

function normalizeEvent(value: unknown): AuditEvent | null {
  const item = asRecord(value)
  const id = stringValue(item.id)
  const occurredAt = stringValue(item.occurredAt ?? item.occurredAtUtc)
  if (!id || !occurredAt) return null
  return {
    id,
    occurredAt,
    actorSubjectId: stringValue(item.actorSubjectId) ?? 'Unavailable',
    action: stringValue(item.action) ?? 'Unavailable',
    entityType: stringValue(item.entityType) ?? 'Unavailable',
    entityId: stringValue(item.entityId) ?? 'Unavailable',
  }
}

function normalizePage(value: unknown): AuditPage {
  const body = asRecord(value)
  const rawItems = Array.isArray(body.items) ? body.items : []
  const items = rawItems.map(normalizeEvent).filter((item): item is AuditEvent => item !== null)
  return {
    items,
    nextCursor: stringValue(body.nextCursor),
    hasMore: body.hasMore === true,
    limit: typeof body.limit === 'number' ? body.limit : 25,
    updatedAt: stringValue(body.updatedAt ?? body.lastSuccessfulRefresh) ?? undefined,
    stale: body.stale === true,
    partial: body.partial === true,
  }
}

function queryString(query: AuditListQuery) {
  const params = new URLSearchParams({ limit: String(query.limit) })
  if (query.cursor) params.set('cursor', query.cursor)
  if (query.from) params.set('from', query.from)
  if (query.to) params.set('to', query.to)
  if (query.actorSubjectId) params.set('actorSubjectId', query.actorSubjectId)
  if (query.action) params.set('action', query.action)
  if (query.entityType) params.set('entityType', query.entityType)
  return params.toString()
}

export function createAuditApi() {
  return {
    list(query: AuditListQuery, options?: { signal?: AbortSignal }): Promise<AuditPage> {
      return apiClient.get<unknown>(`/app/audit?${queryString(query)}`, options?.signal).then(normalizePage)
    },
  }
}
