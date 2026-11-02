<script setup lang="ts">
import { AlertCircle, CircleAlert, ShieldAlert, TriangleAlert } from 'lucide-vue-next'
import { computed } from 'vue'
import type { AlertSeverity } from './types'

const props = defineProps<{ severity: AlertSeverity }>()

const label = computed(() => props.severity === 'unknown' ? 'Severity unavailable' : `${props.severity[0].toUpperCase()}${props.severity.slice(1)} severity`)
const icon = computed(() => {
  if (props.severity === 'critical') return ShieldAlert
  if (props.severity === 'high') return TriangleAlert
  if (props.severity === 'medium') return CircleAlert
  return AlertCircle
})
</script>

<template>
  <span class="alert-severity" :class="`severity-${severity}`">
    <component :is="icon" :size="16" aria-hidden="true" />
    <span>{{ label }}</span>
  </span>
</template>

<style scoped>
.alert-severity { align-items: center; display: inline-flex; gap: 6px; font-size: 13px; font-weight: 600; white-space: nowrap; }
.severity-critical, .severity-high { color: var(--danger, #b42318); }
.severity-medium { color: var(--warning, #a15c07); }
.severity-low, .severity-none { color: var(--info, #175cd3); }
.severity-unknown { color: var(--text-secondary, #667085); }
</style>
