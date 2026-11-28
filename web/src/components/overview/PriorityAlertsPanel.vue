<script setup lang="ts">
import { RouterLink } from 'vue-router'
import type { OverviewAlert } from './types'

defineProps<{ alerts: OverviewAlert[] }>()

function age(value: string | null): string {
  if (!value) return 'Age unavailable'
  const timestamp = new Date(value).valueOf()
  if (Number.isNaN(timestamp)) return 'Age unavailable'
  const minutes = Math.max(0, Math.floor((Date.now() - timestamp) / 60000))
  if (minutes < 60) return `${minutes}m ago`
  const hours = Math.floor(minutes / 60)
  if (hours < 24) return `${hours}h ago`
  return `${Math.floor(hours / 24)}d ago`
}

function severityLabel(value: OverviewAlert['severity']): string { return value.charAt(0).toUpperCase() + value.slice(1) }
</script>

<template>
  <section class="overview-panel" aria-labelledby="alerts-heading">
    <header class="overview-panel__header"><div><h2 id="alerts-heading">Priority alerts</h2><p class="overview-panel__subheading">Alerts are generated from the configured scoring rules.</p></div></header>
    <div v-if="!alerts.length" class="overview-panel__empty">No priority alerts were detected for this period.</div>
    <ul v-else class="alert-list">
      <li v-for="alert in alerts" :key="alert.id" class="alert-item">
        <RouterLink class="alert-item__link" :to="`/app/alerts?alert=${encodeURIComponent(alert.id)}`">
          <span class="severity" :class="`severity--${alert.severity}`">{{ severityLabel(alert.severity) }}</span>
          <div class="alert-item__body"><p class="alert-item__rule">{{ alert.rule }}<span v-if="alert.campaignName"> · {{ alert.campaignName }}</span></p><p class="alert-item__explanation">{{ alert.explanation }}</p></div>
          <time v-if="alert.detectedAt" class="alert-item__age" :datetime="alert.detectedAt">{{ age(alert.detectedAt) }}</time>
          <span v-else class="alert-item__age">Age unavailable</span>
        </RouterLink>
      </li>
    </ul>
  </section>
</template>

<style scoped>
.overview-panel { min-width: 0; border: 1px solid var(--border, #e4e7ec); border-radius: 8px; background: var(--surface, #fff); }
.overview-panel__header { padding: 18px 20px; border-bottom: 1px solid var(--border, #e4e7ec); }
.overview-panel h2 { margin: 0; color: var(--text-primary, #182230); font-size: 17px; font-weight: 600; }
.overview-panel__subheading { margin: 4px 0 0; color: var(--text-secondary, #667085); font-size: 13px; }
.overview-panel__empty { padding: 28px 20px; color: var(--text-secondary, #667085); }
.alert-list { display: grid; gap: 0; margin: 0; padding: 0; list-style: none; }
.alert-item { border-bottom: 1px solid var(--border, #e4e7ec); }
.alert-item:last-child { border-bottom: 0; }
.alert-item__link { display: grid; grid-template-columns: auto minmax(0, 1fr) auto; align-items: start; gap: 12px; padding: 14px 20px; color: inherit; text-decoration: none; }
.alert-item__link:hover { background: var(--surface-subtle, #f8fafc); }
.alert-item__link:focus-visible { outline: 3px solid color-mix(in srgb, var(--focus, #2e90fa) 45%, transparent); outline-offset: -3px; }
.severity { display: inline-flex; align-items: center; min-height: 24px; padding: 2px 8px; border: 1px solid currentColor; border-radius: 999px; font-size: 12px; font-weight: 600; }
.severity--critical, .severity--high { color: var(--danger, #b42318); }
.severity--medium { color: var(--warning, #a15c07); }
.severity--low, .severity--info { color: var(--info, #175cd3); }
.alert-item__body { min-width: 0; }
.alert-item__rule, .alert-item__explanation { margin: 0; }
.alert-item__rule { color: var(--text-primary, #182230); font-size: 13px; font-weight: 600; }
.alert-item__explanation { margin-top: 3px; color: var(--text-secondary, #667085); font-size: 13px; }
.alert-item__age { color: var(--text-secondary, #667085); font-size: 12px; white-space: nowrap; }
@media (max-width: 620px) { .alert-item__link { grid-template-columns: auto minmax(0, 1fr); } .alert-item__age { grid-column: 2; } }
</style>
