<script setup lang="ts">
import { reactive, watch } from 'vue';
import type { CampaignListFilters } from './campaignApi';

const props = defineProps<{ modelValue: CampaignListFilters; disabled?: boolean }>();
const emit = defineEmits<{ 'update:modelValue': [value: CampaignListFilters] }>();
const local = reactive({ ...props.modelValue });
let timer: ReturnType<typeof setTimeout> | undefined;

watch(() => props.modelValue, value => Object.assign(local, value), { deep: true });
watch(local, value => {
  if (timer) clearTimeout(timer);
  timer = setTimeout(() => emit('update:modelValue', { ...value }), 220);
}, { deep: true });

function clearFilters() { Object.assign(local, { search: '', status: '', startDate: '', endDate: '' }); }
</script>

<template>
  <form class="filters" role="search" aria-label="Filter campaigns" @submit.prevent>
    <div class="filter-field filter-field--search"><label for="campaign-search">Search campaigns</label><input id="campaign-search" v-model="local.search" type="search" placeholder="Search by name" :disabled="disabled" /></div>
    <div class="filter-field"><label for="campaign-status">Status</label><select id="campaign-status" v-model="local.status" :disabled="disabled"><option value="">Any status</option><option value="draft">Draft</option><option value="active">Active</option><option value="paused">Paused</option><option value="completed">Completed</option></select></div>
    <div class="filter-field"><label for="campaign-start-date">Starts after</label><input id="campaign-start-date" v-model="local.startDate" type="date" :disabled="disabled" /></div>
    <div class="filter-field"><label for="campaign-end-date">Ends before</label><input id="campaign-end-date" v-model="local.endDate" type="date" :disabled="disabled" /></div>
    <button class="button button--quiet" type="button" :disabled="disabled" @click="clearFilters">Clear</button>
  </form>
</template>

<style scoped>
.filters { display: flex; flex-wrap: wrap; align-items: end; gap: 12px; padding: 16px; border: 1px solid var(--border, #e4e7ec); border-radius: 8px; background: var(--surface, #fff); }
.filter-field { display: grid; gap: 5px; min-width: 150px; }
.filter-field--search { flex: 1 1 240px; }
label { color: var(--text-secondary, #667085); font-size: 12px; font-weight: 600; }
input, select { min-height: 40px; padding: 8px 10px; border: 1px solid var(--border, #e4e7ec); border-radius: 6px; background: var(--surface, #fff); color: var(--text-primary, #182230); font: inherit; }
input:focus-visible, select:focus-visible, button:focus-visible { outline: 3px solid color-mix(in srgb, var(--focus, #2e90fa) 35%, transparent); outline-offset: 1px; border-color: var(--focus, #2e90fa); }
button { min-height: 40px; cursor: pointer; font: inherit; }
.button--quiet { padding: 8px 12px; border: 1px solid var(--border, #e4e7ec); border-radius: 6px; background: var(--surface, #fff); color: var(--text-primary, #182230); }
button:disabled, input:disabled, select:disabled { cursor: not-allowed; opacity: .65; }
@media (max-width: 640px) { .filters { align-items: stretch; } .filter-field, .filter-field--search, .button--quiet { flex: 1 1 100%; min-width: 0; } }
</style>
