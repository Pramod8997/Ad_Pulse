<script setup lang="ts">
import { computed, reactive, watch } from 'vue';
import type { CampaignRecord, CampaignWrite } from './campaignApi';

const props = defineProps<{ campaign?: CampaignRecord | null; submitting?: boolean; serverError?: string | null }>();
const emit = defineEmits<{ submit: [value: CampaignWrite]; cancel: [] }>();
const form = reactive({ name: '', status: 'draft', currency: '', budget: '', startsOn: '', endsOn: '' });
const touched = reactive<Record<keyof CampaignWrite, boolean>>({ name: false, status: false, currency: false, budget: false, startsOn: false, endsOn: false });

watch(() => props.campaign, value => {
  Object.assign(form, value ? { name: value.name, status: value.status, currency: value.currency, budget: value.budget == null ? '' : String(value.budget), startsOn: value.startsOn, endsOn: value.endsOn } : { name: '', status: 'draft', currency: '', budget: '', startsOn: '', endsOn: '' });
  Object.keys(touched).forEach(key => { touched[key as keyof CampaignWrite] = false; });
}, { immediate: true });

const errors = computed<Record<keyof CampaignWrite, string>>(() => ({
  name: !form.name.trim() ? 'Campaign name is required.' : form.name.length > 200 ? 'Use 200 characters or fewer.' : '',
  status: !form.status.trim() ? 'Status is required.' : '',
  currency: !/^[A-Z]{3}$/.test(form.currency) ? 'Use three uppercase letters, for example USD.' : '',
  budget: !/^\d{1,15}(\.\d{1,4})?$/.test(form.budget) ? 'Use a non-negative amount with up to four decimal places.' : '',
  startsOn: !form.startsOn ? 'Start date is required.' : '',
  endsOn: !form.endsOn ? 'End date is required.' : form.startsOn && form.endsOn < form.startsOn ? 'End date must be on or after the start date.' : '',
}));
const valid = computed(() => Object.values(errors.value).every(error => !error));
function mark(field: keyof CampaignWrite) { touched[field] = true; }
function fieldError(field: keyof CampaignWrite) { return touched[field] ? errors.value[field] : ''; }
function submit() {
  (Object.keys(touched) as Array<keyof CampaignWrite>).forEach(field => { touched[field] = true; });
  if (valid.value) emit('submit', { ...form, name: form.name.trim(), currency: form.currency.toUpperCase(), budget: Number(form.budget) });
}
</script>

