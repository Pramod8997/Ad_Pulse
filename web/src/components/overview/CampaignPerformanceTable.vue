<script setup lang="ts">
import { computed, ref } from 'vue'
import type { CampaignPerformanceRow } from './types'

const props = defineProps<{ rows: CampaignPerformanceRow[] }>()
const sortKey = ref<keyof CampaignPerformanceRow>('spend')
const sortDirection = ref<'asc' | 'desc'>('desc')
const page = ref(1)
const pageSize = 10

const sortedRows = computed(() => [...props.rows].sort((a, b) => {
  const left = a[sortKey.value]
  const right = b[sortKey.value]
  if (left === right) return 0
  if (left === null || left === undefined) return 1
  if (right === null || right === undefined) return -1
  return (left < right ? -1 : 1) * (sortDirection.value === 'asc' ? 1 : -1)
}))
const totalPages = computed(() => Math.max(1, Math.ceil(sortedRows.value.length / pageSize)))
const pageRows = computed(() => sortedRows.value.slice((page.value - 1) * pageSize, page.value * pageSize))

function setSort(key: keyof CampaignPerformanceRow) {
  if (sortKey.value === key) sortDirection.value = sortDirection.value === 'asc' ? 'desc' : 'asc'
  else { sortKey.value = key; sortDirection.value = 'desc' }
}
function sortLabel(key: keyof CampaignPerformanceRow): string { return `${sortKey.value === key ? (sortDirection.value === 'asc' ? 'ascending' : 'descending') : 'not sorted'}` }
function money(value: number | null, currency: string | null): string {
  if (value === null) return '—'
  return currency ? new Intl.NumberFormat(undefined, { style: 'currency', currency, maximumFractionDigits: 2 }).format(value) : new Intl.NumberFormat(undefined, { maximumFractionDigits: 2 }).format(value)
}
function number(value: number | null): string { return value === null ? '—' : new Intl.NumberFormat(undefined, { maximumFractionDigits: 2 }).format(value) }
function date(value: string | null): string {
  if (!value) return '—'
  const parsed = new Date(`${value}T00:00:00Z`)
  return Number.isNaN(parsed.valueOf()) ? value : new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeZone: 'UTC' }).format(parsed)
}
</script>

<template>
  <section class="overview-panel" aria-labelledby="campaigns-heading">
    <header class="overview-panel__header"><div><h2 id="campaigns-heading">Campaign performance</h2><p class="overview-panel__subheading">Sorted results from the selected reporting period.</p></div></header>
    <div v-if="!rows.length" class="overview-panel__empty">No campaign performance is available for this period.</div>
    <template v-else>
      <div class="overview-table-wrap">
        <table>
          <caption class="sr-only">Campaign performance, sortable by column</caption>
          <thead><tr>
            <th scope="col" :aria-sort="sortKey === 'campaignName' ? sortDirection : 'none'"><button type="button" class="sort-button" :aria-label="`Sort by campaign, ${sortLabel('campaignName')}`" @click="setSort('campaignName')">Campaign</button></th>
            <th scope="col" :aria-sort="sortKey === 'status' ? sortDirection : 'none'"><button type="button" class="sort-button" :aria-label="`Sort by status, ${sortLabel('status')}`" @click="setSort('status')">Status</button></th>
            <th scope="col" :aria-sort="sortKey === 'budget' ? sortDirection : 'none'"><button type="button" class="sort-button" :aria-label="`Sort by budget, ${sortLabel('budget')}`" @click="setSort('budget')">Budget</button></th>
            <th scope="col" :aria-sort="sortKey === 'spend' ? sortDirection : 'none'"><button type="button" class="sort-button" :aria-label="`Sort by spend, ${sortLabel('spend')}`" @click="setSort('spend')">Spend</button></th>
            <th scope="col" :aria-sort="sortKey === 'cpc' ? sortDirection : 'none'"><button type="button" class="sort-button" :aria-label="`Sort by CPC, ${sortLabel('cpc')}`" @click="setSort('cpc')">CPC</button></th>
            <th scope="col" :aria-sort="sortKey === 'cpa' ? sortDirection : 'none'"><button type="button" class="sort-button" :aria-label="`Sort by CPA, ${sortLabel('cpa')}`" @click="setSort('cpa')">CPA</button></th>
            <th scope="col" :aria-sort="sortKey === 'roas' ? sortDirection : 'none'"><button type="button" class="sort-button" :aria-label="`Sort by ROAS, ${sortLabel('roas')}`" @click="setSort('roas')">ROAS</button></th>
            <th scope="col" :aria-sort="sortKey === 'lastMetricDate' ? sortDirection : 'none'"><button type="button" class="sort-button" :aria-label="`Sort by last metric date, ${sortLabel('lastMetricDate')}`" @click="setSort('lastMetricDate')">Last metric</button></th>
          </tr></thead>
          <tbody><tr v-for="row in pageRows" :key="row.campaignId"><th scope="row">{{ row.campaignName }}</th><td>{{ row.status || '—' }}</td><td>{{ money(row.budget, row.currency) }}</td><td>{{ money(row.spend, row.currency) }}</td><td>{{ money(row.cpc, row.currency) }}</td><td>{{ money(row.cpa, row.currency) }}</td><td>{{ number(row.roas) }}</td><td>{{ date(row.lastMetricDate) }}</td></tr></tbody>
        </table>
      </div>
      <nav v-if="totalPages > 1" class="pagination" aria-label="Campaign performance pages"><button type="button" class="overview-button" :disabled="page === 1" @click="page -= 1">Previous</button><span>Page {{ page }} of {{ totalPages }}</span><button type="button" class="overview-button" :disabled="page === totalPages" @click="page += 1">Next</button></nav>
    </template>
  </section>
