<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import CampaignFilters from '../components/campaigns/CampaignFilters.vue';
import CampaignForm from '../components/campaigns/CampaignForm.vue';
import CampaignPagination from '../components/campaigns/CampaignPagination.vue';
import CampaignTable from '../components/campaigns/CampaignTable.vue';
import {
  createCampaign,
  errorCorrelationId,
  errorMessage,
  listCampaigns,
  updateCampaign,
  type CampaignListFilters,
  type CampaignRecord,
  type CampaignWrite,
} from '../components/campaigns/campaignApi';

const route = useRoute();
const router = useRouter();
const pageSize = 20;

function querySearchValue(value: unknown): string {
  if (typeof value === 'string') return value;
  if (Array.isArray(value) && typeof value[0] === 'string') return value[0];
  return '';
}

const filters = ref<CampaignListFilters>({
  search: querySearchValue(route.query.search),
  status: '',
  startDate: '',
  endDate: '',
  sort: 'createdAt',
  direction: 'desc',
});
const campaigns = ref<CampaignRecord[]>([]);
const page = ref(1);
const totalCount = ref<number | null>(null);
const hasNext = ref(false);
const cursors = ref<Array<string | null>>([null]);
const loading = ref(true);
const error = ref<unknown>(null);
const correlationId = ref<string | null>(null);
const lastSuccessfulAt = ref<string | null>(null);
const formOpen = ref(false);
const editingCampaign = ref<CampaignRecord | null>(null);
const mutationBusy = ref(false);
const mutationError = ref<string | null>(null);
const notice = ref<string | null>(null);
let requestNumber = 0;
let controller: AbortController | undefined;

const hasPartialData = computed(() => campaigns.value.length > 0 && loading.value);
const isInitialLoading = computed(() => loading.value && campaigns.value.length === 0 && !error.value);
const errorText = computed(() => errorMessage(error.value));

async function loadCampaigns() {
  const currentRequest = ++requestNumber;
  controller?.abort();
  controller = new AbortController();
  loading.value = true;
  error.value = null;
  correlationId.value = null;
  try {
    const result = await listCampaigns(filters.value, page.value, pageSize, cursors.value[page.value] ?? null, controller.signal);
    if (currentRequest !== requestNumber) return;
    campaigns.value = result.items;
    totalCount.value = result.totalCount;
    hasNext.value = result.hasNext;
    cursors.value[page.value + 1] = result.nextCursor;
    lastSuccessfulAt.value = result.lastSuccessfulAt;
  } catch (caught) {
    if (currentRequest !== requestNumber || (caught instanceof DOMException && caught.name === 'AbortError')) return;
    error.value = caught;
    correlationId.value = errorCorrelationId(caught);
  } finally {
    if (currentRequest === requestNumber) loading.value = false;
  }
}

watch(() => route.query.search, (value) => {
  const next = querySearchValue(value);
  if (next !== filters.value.search) {
    filters.value.search = next;
  }
});

watch(filters, () => {
  page.value = 1;
  cursors.value = [null];
  const currentQuerySearch = querySearchValue(route.query.search);
  if (filters.value.search !== currentQuerySearch) {
    const nextQuery = { ...route.query };
    if (filters.value.search) {
      nextQuery.search = filters.value.search;
    } else {
      delete nextQuery.search;
    }
    router.replace({ query: nextQuery });
  }
  loadCampaigns();
}, { deep: true, immediate: true });

function goToPage(nextPage: number) {
  if (nextPage < 1 || nextPage === page.value) return;
  page.value = nextPage;
  loadCampaigns();
}

function openCreate() {
  editingCampaign.value = null;
  mutationError.value = null;
  formOpen.value = true;
}

function openEdit(campaign: CampaignRecord) {
  editingCampaign.value = campaign;
  mutationError.value = null;
  formOpen.value = true;
}

