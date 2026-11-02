import { apiClient } from '../../api'
import type { SettingValue } from './SettingsForm.vue'

export interface TenantSettingsResponse {
  name?: string
  reportingTimeZone: string
  defaultCurrency?: string | null
  reportingCurrency?: string | null
  createdAt?: string
  updatedAtUtc?: string
  updatedAt?: string
  canEdit?: boolean
  stale?: boolean
}

export interface SettingsApi {
  get(options?: { signal?: AbortSignal }): Promise<TenantSettingsResponse>
  update(values: Record<string, SettingValue>, options?: { signal?: AbortSignal }): Promise<TenantSettingsResponse>
}

function asRecord(value: unknown): Record<string, unknown> {
  return typeof value === 'object' && value !== null ? value as Record<string, unknown> : {}
}

function stringValue(value: unknown): string | undefined {
  return typeof value === 'string' ? value : undefined
}

function normalize(value: unknown): TenantSettingsResponse {
  const item = asRecord(value)
  return {
    name: stringValue(item.name),
    reportingTimeZone: stringValue(item.reportingTimeZone) ?? '',
    defaultCurrency: stringValue(item.defaultCurrency),
    reportingCurrency: stringValue(item.reportingCurrency),
    createdAt: stringValue(item.createdAt),
    updatedAtUtc: stringValue(item.updatedAtUtc ?? item.updatedAt),
    canEdit: item.canEdit !== false,
    stale: item.stale === true,
  }
}

export function createSettingsApi(): SettingsApi {
  return {
    get(options) {
      return apiClient.get<unknown>('/app/settings', options?.signal).then(normalize)
    },
    update(values, options) {
      return apiClient.request<unknown>('/app/settings', {
        method: 'PATCH',
        body: {
          reportingTimeZone: String(values.reportingTimeZone ?? ''),
          defaultCurrency: String(values.reportingCurrency ?? ''),
        },
        signal: options?.signal,
      }).then(normalize)
    },
  }
}
