<script setup lang="ts">
import { computed, inject, onMounted, ref } from 'vue'
import { useAuthStore } from '../stores/auth'
import SettingsForm, {
  type SettingDefinition,
  type SettingValue,
} from '../components/settings/SettingsForm.vue'
import { createSettingsApi, type SettingsApi, type TenantSettingsResponse } from '../components/settings/settingsApi'

export interface TenantSettingsApi {
  settings: SettingsApi
}

interface SettingsError {
  status?: number
  correlationId?: string
  message: string
}

type ViewState = 'loading' | 'ready' | 'empty' | 'error' | 'forbidden' | 'unauthorized'

const api = inject<TenantSettingsApi | undefined>('adpulse-api')
const settingsApi: SettingsApi = api?.settings ?? createSettingsApi()
const auth = useAuthStore()
const settings = ref<SettingDefinition[]>([])
const canEdit = ref(false)
const updatedAt = ref('')
const stale = ref(false)
const state = ref<ViewState>('loading')
const failure = ref<SettingsError | null>(null)
const isSaving = ref(false)
const isRefreshing = ref(false)
const notConfigured = ref(false)
let requestNumber = 0
let activeController: AbortController | undefined

const hasSettings = computed(() => settings.value.length > 0)
const isInitialLoading = computed(() => state.value === 'loading' && !hasSettings.value)
const canUseSettings = computed(() => state.value !== 'forbidden' && state.value !== 'unauthorized')

function hasTenantAdminRole(): boolean {
  return auth.hasRole('tenant-admin') || auth.hasRole('tenant_admin')
}

function errorDetails(error: unknown): SettingsError {
  const candidate = (error ?? {}) as { status?: number; response?: { status?: number }; correlationId?: string; message?: string }
  const status = candidate.status ?? candidate.response?.status
  if (status === 401) return { status, message: 'Your session is not authorized to view tenant settings.' }
  if (status === 403) return { status, message: 'Only tenant administrators can view or change these settings.' }
  if ((status === 400 || status === 422) && typeof candidate.message === 'string' && candidate.message.trim()) {
    return { status, correlationId: candidate.correlationId, message: candidate.message }
  }
  return {
    status,
    correlationId: candidate.correlationId,
    message: 'Tenant settings could not be loaded. Try again or contact support if the problem persists.',
  }
}

function applyResponse(result: TenantSettingsResponse, options: { configured?: boolean } = {}) {
  const configured = options.configured !== false
  notConfigured.value = !configured
  settings.value = [
    {
      key: 'reportingTimeZone',
      label: 'Reporting timezone',
      description: 'IANA timezone used to convert event timestamps into tenant reporting dates.',
      kind: 'text',
      value: result.reportingTimeZone,
      editable: true,
      required: true,
    },
    {
      key: 'reportingCurrency',
      label: 'Reporting currency',
      description: 'Three-letter uppercase currency used for tenant reporting.',
      kind: 'text',
      value: result.reportingCurrency ?? result.defaultCurrency ?? '',
      editable: true,
      required: true,
    },
  ]
  canEdit.value = result.canEdit ?? hasTenantAdminRole()
  updatedAt.value = result.updatedAtUtc ?? (configured ? new Date().toISOString() : '')
  stale.value = result.stale === true || isOlderThanFiveMinutes(result.updatedAtUtc)
  state.value = 'ready'
}

async function load() {
  activeController?.abort()
  const controller = new AbortController()
  activeController = controller
  const currentRequest = ++requestNumber
  isRefreshing.value = true
  if (!hasSettings.value) state.value = 'loading'
  failure.value = null

  try {
    const result = await settingsApi.get({ signal: controller.signal })
    if (currentRequest !== requestNumber) return
    applyResponse(result)
  } catch (error) {
    if (controller.signal.aborted || currentRequest !== requestNumber) return
    const details = errorDetails(error)
    if (details.status === 404) {
      applyResponse({ reportingTimeZone: '', defaultCurrency: '', reportingCurrency: '', stale: false }, { configured: false })
      return
    }
    failure.value = details
    if (hasSettings.value) stale.value = true
    if (failure.value.status === 401) state.value = 'unauthorized'
    else if (failure.value.status === 403) state.value = 'forbidden'
    else state.value = 'error'
  } finally {
    if (currentRequest === requestNumber) isRefreshing.value = false
  }
}

async function save(values: Record<string, SettingValue>) {
  if (!canEdit.value || isSaving.value) return
  const controller = new AbortController()
  isSaving.value = true
  failure.value = null
  try {
    const result = await settingsApi.update(values, { signal: controller.signal })
    applyResponse(result)
  } catch (error) {
    failure.value = errorDetails(error)
    if (failure.value.status === 403) state.value = 'forbidden'
    else if (failure.value.status === 401) state.value = 'unauthorized'
  } finally {
    isSaving.value = false
  }
}

function isOlderThanFiveMinutes(value?: string) {
  if (!value) return false
  const timestamp = Date.parse(value)
  return !Number.isNaN(timestamp) && Date.now() - timestamp > 5 * 60 * 1000
}

function formatRefreshTime(value: string) {
  const date = new Date(value)
  return Number.isNaN(date.valueOf()) ? 'an unknown time' : date.toLocaleString()
}

onMounted(load)
</script>

