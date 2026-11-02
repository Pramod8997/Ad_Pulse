<script setup lang="ts">
import type { OverviewKpi } from './types'

defineProps<{
  kpis: OverviewKpi[]
  currency: string | null
}>()

const formatter = new Intl.NumberFormat(undefined, { maximumFractionDigits: 2 })

function displayValue(kpi: OverviewKpi, currency: string | null): string {
  if (kpi.value === null) return '—'
  if (kpi.key === 'budgetConsumed' || kpi.key === 'roas') return `${formatter.format(kpi.value)}${kpi.key === 'budgetConsumed' ? '%' : '×'}`
  if (kpi.currency || currency) {
    return new Intl.NumberFormat(undefined, { style: 'currency', currency: kpi.currency || currency || 'USD', maximumFractionDigits: 2 }).format(kpi.value)
  }
  return formatter.format(kpi.value)
}

function comparisonLabel(kpi: OverviewKpi): string | null {
  if (!kpi.comparison || kpi.comparison.value === null || !kpi.comparison.label) return null
  const direction = kpi.comparison.direction === 'up' ? 'increased' : kpi.comparison.direction === 'down' ? 'decreased' : 'unchanged'
  return `${direction} ${formatter.format(Math.abs(kpi.comparison.value))} ${kpi.comparison.label}`
}
</script>

<template>
  <section class="kpi-strip" aria-label="Key performance indicators">
    <article v-for="kpi in kpis" :key="kpi.key" class="kpi-card">
      <p class="kpi-card__label">{{ kpi.label }}</p>
      <p class="kpi-card__value" :aria-label="`${kpi.label}: ${displayValue(kpi, currency)}`">{{ displayValue(kpi, currency) }}</p>
      <p v-if="comparisonLabel(kpi)" class="kpi-card__comparison" :class="`kpi-card__comparison--${kpi.comparison?.direction || 'flat'}`">
        {{ comparisonLabel(kpi) }}
      </p>
      <p v-else class="kpi-card__comparison kpi-card__comparison--missing">No comparison period</p>
    </article>
  </section>
</template>

<style scoped>
.kpi-strip { display: grid; grid-template-columns: repeat(5, minmax(0, 1fr)); gap: 12px; }
.kpi-card { min-width: 0; padding: 16px; border: 1px solid var(--border, #e4e7ec); border-radius: 8px; background: var(--surface, #fff); }
.kpi-card__label, .kpi-card__comparison { margin: 0; color: var(--text-secondary, #667085); font-size: 13px; }
.kpi-card__value { margin: 10px 0 8px; overflow: hidden; color: var(--text-primary, #182230); font-size: 26px; font-variant-numeric: tabular-nums; font-weight: 650; text-overflow: ellipsis; white-space: nowrap; }
.kpi-card__comparison { min-height: 20px; }
.kpi-card__comparison--up { color: var(--success, #16794a); }
.kpi-card__comparison--down { color: var(--danger, #b42318); }
.kpi-card__comparison--missing { color: var(--text-secondary, #667085); }
@media (max-width: 980px) { .kpi-strip { grid-template-columns: repeat(3, minmax(0, 1fr)); } }
@media (max-width: 620px) { .kpi-strip { grid-template-columns: 1fr 1fr; } .kpi-card__value { font-size: 22px; } }
@media (max-width: 380px) { .kpi-strip { grid-template-columns: 1fr; } }
</style>
