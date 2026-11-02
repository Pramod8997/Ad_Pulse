<script setup lang="ts">
import { computed, ref, watch } from 'vue'

export type SettingKind = 'text' | 'number' | 'date' | 'select' | 'boolean'
export type SettingValue = string | number | boolean | null

export interface SettingOption {
  value: string
  label: string
}

export interface SettingDefinition {
  key: string
  label: string
  description?: string
  kind: SettingKind
  value: SettingValue
  editable?: boolean
  disabledReason?: string
  required?: boolean
  min?: number
  max?: number
  step?: number
  options?: SettingOption[]
}

const props = withDefaults(defineProps<{
  settings: SettingDefinition[]
  canEdit: boolean
  saving?: boolean
  errorMessage?: string
}>(), {
  saving: false,
  errorMessage: '',
})

const emit = defineEmits<{
  save: [values: Record<string, SettingValue>]
}>()

const draft = ref<Record<string, SettingValue>>({})
const original = ref<Record<string, SettingValue>>({})

function syncDraft(settings: SettingDefinition[]) {
  const next: Record<string, SettingValue> = {}
  for (const setting of settings) next[setting.key] = setting.value
  draft.value = { ...next }
  original.value = { ...next }
}

watch(() => props.settings, syncDraft, { immediate: true, deep: true })

const supportedSettings = computed(() => props.settings.filter(isSupported))
const hasChanges = computed(() => supportedSettings.value.some((setting) => !sameValue(draft.value[setting.key], original.value[setting.key])))
const canSubmit = computed(() => props.canEdit && !props.saving && hasChanges.value)

function isSupported(setting: SettingDefinition) {
  return ['text', 'number', 'date', 'select', 'boolean'].includes(setting.kind)
}

function isEnabled(setting: SettingDefinition) {
  return props.canEdit && setting.editable !== false && !props.saving && isSupported(setting)
}

function sameValue(left: SettingValue, right: SettingValue) {
  return left === right
}

function normalizedValue(setting: SettingDefinition): SettingValue {
  const value = draft.value[setting.key]
  if (setting.kind === 'number') {
    if (value === '' || value === null || value === undefined) return null
    const parsed = Number(value)
    return Number.isFinite(parsed) ? parsed : null
  }
  if (setting.kind === 'boolean') return Boolean(value)
  return value ?? ''
}

function save() {
  if (!canSubmit.value) return
  const values: Record<string, SettingValue> = {}
  for (const setting of supportedSettings.value) {
    const value = normalizedValue(setting)
    if (!sameValue(value, original.value[setting.key])) values[setting.key] = value
  }
  if (Object.keys(values).length > 0) emit('save', values)
}
</script>