async function saveCampaign(input: CampaignWrite) {
  mutationBusy.value = true;
  mutationError.value = null;
  notice.value = null;
  try {
    if (editingCampaign.value) {
      await updateCampaign(editingCampaign.value.id, input);
      notice.value = 'Campaign changes saved.';
    } else {
      await createCampaign(input);
      notice.value = 'Campaign created.';
    }
    formOpen.value = false;
    await loadCampaigns();
  } catch (caught) {
    mutationError.value = errorMessage(caught);
    const id = errorCorrelationId(caught);
    if (id) mutationError.value += ` Reference: ${id}`;
  } finally {
    mutationBusy.value = false;
  }
}

function retry() { loadCampaigns(); }
function openDetail(campaign: CampaignRecord) { router.push(`/app/campaigns/${encodeURIComponent(campaign.id)}`); }
</script>

<template>
  <main class="page" aria-labelledby="campaigns-title">
    <header class="page-header"><div><p class="eyebrow">Workspace</p><h1 id="campaigns-title">Campaigns</h1><p class="lede">Manage campaign schedules, budgets and measured performance.</p></div><button class="primary-button" type="button" :disabled="mutationBusy" @click="openCreate">New campaign</button></header>

    <div v-if="notice" class="notice notice--success" role="status" aria-live="polite">{{ notice }}</div>
    <div v-if="mutationError" class="notice notice--error" role="alert">{{ mutationError }}</div>
    <CampaignForm v-if="formOpen" :campaign="editingCampaign" :submitting="mutationBusy" :server-error="mutationError" @submit="saveCampaign" @cancel="formOpen = false" />

    <section class="content-section" aria-labelledby="campaign-list-title">
      <div class="section-heading">
        <div>
          <h2 id="campaign-list-title">All campaigns</h2>
          <p v-if="lastSuccessfulAt" class="refresh-time">Last refreshed {{ new Date(lastSuccessfulAt).toLocaleTimeString([], { hour: 'numeric', minute: '2-digit' }) }}<span v-if="hasPartialData"> · Refreshing</span></p>
        </div>
        <div class="sort-controls">
          <label class="sort-field" for="campaign-sort">
            <span>Sort by</span>
            <select id="campaign-sort" v-model="filters.sort" :disabled="mutationBusy">
              <option value="createdAt">Created date</option>
              <option value="name">Name</option>
              <option value="status">Status</option>
              <option value="budget">Budget</option>
            </select>
          </label>
          <label class="sort-field" for="campaign-sort-direction">
            <span>Direction</span>
            <select id="campaign-sort-direction" v-model="filters.direction" :disabled="mutationBusy">
              <option value="desc">Descending</option>
              <option value="asc">Ascending</option>
            </select>
          </label>
        </div>
      </div>
      <CampaignFilters v-model="filters" :disabled="mutationBusy" />
      <div v-if="isInitialLoading" class="loading-panel" role="status" aria-live="polite" aria-label="Loading campaigns"><div v-for="index in 5" :key="index" class="skeleton-row"><span /><span /><span /><span /></div></div>
      <div v-else-if="error && !campaigns.length" class="state-panel state-panel--error" role="alert"><h3>Campaigns could not be loaded</h3><p>{{ errorText }}</p><p v-if="correlationId" class="support-reference">Reference: {{ correlationId }}</p><button class="secondary-button" type="button" @click="retry">Try again</button></div>
      <div v-else-if="!campaigns.length" class="state-panel"><h3>No campaigns match these filters</h3><p>Clear a filter or create a campaign to start tracking performance.</p><button class="secondary-button" type="button" @click="openCreate">New campaign</button></div>
      <template v-else>
        <div v-if="error" class="partial-banner" role="status"><strong>Showing the last successful results.</strong> {{ errorText }}<span v-if="correlationId"> Reference: {{ correlationId }}</span><button type="button" @click="retry">Retry</button></div>
        <CampaignTable :campaigns="campaigns" :busy="loading || mutationBusy" @open="openDetail" @edit="openEdit" />
        <CampaignPagination :page="page" :page-size="pageSize" :total-count="totalCount" :has-next="hasNext" :disabled="loading || mutationBusy" @previous="goToPage(page - 1)" @next="goToPage(page + 1)" />
      </template>
    </section>
  </main>
</template>