<template>
  <section class="form-panel" aria-labelledby="campaign-form-title">
    <div class="form-panel__header"><div><h2 id="campaign-form-title">{{ campaign ? 'Edit campaign' : 'New campaign' }}</h2><p>Campaign identity, schedule and budget. Tenant scope comes from your signed-in membership.</p></div><button type="button" class="close-button" aria-label="Close campaign form" @click="emit('cancel')">Close</button></div>
    <form novalidate @submit.prevent="submit">
      <div class="form-grid">
        <div class="field field--wide"><label for="campaign-name">Name</label><input id="campaign-name" v-model="form.name" maxlength="200" autocomplete="off" :aria-invalid="!!fieldError('name')" :aria-describedby="fieldError('name') ? 'campaign-name-error' : undefined" @blur="mark('name')" /><span v-if="fieldError('name')" id="campaign-name-error" class="field-error">{{ fieldError('name') }}</span></div>
        <div class="field"><label for="campaign-form-status">Status</label><select id="campaign-form-status" v-model="form.status" :aria-invalid="!!fieldError('status')" @blur="mark('status')"><option value="draft">Draft</option><option value="active">Active</option><option value="paused">Paused</option><option value="completed">Completed</option></select><span v-if="fieldError('status')" class="field-error">{{ fieldError('status') }}</span></div>
        <div class="field"><label for="campaign-currency">Currency</label><input id="campaign-currency" v-model="form.currency" maxlength="3" inputmode="text" placeholder="USD" :aria-invalid="!!fieldError('currency')" @input="form.currency = form.currency.toUpperCase()" @blur="mark('currency')" /><span v-if="fieldError('currency')" class="field-error">{{ fieldError('currency') }}</span></div>
        <div class="field"><label for="campaign-budget">Budget</label><input id="campaign-budget" v-model="form.budget" inputmode="decimal" placeholder="0.00" :aria-invalid="!!fieldError('budget')" @blur="mark('budget')" /><span v-if="fieldError('budget')" class="field-error">{{ fieldError('budget') }}</span></div>
        <div class="field"><label for="campaign-starts-on">Starts on</label><input id="campaign-starts-on" v-model="form.startsOn" type="date" :aria-invalid="!!fieldError('startsOn')" @blur="mark('startsOn')" /><span v-if="fieldError('startsOn')" class="field-error">{{ fieldError('startsOn') }}</span></div>
        <div class="field"><label for="campaign-ends-on">Ends on</label><input id="campaign-ends-on" v-model="form.endsOn" type="date" :aria-invalid="!!fieldError('endsOn')" @blur="mark('endsOn')" /><span v-if="fieldError('endsOn')" class="field-error">{{ fieldError('endsOn') }}</span></div>
      </div>
      <p v-if="serverError" class="server-error" role="alert">{{ serverError }}</p>
      <div class="form-actions"><button type="button" class="button button--quiet" :disabled="submitting" @click="emit('cancel')">Cancel</button><button type="submit" class="button button--primary" :disabled="submitting">{{ submitting ? 'Saving…' : campaign ? 'Save changes' : 'Create campaign' }}</button></div>
    </form>
  </section>
</template>

<style scoped>
.form-panel { padding: 20px; border: 1px solid var(--border, #e4e7ec); border-radius: 8px; background: var(--surface, #fff); }
.form-panel__header { display: flex; align-items: flex-start; justify-content: space-between; gap: 16px; margin-bottom: 20px; }
h2 { margin: 0; color: var(--text-primary, #182230); font-size: 18px; }
.form-panel__header p { margin: 6px 0 0; color: var(--text-secondary, #667085); font-size: 13px; }
.close-button { min-height: 36px; padding: 6px 10px; border: 1px solid var(--border, #e4e7ec); border-radius: 6px; background: transparent; color: var(--text-secondary, #667085); cursor: pointer; font: inherit; }
.form-grid { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 16px; }
.field { display: grid; gap: 5px; }
.field--wide { grid-column: span 2; }
label { color: var(--text-secondary, #667085); font-size: 12px; font-weight: 600; }
input, select { min-height: 40px; padding: 8px 10px; border: 1px solid var(--border, #e4e7ec); border-radius: 6px; background: var(--surface, #fff); color: var(--text-primary, #182230); font: inherit; }
input:focus-visible, select:focus-visible, button:focus-visible { outline: 3px solid color-mix(in srgb, var(--focus, #2e90fa) 35%, transparent); outline-offset: 1px; border-color: var(--focus, #2e90fa); }
[aria-invalid="true"] { border-color: var(--danger, #b42318); }
.field-error, .server-error { color: var(--danger, #b42318); font-size: 12px; }
.server-error { margin: 18px 0 0; padding: 10px 12px; border: 1px solid #f2b8b5; border-radius: 6px; background: #fff6f5; }
.form-actions { display: flex; justify-content: flex-end; gap: 10px; margin-top: 20px; }
.button { min-height: 40px; padding: 8px 14px; border-radius: 6px; cursor: pointer; font: inherit; font-weight: 600; }
.button--quiet { border: 1px solid var(--border, #e4e7ec); background: var(--surface, #fff); color: var(--text-primary, #182230); }
.button--primary { border: 1px solid var(--accent, #176b64); background: var(--accent, #176b64); color: #fff; }
button:disabled { cursor: not-allowed; opacity: .65; }
@media (max-width: 700px) { .form-grid { grid-template-columns: 1fr; } .field--wide { grid-column: auto; } }
</style>
