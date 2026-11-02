<script setup lang="ts">
import { ref, watch } from 'vue'

export interface AuditFilterModel {
  from: string
  to: string
  actor: string
  action: string
  entity: string
}

const props = withDefaults(defineProps<{
  modelValue: AuditFilterModel
  disabled?: boolean
  validationMessage?: string
}>(), {
  disabled: false,
  validationMessage: '',
})

const emit = defineEmits<{
  'update:modelValue': [value: AuditFilterModel]
  apply: []
  reset: []
}>()

const draft = ref<AuditFilterModel>({ ...props.modelValue })

watch(() => props.modelValue, (value) => {
  draft.value = { ...value }
}, { deep: true })

function submit() {
  emit('update:modelValue', { ...draft.value })
  emit('apply')
}

function reset() {
  draft.value = { from: '', to: '', actor: '', action: '', entity: '' }
  emit('update:modelValue', { ...draft.value })
  emit('reset')
}
</script>

<template>
  <form class="audit-filters" @submit.prevent="submit">
    <div class="filter-grid">
      <div class="field">
        <label for="audit-from">From</label>
        <input id="audit-from" v-model="draft.from" type="date" :disabled="disabled" />
      </div>

      <div class="field">
        <label for="audit-to">To</label>
        <input id="audit-to" v-model="draft.to" type="date" :disabled="disabled" />
      </div>

      <div class="field">
        <label for="audit-actor">Actor</label>
        <input
          id="audit-actor"
          v-model="draft.actor"
          type="search"
          autocomplete="off"
          placeholder="Subject ID"
          :disabled="disabled"
        />
      </div>

      <div class="field">
        <label for="audit-action">Action</label>
        <input
          id="audit-action"
          v-model="draft.action"
          type="search"
          autocomplete="off"
          placeholder="e.g. campaign.updated"
          :disabled="disabled"
        />
      </div>

      <div class="field">
        <label for="audit-entity">Entity</label>
        <input
          id="audit-entity"
          v-model="draft.entity"
          type="search"
          autocomplete="off"
          placeholder="Type or ID"
          :disabled="disabled"
        />
      </div>
    </div>

    <p v-if="validationMessage" id="audit-filter-error" class="field-error" role="alert">
      {{ validationMessage }}
    </p>

    <p v-if="disabled" class="disabled-help">
      Filters are unavailable until audit access is granted.
    </p>

    <div class="filter-actions">
      <button type="submit" :disabled="disabled">Apply filters</button>
      <button type="button" class="secondary-button" :disabled="disabled" @click="reset">
        Clear filters
      </button>
    </div>
  </form>
</template>

<style scoped>
.audit-filters {
  display: grid;
  gap: 16px;
}

.filter-grid {
  display: grid;
  grid-template-columns: repeat(5, minmax(0, 1fr));
  gap: 12px;
}

.field {
  display: grid;
  gap: 6px;
  min-width: 0;
}

label {
  color: var(--text-secondary);
  font-size: 12px;
  font-weight: 600;
}

input {
  min-height: 40px;
  width: 100%;
  border: 1px solid var(--border);
  border-radius: 6px;
  background: var(--surface);
  color: var(--text-primary);
  font: inherit;
  padding: 8px 10px;
}

input:focus-visible,
button:focus-visible {
  outline: 3px solid var(--focus);
  outline-offset: 2px;
}

input:disabled,
button:disabled {
  cursor: not-allowed;
  opacity: 0.6;
}

.field-error,
.disabled-help {
  margin: 0;
  color: var(--danger);
  font-size: 13px;
}

.disabled-help {
  color: var(--text-secondary);
}

.filter-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

button {
  min-height: 40px;
  border: 1px solid var(--accent);
  border-radius: 6px;
  background: var(--accent);
  color: var(--surface);
  cursor: pointer;
  font: inherit;
  font-weight: 600;
  padding: 8px 14px;
}

button:hover:not(:disabled) {
  background: var(--accent-hover);
}

.secondary-button {
  border-color: var(--border);
  background: var(--surface);
  color: var(--text-primary);
}

.secondary-button:hover:not(:disabled) {
  background: var(--surface-subtle);
}

@media (max-width: 900px) {
  .filter-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}

@media (max-width: 520px) {
  .filter-grid {
    grid-template-columns: 1fr;
  }

  .filter-actions button {
    flex: 1 1 100%;
  }
}
</style>
