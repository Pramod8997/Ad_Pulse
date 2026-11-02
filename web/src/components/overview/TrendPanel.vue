<script setup lang="ts">
import { computed, ref } from 'vue'
import type { OverviewTrendPoint } from './types'

const props = defineProps<{
  points: OverviewTrendPoint[]
  timezone: string
  currency: string | null
}>()

const showSpend = ref(true)
const showRevenue = ref(true)
const chartWidth = 720
const chartHeight = 220
const padding = { top: 18, right: 20, bottom: 30, left: 52 }

const values = computed(() => props.points.flatMap((point) => [
  showSpend.value ? point.spend : null,
  showRevenue.value ? point.revenue : null,
]).filter((value): value is number => value !== null))

const maxValue = computed(() => Math.max(...values.value, 1))

function x(index: number): number {
  const width = chartWidth - padding.left - padding.right
  return padding.left + (props.points.length <= 1 ? width / 2 : (index / (props.points.length - 1)) * width)
}

function y(value: number): number {
  const height = chartHeight - padding.top - padding.bottom
  return padding.top + height - (value / maxValue.value) * height
}

function linePath(key: 'spend' | 'revenue'): string {
  return props.points.reduce((path, point, index) => {
    const value = point[key]
    if (value === null) return path
    const command = path ? 'L' : 'M'
    return `${path}${command} ${x(index).toFixed(2)} ${y(value).toFixed(2)} `
  }, '')
}

const yTicks = computed(() => [0, 0.5, 1].map((ratio) => ({
  value: maxValue.value * ratio,
  y: y(maxValue.value * ratio),
})))

const xLabels = computed(() => props.points.reduce<{ date: string; x: number }[]>((labels, point, index) => {
  if (props.points.length <= 5 || index === 0 || index === props.points.length - 1 || index % Math.ceil(props.points.length / 4) === 0) {
    labels.push({ date: point.date, x: x(index) })
  }
  return labels
}, []))

const numberFormatter = computed(() => new Intl.NumberFormat(undefined, {
  style: props.currency ? 'currency' : 'decimal',
  currency: props.currency || undefined,
  maximumFractionDigits: 0,
}))

function formatValue(value: number | null): string { return value === null ? '—' : numberFormatter.value.format(value) }
function formatDate(value: string): string {
  const date = new Date(`${value}T00:00:00`)
  return Number.isNaN(date.valueOf()) ? value : new Intl.DateTimeFormat(undefined, { month: 'short', day: 'numeric', timeZone: props.timezone }).format(date)
}
function pointSummary(point: OverviewTrendPoint): string {
  return `${formatDate(point.date)}: spend ${formatValue(point.spend)}, revenue ${formatValue(point.revenue)}`
}
</script>

