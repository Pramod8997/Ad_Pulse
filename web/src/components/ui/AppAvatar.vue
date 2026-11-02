<script setup lang="ts">
import { computed } from 'vue'

const props = withDefaults(
  defineProps<{
    name: string
    initials?: string
    size?: 'sm' | 'md'
  }>(),
  { size: 'md' },
)

const label = computed(() => {
  if (props.initials?.trim()) return props.initials.trim().slice(0, 2).toUpperCase()
  return props.name
    .split(/\s+/)
    .filter(Boolean)
    .map((part) => part[0])
    .join('')
    .slice(0, 2)
    .toUpperCase()
})
</script>

<template>
  <span class="app-avatar" :class="`app-avatar--${size}`" aria-hidden="true">{{ label }}</span>
</template>

<style scoped>
.app-avatar {
  flex: 0 0 auto;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border-radius: 50%;
  background: var(--surface-subtle, #f8fafc);
  border: 1px solid var(--border, #e4e7ec);
  color: var(--accent, #176b64);
  font-size: 12px;
  font-weight: 700;
  letter-spacing: 0.02em;
}

.app-avatar--sm {
  width: 32px;
  height: 32px;
}

.app-avatar--md {
  width: 36px;
  height: 36px;
}
</style>
