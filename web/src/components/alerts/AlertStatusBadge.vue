<script setup lang="ts">
import { CheckCircle2, CircleDot, CircleHelp, Clock3 } from 'lucide-vue-next'
import { computed } from 'vue'
import type { AlertStatus } from './types'

const props = defineProps<{ status: AlertStatus }>()

const label = computed(() => {
  if (props.status === 'acknowledged') return 'Acknowledged'
  if (props.status === 'resolved') return 'Resolved'
  if (props.status === 'open') return 'Open'
  return 'Status unavailable'
})
const icon = computed(() => {
  if (props.status === 'resolved') return CheckCircle2
  if (props.status === 'acknowledged') return Clock3
  if (props.status === 'open') return CircleDot
  return CircleHelp
})
</script>

<template>
  <span class="alert-status" :class="`status-${status}`">
    <component :is="icon" :size="16" aria-hidden="true" />
    <span>{{ label }}</span>
  </span>
</template>

<style scoped>
.alert-status { align-items: center; display: inline-flex; gap: 6px; font-size: 13px; font-weight: 600; white-space: nowrap; }
.status-open { color: var(--danger, #b42318); }
.status-acknowledged { color: var(--warning, #a15c07); }
.status-resolved { color: var(--success, #16794a); }
.status-unknown { color: var(--text-secondary, #667085); }
</style>