<template>
  <section class="overview-panel trend-panel" aria-labelledby="trend-heading">
    <header class="overview-panel__header">
      <div>
        <h2 id="trend-heading">Daily trend</h2>
        <p class="overview-panel__subheading">Reporting dates shown in {{ timezone }}</p>
      </div>
      <div class="trend-panel__toggles" aria-label="Trend series">
        <label><input v-model="showSpend" type="checkbox" /> Spend</label>
        <label><input v-model="showRevenue" type="checkbox" /> Revenue</label>
      </div>
    </header>

    <div v-if="!points.length" class="overview-panel__empty">No daily metrics are available for this period.</div>
    <template v-else>
      <div class="trend-chart-wrap">
        <svg class="trend-chart" :viewBox="`0 0 ${chartWidth} ${chartHeight}`" role="img" aria-labelledby="trend-chart-title trend-chart-description">
          <title id="trend-chart-title">Daily spend and revenue trend</title>
          <desc id="trend-chart-description">Line chart with {{ points.length }} reporting dates. A table below provides the exact values.</desc>
          <g aria-hidden="true">
            <line v-for="tick in yTicks" :key="tick.y" class="trend-chart__grid" :x1="padding.left" :x2="chartWidth - padding.right" :y1="tick.y" :y2="tick.y" />
            <text v-for="tick in yTicks" :key="`label-${tick.y}`" class="trend-chart__axis-label" :x="padding.left - 8" :y="tick.y + 4" text-anchor="end">{{ formatValue(tick.value) }}</text>
            <path v-if="showSpend" class="trend-chart__line trend-chart__line--spend" :d="linePath('spend')" />
            <path v-if="showRevenue" class="trend-chart__line trend-chart__line--revenue" :d="linePath('revenue')" />
            <text v-for="label in xLabels" :key="label.x" class="trend-chart__axis-label" :x="label.x" :y="chartHeight - 8" text-anchor="middle">{{ formatDate(label.date) }}</text>
          </g>
        </svg>
      </div>
      <details class="trend-data">
        <summary>View exact trend data</summary>
        <div class="overview-table-wrap">
          <table>
            <caption class="sr-only">Exact daily spend and revenue values</caption>
            <thead><tr><th scope="col">Date</th><th scope="col">Spend</th><th scope="col">Revenue</th></tr></thead>
            <tbody><tr v-for="point in points" :key="point.date"><th scope="row">{{ formatDate(point.date) }}</th><td>{{ formatValue(point.spend) }}</td><td>{{ formatValue(point.revenue) }}</td></tr></tbody>
          </table>
        </div>
      </details>
      <p class="sr-only">{{ points.map(pointSummary).join('. ') }}</p>
    </template>
  </section>
</template>

<style scoped>
.overview-panel { min-width: 0; border: 1px solid var(--border, #e4e7ec); border-radius: 8px; background: var(--surface, #fff); }
.overview-panel__header { display: flex; align-items: flex-start; justify-content: space-between; gap: 16px; padding: 18px 20px; border-bottom: 1px solid var(--border, #e4e7ec); }
.overview-panel h2 { margin: 0; color: var(--text-primary, #182230); font-size: 17px; font-weight: 600; }
.overview-panel__subheading { margin: 4px 0 0; color: var(--text-secondary, #667085); font-size: 13px; }
.trend-panel__toggles { display: flex; flex-wrap: wrap; gap: 12px; color: var(--text-secondary, #667085); font-size: 13px; }
.trend-panel__toggles label { display: inline-flex; align-items: center; gap: 6px; min-height: 32px; cursor: pointer; }
.trend-panel__toggles input { accent-color: var(--accent, #176b64); }
.trend-chart-wrap { padding: 16px 20px 0; overflow-x: auto; }
.trend-chart { display: block; width: 100%; min-width: 520px; height: auto; }
.trend-chart__grid { stroke: var(--border, #e4e7ec); stroke-width: 1; }
.trend-chart__axis-label { fill: var(--text-secondary, #667085); font-size: 11px; }
.trend-chart__line { fill: none; stroke-width: 2.5; stroke-linecap: round; stroke-linejoin: round; }
.trend-chart__line--spend { stroke: var(--accent, #176b64); }
.trend-chart__line--revenue { stroke: var(--info, #175cd3); stroke-dasharray: 5 4; }
.trend-data { margin: 8px 20px 18px; color: var(--text-secondary, #667085); font-size: 13px; }
.trend-data summary { cursor: pointer; }
.overview-table-wrap { overflow-x: auto; margin-top: 10px; }
table { width: 100%; border-collapse: collapse; color: var(--text-primary, #182230); }
th, td { padding: 9px 10px; border-bottom: 1px solid var(--border, #e4e7ec); text-align: left; font-variant-numeric: tabular-nums; }
th { background: var(--surface-subtle, #f8fafc); font-weight: 600; }
.overview-panel__empty { padding: 28px 20px; color: var(--text-secondary, #667085); }
.sr-only { position: absolute; width: 1px; height: 1px; padding: 0; margin: -1px; overflow: hidden; clip: rect(0, 0, 0, 0); white-space: nowrap; border: 0; }
@media (max-width: 620px) { .overview-panel__header { display: block; } .trend-panel__toggles { margin-top: 12px; } }
</style>
