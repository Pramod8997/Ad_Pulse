<script setup lang="ts">
import { computed, ref } from 'vue'
import BudgetPacingPanel from '@/components/overview/BudgetPacingPanel.vue'
import CampaignPerformanceTable from '@/components/overview/CampaignPerformanceTable.vue'
import KpiStrip from '@/components/overview/KpiStrip.vue'
import OverviewState from '@/components/overview/OverviewState.vue'
import PriorityAlertsPanel from '@/components/overview/PriorityAlertsPanel.vue'
import TrendPanel from '@/components/overview/TrendPanel.vue'
import type { OverviewData, OverviewKpi } from '@/components/overview/types'
import { overviewErrorCorrelationId, useOverviewQuery } from '@/components/overview/overviewApi'

function isoDate(date: Date): string {
  const year = date.getFullYear()
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}
function dateDaysAgo(days: number): string {
  const date = new Date()
  date.setDate(date.getDate() - days)
  return isoDate(date)
}

const from = ref(dateDaysAgo(29))
const to = ref(isoDate(new Date()))
const query = useOverviewQuery({ from, to })
const isLoading = computed(() => query.isLoading.value)
const isError = computed(() => query.isError.value)
const isFetching = computed(() => query.isFetching.value)

const data = computed(() => query.data.value as OverviewData | undefined)
const timezone = computed(() => data.value?.period.reportingTimeZone || Intl.DateTimeFormat().resolvedOptions().timeZone)
const hasData = computed(() => Boolean(data.value && (
  data.value.kpis.length || data.value.trend.length || data.value.budgetPacing.length || data.value.alerts.length || data.value.campaigns.length
)))
const isEmpty = computed(() => Boolean(data.value && !hasData.value))
const errorMessage = computed(() => {
  const error = query.error.value as { message?: string } | null
  return error?.message || 'The overview could not be loaded. Try again or contact support.'
})
const correlationId = computed(() => {
  return overviewErrorCorrelationId(query.error.value) || data.value?.meta.correlationId || null
})
const lastUpdated = computed(() => data.value?.meta.lastUpdatedAt || (query.dataUpdatedAt.value ? new Date(query.dataUpdatedAt.value).toISOString() : null))
const stale = computed(() => Boolean(data.value && (query.isStale.value || query.isFetching.value)))
const partialMessage = computed(() => {
  const missing = data.value?.meta.missingSections || []
  return missing.length ? `Some sections are unavailable: ${missing.join(', ')}.` : 'Some metrics are unavailable. Missing values are shown as —.'
})

const kpis = computed<OverviewKpi[]>(() => {
  const values = data.value?.kpis || []
  const definitions: Array<{ key: OverviewKpi['key']; label: string }> = [
    { key: 'spend', label: 'Spend' },
    { key: 'budgetConsumed', label: 'Budget consumed' },
    { key: 'cpc', label: 'CPC' },
    { key: 'cpa', label: 'CPA' },
    { key: 'roas', label: 'ROAS' },
  ]
  return definitions.map((definition) => values.find((value) => value.key === definition.key) || {
    ...definition,
    value: null,
    currency: null,
    comparison: null,
  })
})

const displayCurrency = computed<string | null>(() => data.value?.currency || null)

function formatPeriod(value: string): string {
  const date = new Date(`${value}T00:00:00Z`)
  return Number.isNaN(date.valueOf()) ? value : new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeZone: 'UTC' }).format(date)
}

function formatUpdated(value: string | null): string {
  if (!value) return 'Last refresh unavailable'
  const date = new Date(value)
  return Number.isNaN(date.valueOf()) ? 'Last refresh unavailable' : `Last refreshed ${new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(date)}`
}

function refresh() { return query.refetch() }
</script>