<style scoped>
.page { max-width: 1440px; margin: 0 auto; padding: 24px; color: var(--text-primary, #182230); }
.page-header { display: flex; align-items: flex-start; justify-content: space-between; gap: 20px; margin-bottom: 24px; }
.eyebrow { margin: 0 0 5px; color: var(--text-secondary, #667085); font-size: 12px; font-weight: 700; letter-spacing: .06em; text-transform: uppercase; }
h1 { margin: 0; font-size: clamp(24px, 3vw, 28px); line-height: 1.2; }
.lede { margin: 7px 0 0; color: var(--text-secondary, #667085); }
.primary-button, .secondary-button { min-height: 42px; padding: 9px 14px; border-radius: 6px; cursor: pointer; font: inherit; font-weight: 700; }
.primary-button { border: 1px solid var(--accent, #176b64); background: var(--accent, #176b64); color: #fff; }
.secondary-button { border: 1px solid var(--border, #e4e7ec); background: var(--surface, #fff); color: var(--text-primary, #182230); }
button:focus-visible { outline: 3px solid color-mix(in srgb, var(--focus, #2e90fa) 35%, transparent); outline-offset: 2px; }
button:disabled { cursor: not-allowed; opacity: .65; }
.content-section { display: grid; gap: 16px; }
.section-heading { display: flex; flex-wrap: wrap; align-items: flex-end; justify-content: space-between; gap: 12px; }
.section-heading h2 { margin: 0; font-size: 18px; }
.sort-controls { display: flex; flex-wrap: wrap; gap: 10px; }
.sort-field { display: grid; gap: 4px; color: var(--text-secondary, #667085); font-size: 12px; font-weight: 600; }
.sort-field select { min-height: 36px; padding: 6px 10px; border: 1px solid var(--border, #e4e7ec); border-radius: 6px; background: var(--surface, #fff); color: var(--text-primary, #182230); font: inherit; font-weight: 500; }
.sort-field select:focus-visible { outline: 3px solid color-mix(in srgb, var(--focus, #2e90fa) 35%, transparent); outline-offset: 1px; border-color: var(--focus, #2e90fa); }
.sort-field select:disabled { cursor: not-allowed; opacity: .65; }
.refresh-time { margin: 5px 0 0; color: var(--text-secondary, #667085); font-size: 12px; }
.notice, .partial-banner { padding: 11px 14px; border-radius: 6px; font-size: 13px; }
.notice { margin-bottom: 16px; }
.notice--success { border: 1px solid #a6e3c1; background: #effaf3; color: var(--success, #16794a); }
.notice--error, .partial-banner { border: 1px solid #f2b8b5; background: #fff6f5; color: var(--danger, #b42318); }
.partial-banner { display: flex; flex-wrap: wrap; align-items: center; gap: 6px; }
.partial-banner button { margin-left: auto; border: 0; background: transparent; color: inherit; cursor: pointer; font: inherit; font-weight: 700; text-decoration: underline; }
.state-panel { padding: 40px 20px; border: 1px dashed var(--border, #e4e7ec); border-radius: 8px; background: var(--surface, #fff); text-align: center; }
.state-panel h3 { margin: 0; font-size: 16px; }
.state-panel p { margin: 8px 0 16px; color: var(--text-secondary, #667085); }
.state-panel--error { border-style: solid; border-color: #f2b8b5; background: #fffafa; }
.support-reference { font-family: ui-monospace, SFMono-Regular, Consolas, monospace; font-size: 12px; }
.loading-panel { display: grid; gap: 1px; overflow: hidden; border: 1px solid var(--border, #e4e7ec); border-radius: 8px; background: var(--surface, #fff); }
.skeleton-row { display: grid; grid-template-columns: 2fr 1fr 1fr 1fr; gap: 16px; padding: 19px 14px; border-bottom: 1px solid var(--border, #e4e7ec); }
.skeleton-row span { height: 14px; border-radius: 4px; background: #eef1f4; }
@media (max-width: 600px) { .page { padding: 16px 12px; } .page-header { align-items: stretch; flex-direction: column; } .primary-button { width: 100%; } }
</style>
