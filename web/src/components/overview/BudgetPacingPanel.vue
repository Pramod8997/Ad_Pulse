<script setup lang="ts">
import type { BudgetPacingRow } from './types'

defineProps<{
  rows: BudgetPacingRow[]
  timezone: string
  currency: string | null
}>()

const formatter = new Intl.NumberFormat(undefined, { maximumFractionDigits: 2 })

function money(value: number | null, currency: string | null): string {
  if (value === null) return '—'
  return currency ? new Intl.NumberFormat(undefined, { style: 'currency', currency, maximumFractionDigits: 2 }).format(value) : formatter.format(value)
}

function percent(value: number | null): string { return value === null ? '—' : `${formatter.format(value)}%` }
function statusLabel(status: BudgetPacingRow['status']): string {
  return status === 'on-track' ? 'On track' : status === 'at-risk' ? 'At risk' : status === 'over-budget' ? 'Over budget' : 'Insufficient data'
}
</script>

<template>
  <section class="overview-panel" aria-labelledby="budget-heading">
    <header class="overview-panel__header">
      <div><h2 id="budget-heading">Budget pacing</h2><p class="overview-panel__subheading">Planned-to-date amounts use the tenant reporting calendar ({{ timezone }}).</p></div>
    </header>
    <div v-if="!rows.length" class="overview-panel__empty">No campaign budgets are available for this period.</div>
    <div v-else class="overview-table-wrap">
      <table>
        <caption class="sr-only">Campaign budget pacing</caption>
        <thead><tr><th scope="col">Campaign</th><th scope="col">Spent</th><th scope="col">Planned to date</th><th scope="col">Pacing</th><th scope="col">Remaining</th><th scope="col">Status</th></tr></thead>
        <tbody>
          <tr v-for="row in rows" :key="row.campaignId">
            <th scope="row">{{ row.campaignName }}</th>
            <td>{{ money(row.spent, currency) }}</td>
            <td>{{ money(row.plannedToDate, currency) }}</td>
            <td>{{ percent(row.pacing) }}</td>
            <td>{{ money(row.remainingBudget, currency) }}</td>
            <td><span class="status" :class="`status--${row.status}`">{{ statusLabel(row.status) }}</span></td>
          </tr>
        </tbody>
      </table>
    </div>
  </section>
</template>

<style scoped>
.overview-panel { min-width: 0; border: 1px solid var(--border, #e4e7ec); border-radius: 8px; background: var(--surface, #fff); }
.overview-panel__header { padding: 18px 20px; border-bottom: 1px solid var(--border, #e4e7ec); }
.overview-panel h2 { margin: 0; color: var(--text-primary, #182230); font-size: 17px; font-weight: 600; }
.overview-panel__subheading { margin: 4px 0 0; color: var(--text-secondary, #667085); font-size: 13px; }
.overview-table-wrap { overflow-x: auto; }
table { width: 100%; min-width: 680px; border-collapse: collapse; color: var(--text-primary, #182230); font-size: 13px; }
th, td { padding: 11px 14px; border-bottom: 1px solid var(--border, #e4e7ec); text-align: right; font-variant-numeric: tabular-nums; white-space: nowrap; }
th:first-child, td:first-child { text-align: left; }
thead th { background: var(--surface-subtle, #f8fafc); color: var(--text-secondary, #667085); font-weight: 600; }
tbody th { font-weight: 600; }
.overview-table-wrap tr:last-child th, .overview-table-wrap tr:last-child td { border-bottom: 0; }
.overview-panel__empty { padding: 28px 20px; color: var(--text-secondary, #667085); }
.status { display: inline-flex; align-items: center; gap: 6px; }
.status::before { width: 7px; height: 7px; border-radius: 50%; background: currentColor; content: ''; }
.status--on-track { color: var(--success, #16794a); }
.status--at-risk { color: var(--warning, #a15c07); }
.status--over-budget { color: var(--danger, #b42318); }
.status--insufficient-data { color: var(--text-secondary, #667085); }
.sr-only { position: absolute; width: 1px; height: 1px; padding: 0; margin: -1px; overflow: hidden; clip: rect(0, 0, 0, 0); white-space: nowrap; border: 0; }
</style>