</template>

<style scoped>
.overview-panel { min-width: 0; border: 1px solid var(--border, #e4e7ec); border-radius: 8px; background: var(--surface, #fff); }
.overview-panel__header { padding: 18px 20px; border-bottom: 1px solid var(--border, #e4e7ec); }
.overview-panel h2 { margin: 0; color: var(--text-primary, #182230); font-size: 17px; font-weight: 600; }
.overview-panel__subheading { margin: 4px 0 0; color: var(--text-secondary, #667085); font-size: 13px; }
.overview-panel__empty { padding: 28px 20px; color: var(--text-secondary, #667085); }
.overview-table-wrap { overflow-x: auto; }
table { width: 100%; min-width: 860px; border-collapse: collapse; color: var(--text-primary, #182230); font-size: 13px; }
th, td { padding: 11px 14px; border-bottom: 1px solid var(--border, #e4e7ec); text-align: right; font-variant-numeric: tabular-nums; white-space: nowrap; }
th:first-child, td:first-child { text-align: left; }
thead th { background: var(--surface-subtle, #f8fafc); color: var(--text-secondary, #667085); font-weight: 600; }
tbody th { font-weight: 600; }
.sort-button { display: inline-flex; align-items: center; gap: 5px; min-height: 32px; margin: -6px -8px; padding: 6px 8px; border: 0; background: transparent; color: inherit; font: inherit; font-weight: inherit; cursor: pointer; }
.sort-button:focus-visible, .overview-button:focus-visible { outline: 3px solid color-mix(in srgb, var(--focus, #2e90fa) 45%, transparent); outline-offset: 2px; }
.pagination { display: flex; align-items: center; justify-content: flex-end; gap: 12px; padding: 14px 20px; color: var(--text-secondary, #667085); font-size: 13px; }
.overview-button { min-height: 36px; padding: 6px 12px; border: 1px solid var(--border, #e4e7ec); border-radius: 6px; background: var(--surface, #fff); color: var(--text-primary, #182230); font: inherit; cursor: pointer; }
.overview-button:disabled { cursor: not-allowed; opacity: .5; }
.sr-only { position: absolute; width: 1px; height: 1px; padding: 0; margin: -1px; overflow: hidden; clip: rect(0, 0, 0, 0); white-space: nowrap; border: 0; }
</style>
