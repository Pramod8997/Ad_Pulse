<script setup lang="ts">
import { ref } from 'vue'
import type { Component } from 'vue'

withDefaults(
  defineProps<{
    label: string
    icon?: Component
    type?: 'button' | 'submit' | 'reset'
    disabled?: boolean
    pressed?: boolean
  }>(),
  {
    type: 'button',
    disabled: false,
    pressed: undefined,
  },
)

const button = ref<HTMLButtonElement | null>(null)

defineExpose({
  focus: () => button.value?.focus(),
})
</script>

<template>
  <button
    ref="button"
    class="app-icon-button"
    :type="type"
    :aria-label="label"
    :title="label"
    :aria-pressed="pressed"
    :disabled="disabled"
  >
    <component :is="icon" v-if="icon" :size="18" :stroke-width="1.8" aria-hidden="true" />
    <slot v-else />
  </button>
</template>

<style scoped>
.app-icon-button {
  width: 40px;
  height: 40px;
  border: 1px solid transparent;
  border-radius: 6px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  background: transparent;
  color: var(--text-secondary, #667085);
  cursor: pointer;
  transition: background-color 160ms ease, color 160ms ease, border-color 160ms ease;
}

.app-icon-button:hover:not(:disabled) {
  border-color: var(--border, #e4e7ec);
  background: var(--surface-subtle, #f8fafc);
  color: var(--text-primary, #182230);
}

.app-icon-button:focus-visible {
  outline: 3px solid var(--focus, #2e90fa);
  outline-offset: 2px;
}

.app-icon-button:disabled {
  cursor: not-allowed;
  opacity: 0.56;
}
</style>