<template>
  <main class="overview-page">
    <header class="overview-page__header">
      <div>
        <p class="overview-page__eyebrow">Workspace overview</p>
        <h1>Overview</h1>
        <p class="overview-page__period">{{ from && to ? `${formatPeriod(from)} – ${formatPeriod(to)}` : 'Select a reporting period' }} · {{ timezone }}</p>
      </div>
      <form class="overview-filters" aria-label="Overview reporting period" @submit.prevent="refresh">
        <label>From <input v-model="from" type="date" :max="to || undefined" /></label>
        <label>To <input v-model="to" type="date" :min="from || undefined" :max="isoDate(new Date())" /></label>
        <button type="submit" class="overview-button overview-button--primary" :disabled="isFetching">Refresh</button>
      </form>
    </header>

    <div class="overview-page__meta" aria-live="polite">
      <span>{{ formatUpdated(lastUpdated) }}</span>
      <span v-if="stale" class="overview-page__stale">Updating from the server…</span>
    </div>

    <OverviewState v-if="isLoading" kind="loading" />
    <OverviewState v-else-if="isError && !data" kind="error" title="Overview unavailable" :message="errorMessage" :correlation-id="correlationId" :retry="refresh" />
    <OverviewState v-else-if="isEmpty" kind="empty" title="No overview data yet" message="There are no campaign metrics for this reporting period. Choose another period or add campaign metrics to begin." />
    <template v-else-if="data">
      <aside v-if="data.meta.partial" class="overview-notice overview-notice--partial" role="status"><strong>Partial data.</strong> {{ partialMessage }}</aside>
      <aside v-if="isError" class="overview-notice overview-notice--error" role="alert"><strong>Refresh incomplete.</strong> Showing the last successful overview. {{ errorMessage }} <button type="button" class="overview-link" @click="refresh">Try again</button></aside>

      <KpiStrip :kpis="kpis" :currency="displayCurrency" />

      <section class="overview-grid overview-grid--primary" aria-label="Overview analysis">
        <TrendPanel :points="data.trend" :timezone="timezone" :currency="displayCurrency" />
        <PriorityAlertsPanel :alerts="data.alerts" />
      </section>

      <BudgetPacingPanel :rows="data.budgetPacing" :timezone="timezone" :currency="displayCurrency" />
      <CampaignPerformanceTable :rows="data.campaigns" />
    </template>
  </main>
</template>

<style scoped>
.overview-page { display: grid; gap: 20px; width: 100%; max-width: 1440px; margin: 0 auto; padding: 24px; box-sizing: border-box; color: var(--text-primary, #182230); }
.overview-page__header { display: flex; align-items: end; justify-content: space-between; gap: 24px; }
.overview-page__eyebrow { margin: 0 0 6px; color: var(--text-secondary, #667085); font-size: 13px; font-weight: 600; letter-spacing: .02em; }
.overview-page h1 { margin: 0; font-size: clamp(24px, 3vw, 28px); line-height: 1.2; font-weight: 650; }
.overview-page__period { margin: 7px 0 0; color: var(--text-secondary, #667085); font-size: 13px; }
.overview-filters { display: flex; align-items: end; flex-wrap: wrap; justify-content: flex-end; gap: 10px; }
.overview-filters label { display: grid; gap: 5px; color: var(--text-secondary, #667085); font-size: 12px; font-weight: 600; }
.overview-filters input { min-height: 40px; padding: 7px 10px; border: 1px solid var(--border, #e4e7ec); border-radius: 6px; background: var(--surface, #fff); color: var(--text-primary, #182230); font: inherit; }
.overview-filters input:focus-visible, .overview-button:focus-visible, .overview-link:focus-visible { outline: 3px solid color-mix(in srgb, var(--focus, #2e90fa) 45%, transparent); outline-offset: 2px; }
.overview-page__meta { display: flex; flex-wrap: wrap; gap: 12px; color: var(--text-secondary, #667085); font-size: 12px; }
.overview-page__stale { color: var(--warning, #a15c07); }
.overview-grid { display: grid; gap: 20px; }
.overview-grid--primary { grid-template-columns: minmax(0, 1.8fr) minmax(300px, 1fr); }
.overview-notice { padding: 12px 14px; border: 1px solid var(--border, #e4e7ec); border-radius: 8px; font-size: 13px; }
.overview-notice--partial { border-color: color-mix(in srgb, var(--warning, #a15c07) 35%, var(--border, #e4e7ec)); background: color-mix(in srgb, var(--warning, #a15c07) 6%, var(--surface, #fff)); color: var(--warning, #a15c07); }
.overview-notice--error { border-color: color-mix(in srgb, var(--danger, #b42318) 35%, var(--border, #e4e7ec)); background: color-mix(in srgb, var(--danger, #b42318) 5%, var(--surface, #fff)); color: var(--danger, #b42318); }
.overview-link { padding: 0; border: 0; background: none; color: inherit; font: inherit; font-weight: 600; text-decoration: underline; cursor: pointer; }
.overview-button { min-height: 40px; padding: 8px 14px; border: 1px solid var(--border, #e4e7ec); border-radius: 6px; background: var(--surface, #fff); color: var(--text-primary, #182230); font: inherit; cursor: pointer; }
.overview-button--primary { border-color: var(--accent, #176b64); background: var(--accent, #176b64); color: #fff; }
.overview-button:disabled { cursor: not-allowed; opacity: .55; }
@media (max-width: 980px) { .overview-page__header { align-items: stretch; flex-direction: column; } .overview-filters { justify-content: flex-start; } .overview-grid--primary { grid-template-columns: 1fr; } }
@media (max-width: 620px) { .overview-page { gap: 16px; padding: 16px 12px; } .overview-filters { display: grid; grid-template-columns: 1fr 1fr; } .overview-filters label { min-width: 0; } .overview-filters input { width: 100%; box-sizing: border-box; } .overview-filters .overview-button { grid-column: 1 / -1; } }
</style>
