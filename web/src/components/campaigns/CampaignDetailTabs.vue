<script setup lang="ts">
const props = defineProps<{ modelValue: 'overview' | 'performance' | 'alerts' | 'activity' }>();
const emit = defineEmits<{ 'update:modelValue': [value: 'overview' | 'performance' | 'alerts' | 'activity'] }>();
const tabs = [{ id: 'overview', label: 'Overview' }, { id: 'performance', label: 'Performance' }, { id: 'alerts', label: 'Alerts' }, { id: 'activity', label: 'Activity' }] as const;
</script>

<template>
  <div class="tabs" role="tablist" aria-label="Campaign detail sections"><button v-for="tab in tabs" :key="tab.id" type="button" role="tab" :aria-selected="props.modelValue === tab.id" :tabindex="props.modelValue === tab.id ? 0 : -1" :class="{ active: props.modelValue === tab.id }" @click="emit('update:modelValue', tab.id)">{{ tab.label }}</button></div>
</template>

<style scoped>
.tabs { display: flex; gap: 4px; border-bottom: 1px solid var(--border, #e4e7ec); overflow-x: auto; }
button { min-height: 44px; padding: 10px 14px; border: 0; border-bottom: 2px solid transparent; background: transparent; color: var(--text-secondary, #667085); cursor: pointer; font: inherit; font-weight: 600; white-space: nowrap; }
button.active { border-bottom-color: var(--accent, #176b64); color: var(--text-primary, #182230); }
button:focus-visible { outline: 3px solid color-mix(in srgb, var(--focus, #2e90fa) 35%, transparent); outline-offset: -2px; }
</style>