<template>
  <form class="settings-form" @submit.prevent="save">
    <fieldset :disabled="saving">
      <legend class="sr-only">Tenant settings</legend>
      <div v-for="setting in settings" :key="setting.key" class="setting-row">
        <template v-if="isSupported(setting)">
          <label v-if="setting.kind !== 'boolean'" :for="`setting-${setting.key}`" class="setting-label">
            {{ setting.label }}
            <span v-if="setting.required" aria-hidden="true"> *</span>
          </label>
          <label v-else class="boolean-label" :for="`setting-${setting.key}`">
            <input
              :id="`setting-${setting.key}`"
              v-model="draft[setting.key]"
              type="checkbox"
              :disabled="!isEnabled(setting)"
              :aria-describedby="`setting-help-${setting.key}`"
            />
            <span>{{ setting.label }}</span>
          </label>

          <select
            v-if="setting.kind === 'select'"
            :id="`setting-${setting.key}`"
            v-model="draft[setting.key]"
            :disabled="!isEnabled(setting)"
            :required="setting.required"
            :aria-describedby="`setting-help-${setting.key}`"
          >
            <option v-for="option in setting.options ?? []" :key="option.value" :value="option.value">
              {{ option.label }}
            </option>
          </select>
          <input
            v-else-if="setting.kind !== 'boolean'"
            :id="`setting-${setting.key}`"
            v-model="draft[setting.key]"
            :type="setting.kind === 'number' ? 'number' : setting.kind"
            :disabled="!isEnabled(setting)"
            :required="setting.required"
            :min="setting.min"
            :max="setting.max"
            :step="setting.step"
            :aria-describedby="`setting-help-${setting.key}`"
          />

          <p v-if="setting.description || !isEnabled(setting)" :id="`setting-help-${setting.key}`" class="setting-help">
            {{ setting.description }}
            <span v-if="!isEnabled(setting)" class="disabled-reason">
              {{ setting.disabledReason || (canEdit ? 'This setting is read-only.' : 'Only tenant administrators can change settings.') }}
            </span>
          </p>
        </template>
        <p v-else class="unsupported-setting">
          <strong>{{ setting.label }}</strong> is available from the API but cannot be edited by this client.
        </p>
      </div>
    </fieldset>

    <p v-if="!canEdit" class="form-note" role="status">
      You have read-only access. Tenant administrator permission is required to save changes.
    </p>
    <p v-if="errorMessage" class="form-error" role="alert">{{ errorMessage }}</p>

    <div class="form-actions">
      <button type="submit" :disabled="!canSubmit" :aria-busy="saving">
        {{ saving ? 'Saving…' : 'Save changes' }}
      </button>
      <span v-if="canEdit && !hasChanges" class="form-note">There are no unsaved changes.</span>
    </div>
  </form>
</template>

<style scoped>
.settings-form {
  display: grid;
  gap: 20px;
}

fieldset {
  display: grid;
  gap: 0;
  min-width: 0;
  border: 0;
  margin: 0;
  padding: 0;
}

.setting-row {
  display: grid;
  grid-template-columns: minmax(180px, 0.7fr) minmax(240px, 1fr);
  gap: 8px 24px;
  align-items: center;
  border-bottom: 1px solid var(--border);
  padding: 16px 0;
}

.setting-row:first-child {
  padding-top: 0;
}

.setting-row:last-child {
  border-bottom: 0;
}

.setting-label,
.boolean-label {
  color: var(--text-primary);
  font-size: 14px;
  font-weight: 600;
}

.boolean-label {
  display: flex;
  align-items: center;
  gap: 10px;
  grid-column: 1 / -1;
}

input:not([type='checkbox']),
select {
  box-sizing: border-box;
  min-height: 40px;
  width: 100%;
  border: 1px solid var(--border);
  border-radius: 6px;
  background: var(--surface);
  color: var(--text-primary);
  font: inherit;
  padding: 8px 10px;
}

input[type='checkbox'] {
  width: 18px;
  height: 18px;
  accent-color: var(--accent);
}

input:focus-visible,
select:focus-visible,
button:focus-visible {
  outline: 3px solid var(--focus);
  outline-offset: 2px;
}

input:disabled,
select:disabled,
button:disabled {
  cursor: not-allowed;
  opacity: 0.6;
}

.setting-help,
.form-note,
.unsupported-setting {
  grid-column: 2;
  margin: 0;
  color: var(--text-secondary);
  font-size: 13px;
}

.setting-help {
  margin-top: -2px;
}

.disabled-reason {
  display: block;
  color: var(--warning);
  margin-top: 3px;
}

.unsupported-setting {
  grid-column: 1 / -1;
}

.form-error {
  margin: 0;
  border-left: 3px solid var(--danger);
  background: var(--surface-subtle);
  color: var(--danger);
  font-size: 13px;
  padding: 12px 14px;
}

.form-actions {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 12px;
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

.sr-only {
  position: absolute;
  width: 1px;
  height: 1px;
  padding: 0;
  margin: -1px;
  overflow: hidden;
  clip: rect(0, 0, 0, 0);
  white-space: nowrap;
  border: 0;
}

@media (max-width: 680px) {
  .setting-row {
    grid-template-columns: 1fr;
    gap: 8px;
  }

  .setting-help,
  .form-note {
    grid-column: 1;
  }
}
</style>