<template>
  <main class="settings-page" aria-labelledby="settings-title">
    <header class="page-header">
      <div>
        <p class="eyebrow">Workspace governance</p>
        <h1 id="settings-title">Tenant settings</h1>
        <p class="lede">Manage persisted settings exposed for this authorized workspace.</p>
      </div>
      <button
        type="button"
        class="refresh-button"
        :disabled="!canUseSettings || isRefreshing || isSaving"
        :aria-busy="isRefreshing"
        @click="load"
      >
        {{ isRefreshing ? 'Refreshing…' : 'Refresh' }}
      </button>
    </header>

    <section v-if="state === 'forbidden' || state === 'unauthorized'" class="state-panel authorization-state" role="alert">
      <h2>{{ state === 'forbidden' ? 'Settings access is restricted' : 'Sign-in is required' }}</h2>
      <p>{{ failure?.message }}</p>
      <p class="disabled-help">Settings controls are disabled until this access issue is resolved.</p>
    </section>

    <section v-else class="panel" aria-labelledby="settings-section-title">
      <div class="section-heading">
        <div>
          <h2 id="settings-section-title">Persisted workspace settings</h2>
          <p v-if="updatedAt" :class="['refresh-note', { 'stale-note': stale }]" role="status">
            {{ stale ? 'Showing stale data. ' : '' }}Last successful refresh {{ formatRefreshTime(updatedAt) }}.
          </p>
          <p v-else-if="notConfigured" class="refresh-note" role="status">
            Reporting settings have not been configured yet. Set a reporting timezone and currency to save initial defaults.
          </p>
        </div>
        <span v-if="!canEdit && hasSettings" class="status-label">Read-only access</span>
      </div>

      <div v-if="isInitialLoading" class="loading-state" role="status" aria-live="polite">
        <span v-for="row in 4" :key="row" class="loading-row" aria-hidden="true"></span>
        <span class="sr-only">Loading tenant settings</span>
      </div>

      <div v-else-if="state === 'empty'" class="empty-state">
        <h3>No settings are exposed</h3>
        <p>This workspace has no persisted tenant settings available to manage.</p>
      </div>

      <div v-else-if="state === 'error'" class="error-state" role="alert">
        <h3>Settings unavailable</h3>
        <p>{{ failure?.message }}</p>
        <p v-if="failure?.correlationId" class="support-reference">Reference: {{ failure.correlationId }}</p>
        <button type="button" class="secondary-button" :disabled="isRefreshing" @click="load">Try again</button>
      </div>

      <SettingsForm
        v-else-if="hasSettings"
        :settings="settings"
        :can-edit="canEdit"
        :saving="isSaving"
        :error-message="failure?.message"
        @save="save"
      />
    </section>
  </main>
</template>

<style scoped>
.settings-page {
  display: grid;
  gap: 20px;
  max-width: 960px;
  margin: 0 auto;
  padding: 24px;
  color: var(--text-primary);
}

.page-header,
.section-heading {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 16px;
}

.eyebrow {
  margin: 0 0 6px;
  color: var(--accent);
  font-size: 12px;
  font-weight: 700;
  letter-spacing: 0.08em;
  text-transform: uppercase;
}

h1,
h2,
h3,
p {
  margin-top: 0;
}

h1 {
  margin-bottom: 6px;
  font-size: clamp(24px, 3vw, 28px);
  line-height: 1.2;
}

h2 {
  margin-bottom: 4px;
  font-size: 17px;
}

h3 {
  margin-bottom: 8px;
  font-size: 16px;
}

.lede,
.section-heading p,
.empty-state p,
.error-state p,
.state-panel p {
  margin-bottom: 0;
  color: var(--text-secondary);
  font-size: 14px;
}

.panel,
.state-panel {
  display: grid;
  gap: 20px;
  border: 1px solid var(--border);
  border-radius: 8px;
  background: var(--surface);
  padding: 20px;
}

.state-panel {
  border-color: color-mix(in srgb, var(--danger) 35%, var(--border));
}

.refresh-button,
.secondary-button {
  min-height: 40px;
  border: 1px solid var(--accent);
  border-radius: 6px;
  background: var(--accent);
  color: var(--surface);
  cursor: pointer;
  font: inherit;
  font-weight: 600;
  padding: 8px 14px;
}

.refresh-button:hover:not(:disabled) {
  background: var(--accent-hover);
}

.secondary-button {
  border-color: var(--border);
  background: var(--surface);
  color: var(--text-primary);
}

.secondary-button:hover:not(:disabled) {
  background: var(--surface-subtle);
}

button:focus-visible {
  outline: 3px solid var(--focus);
  outline-offset: 2px;
}

button:disabled {
  cursor: not-allowed;
  opacity: 0.6;
}

.disabled-help,
.refresh-note,
.support-reference {
  margin-bottom: 0;
  color: var(--text-secondary);
  font-size: 13px;
}

.stale-note {
  color: var(--warning);
}

.status-label {
  border-radius: 999px;
  background: var(--surface-subtle);
  color: var(--text-secondary);
  font-size: 12px;
  font-weight: 600;
  padding: 5px 9px;
  white-space: nowrap;
}

.loading-state {
  display: grid;
  gap: 16px;
}

.loading-row {
  display: block;
  height: 40px;
  border-radius: 5px;
  background: var(--surface-subtle);
}

.empty-state,
.error-state {
  border: 1px dashed var(--border);
  border-radius: 6px;
  padding: 28px 20px;
}

.error-state {
  border-color: var(--danger);
}

.error-state .secondary-button {
  margin-top: 16px;
}

.sr-only {
  position: absolute;
  width: 1px;
  height: 1px;
  padding: 0;
  margin: -1px;
  overflow: hidden;
  clip: rect(0, 0, 0, 0);
  white-space: nowrap;
  border: 0;
}

@media (max-width: 640px) {
  .settings-page {
    padding: 16px 12px;
  }

  .page-header,
  .section-heading {
    align-items: stretch;
    flex-direction: column;
  }

  .refresh-button {
    width: 100%;
  }

  .panel,
  .state-panel {
    padding: 16px;
  }
}
</style>
