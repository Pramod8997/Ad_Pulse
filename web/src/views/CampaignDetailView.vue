<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { RouterLink, useRoute, useRouter } from 'vue-router';
import CampaignDetailTabs from '../components/campaigns/CampaignDetailTabs.vue';
import CampaignForm from '../components/campaigns/CampaignForm.vue';
import CampaignMetricForm from '../components/campaigns/CampaignMetricForm.vue';
import CampaignStatusBadge from '../components/campaigns/CampaignStatusBadge.vue';
import {
  errorCorrelationId,
  errorMessage,
  formatDate,
  formatDateTime,
  formatDecimal,
  formatMoney,
  getCampaignDetail,
  scoreCampaign,
  updateCampaign,
  upsertCampaignMetric,
  type CampaignDetailResult,
  type CampaignWrite,
  type DailyMetricWrite,
} from '../components/campaigns/campaignApi';

type DetailTab = 'overview' | 'performance' | 'alerts' | 'activity';
const route = useRoute();
const router = useRouter();
const campaignId = computed(() => String(route.params.id ?? ''));
const selectedTab = ref<DetailTab>((route.query.tab as DetailTab) || 'overview');
const result = ref<CampaignDetailResult | null>(null);
const loading = ref(true);
const error = ref<unknown>(null);
const correlationId = ref<string | null>(null);
const editOpen = ref(false);
const mutationBusy = ref(false);
const mutationError = ref<string | null>(null);
const metricFormOpen = ref(false);
const metricBusy = ref(false);
const metricError = ref<string | null>(null);
const scoringBusy = ref(false);
const notice = ref<string | null>(null);
let requestNumber = 0;
let controller: AbortController | undefined;

const campaign = computed(() => result.value?.campaign ?? null);
const hasPartialData = computed(() => !!result.value && loading.value);
const partial = computed(() => result.value?.partial ?? false);
const isInitialLoading = computed(() => loading.value && !result.value && !error.value);

async function loadDetail() {
  const currentRequest = ++requestNumber;
  controller?.abort();
  controller = new AbortController();
  loading.value = true;
  error.value = null;
  correlationId.value = null;
  try {
    const response = await getCampaignDetail(campaignId.value, controller.signal);
    if (currentRequest !== requestNumber) return;
    result.value = response;
  } catch (caught) {
    if (currentRequest !== requestNumber || (caught instanceof DOMException && caught.name === 'AbortError')) return;
    error.value = caught;
    correlationId.value = errorCorrelationId(caught);
  } finally {
    if (currentRequest === requestNumber) loading.value = false;
  }
}

watch([campaignId, selectedTab], ([id, tab]) => {
  if (!id) return;
  router.replace({ query: { ...route.query, tab } });
  loadDetail();
}, { immediate: true });

function selectTab(tab: DetailTab) { selectedTab.value = tab; }
function retry() { loadDetail(); }
function goBack() { router.push('/app/campaigns'); }

async function saveCampaign(input: CampaignWrite) {
  if (!campaign.value) return;
  mutationBusy.value = true;
  mutationError.value = null;
  notice.value = null;
  try {
    await updateCampaign(campaign.value.id, input);
    editOpen.value = false;
    notice.value = 'Campaign changes saved.';
    await loadDetail();
  } catch (caught) {
    mutationError.value = errorMessage(caught);
    const id = errorCorrelationId(caught);
    if (id) mutationError.value += ` Reference: ${id}`;
  } finally {
    mutationBusy.value = false;
  }
}

function toggleMetricForm() {
  metricError.value = null;
  metricFormOpen.value = !metricFormOpen.value;
}

async function saveMetric(payload: { metricDate: string; metric: DailyMetricWrite }) {
  if (!campaign.value) return;
  metricBusy.value = true;
  metricError.value = null;
  notice.value = null;
  try {
    await upsertCampaignMetric(campaign.value.id, payload.metricDate, payload.metric);
    metricFormOpen.value = false;
    notice.value = `Daily metric for ${formatDate(payload.metricDate)} saved.`;
    await loadDetail();
  } catch (caught) {
    metricError.value = errorMessage(caught);
    const id = errorCorrelationId(caught);
    if (id) metricError.value += ` Reference: ${id}`;
  } finally {
    metricBusy.value = false;
  }
}

async function runScoring() {
  if (!campaign.value) return;
  scoringBusy.value = true;
  mutationError.value = null;
  notice.value = null;
  try {
    const outcome = await scoreCampaign(campaign.value.id);
    if (outcome.message) {
      notice.value = outcome.message;
    } else if (outcome.created === true) {
      notice.value = 'Anomaly scoring completed and created a new alert.';
    } else if (outcome.created === false) {
      notice.value = 'Anomaly scoring completed; no new alert was created.';
    } else {
      notice.value = 'Anomaly scoring completed.';
    }
    await loadDetail();
  } catch (caught) {
    mutationError.value = errorMessage(caught);
    const id = errorCorrelationId(caught);
    if (id) mutationError.value += ` Reference: ${id}`;
  } finally {
    scoringBusy.value = false;
  }
}
</script>

<template>
  <main class="page" aria-labelledby="campaign-detail-title">
    <button class="back-link" type="button" @click="goBack">Back to campaigns</button>
    <div v-if="notice" class="notice notice--success" role="status" aria-live="polite">{{ notice }}</div>
    <div v-if="mutationError" class="notice notice--error" role="alert">{{ mutationError }}</div>
    <div v-if="isInitialLoading" class="loading-panel" role="status" aria-label="Loading campaign"><span v-for="index in 6" :key="index" /></div>
    <div v-else-if="error && !campaign" class="state-panel state-panel--error" role="alert"><h1 id="campaign-detail-title">Campaign unavailable</h1><p>{{ errorMessage(error) }}</p><p v-if="correlationId" class="support-reference">Reference: {{ correlationId }}</p><button class="secondary-button" type="button" @click="retry">Try again</button></div>
    <template v-else-if="campaign && result">
      <header class="detail-header"><div><p class="eyebrow">Campaign detail</p><h1 id="campaign-detail-title">{{ campaign.name || 'Unnamed campaign' }}</h1><p class="date-range">{{ formatDate(campaign.startsOn) }} – {{ formatDate(campaign.endsOn) }}</p></div><div class="header-meta"><CampaignStatusBadge :status="campaign.status" /><div class="budget"><span>Budget</span><strong>{{ formatMoney(campaign.budget, campaign.currency) }}</strong></div><button class="primary-button" type="button" :disabled="mutationBusy || scoringBusy" :aria-busy="scoringBusy" @click="runScoring">{{ scoringBusy ? 'Running scoring…' : 'Run anomaly scoring' }}</button><button class="secondary-button" type="button" :disabled="mutationBusy || scoringBusy" @click="editOpen = true">Edit campaign</button></div></header>
      <CampaignForm v-if="editOpen" :campaign="campaign" :submitting="mutationBusy" :server-error="mutationError" @submit="saveCampaign" @cancel="editOpen = false" />
      <section class="content-section" aria-label="Campaign detail content">
        <CampaignDetailTabs :model-value="selectedTab" @update:model-value="selectTab" />
        <div v-if="error || partial" class="partial-banner" role="status"><strong>Some campaign data is unavailable.</strong><span v-if="error"> {{ errorMessage(error) }}</span><span v-else> Showing the available campaign data; missing metrics are shown as —.</span><span v-if="correlationId"> Reference: {{ correlationId }}</span><button type="button" @click="retry">Retry</button></div>
        <div v-if="hasPartialData" class="refreshing" role="status">Refreshing campaign data…</div>

        <section v-if="selectedTab === 'overview'" class="tab-content" aria-labelledby="overview-title">
          <h2 id="overview-title">Overview</h2>
          <div class="kpi-grid"><div class="kpi"><span>Spend</span><strong>{{ formatMoney(result.kpis.spend, campaign.currency) }}</strong></div><div class="kpi"><span>CPC</span><strong>{{ formatMoney(result.kpis.cpc, campaign.currency) }}</strong></div><div class="kpi"><span>CPA</span><strong>{{ formatMoney(result.kpis.cpa, campaign.currency) }}</strong></div><div class="kpi"><span>ROAS</span><strong>{{ formatDecimal(result.kpis.roas) }}</strong></div><div class="kpi"><span>Budget pacing</span><strong>{{ formatDecimal(result.kpis.budgetPacing) }}</strong></div></div>
          <div class="two-column"><section class="panel"><h3>Spend pacing</h3><div v-if="!result.metrics.length" class="empty-inline">No daily metrics are available for this campaign.</div><div v-else class="metric-list"><div v-for="metric in result.metrics.slice(-8)" :key="metric.date" class="metric-row"><span>{{ formatDate(metric.date) }}</span><strong>{{ formatMoney(metric.spend, campaign.currency) }}</strong><span>Revenue {{ formatMoney(metric.revenue, campaign.currency) }}</span></div></div></section><section class="panel"><h3>Recent alerts</h3><div v-if="!result.alerts.length" class="empty-inline">No alerts have been recorded.</div><ul v-else class="compact-list"><li v-for="alert in result.alerts.slice(0, 5)" :key="alert.id"><span class="severity" :class="`severity--${alert.severity}`">{{ alert.severity || 'Unknown' }}</span><RouterLink class="alert-link" :to="`/app/alerts?alert=${encodeURIComponent(alert.id)}`">{{ alert.title }}</RouterLink></li></ul></section></div>
        </section>

        <section v-else-if="selectedTab === 'performance'" class="tab-content" aria-labelledby="performance-title">
          <div class="tab-header">
            <div>
              <h2 id="performance-title">Performance</h2>
              <p class="muted">Daily values are shown in {{ campaign.currency }}. Missing values remain unavailable; they are not treated as zero.</p>
            </div>
            <button class="secondary-button" type="button" :aria-expanded="metricFormOpen" :disabled="mutationBusy || scoringBusy" @click="toggleMetricForm">
              {{ metricFormOpen ? 'Close metric form' : 'Record metric' }}
            </button>
          </div>
          <CampaignMetricForm v-if="metricFormOpen" :currency="campaign.currency" :starts-on="campaign.startsOn" :ends-on="campaign.endsOn" :submitting="metricBusy" :server-error="metricError" @submit="saveMetric" @cancel="metricFormOpen = false" />
          <div v-if="!result.metrics.length" class="state-panel"><h3>No performance data</h3><p>There are no metric records for this campaign yet.</p></div><div v-else class="table-wrap"><table><caption class="sr-only">Daily campaign performance</caption><thead><tr><th scope="col">Date</th><th scope="col" class="numeric">Spend</th><th scope="col" class="numeric">Revenue</th><th scope="col" class="numeric">Impressions</th><th scope="col" class="numeric">Clicks</th><th scope="col" class="numeric">Conversions</th></tr></thead><tbody><tr v-for="metric in result.metrics" :key="metric.date"><td>{{ formatDate(metric.date) }}</td><td class="numeric">{{ formatMoney(metric.spend, campaign.currency) }}</td><td class="numeric">{{ formatMoney(metric.revenue, campaign.currency) }}</td><td class="numeric">{{ metric.impressions ?? '—' }}</td><td class="numeric">{{ metric.clicks ?? '—' }}</td><td class="numeric">{{ metric.conversions ?? '—' }}</td></tr></tbody></table></div>
        </section>

        <section v-else-if="selectedTab === 'alerts'" class="tab-content" aria-labelledby="alerts-title"><h2 id="alerts-title">Alerts</h2><div v-if="!result.alerts.length" class="state-panel"><h3>No alerts for this campaign</h3><p>Recorded alerts will appear here when the alert workflow has data.</p></div><div v-else class="table-wrap"><table><caption class="sr-only">Campaign alerts</caption><thead><tr><th scope="col">Alert</th><th scope="col">Severity</th><th scope="col">Metric</th><th scope="col">Observed / baseline</th><th scope="col">Detected</th><th scope="col">Status</th></tr></thead><tbody><tr v-for="alert in result.alerts" :key="alert.id"><td><RouterLink class="alert-link" :to="`/app/alerts?alert=${encodeURIComponent(alert.id)}`">{{ alert.title }}</RouterLink></td><td><span class="severity" :class="`severity--${alert.severity}`">{{ alert.severity || 'Unknown' }}</span></td><td>{{ alert.metricName || '—' }}</td><td class="numeric">{{ formatDecimal(alert.observed) }} / {{ formatDecimal(alert.baseline) }}</td><td>{{ formatDateTime(alert.detectedAt) }}</td><td>{{ alert.status || '—' }}</td></tr></tbody></table></div></section>

        <section v-else class="tab-content" aria-labelledby="activity-title"><h2 id="activity-title">Activity</h2><div v-if="!result.activity.length" class="state-panel"><h3>No activity records</h3><p>Recorded audit activity for this campaign will appear here.</p></div><div v-else class="activity-list"><article v-for="event in result.activity" :key="event.id" class="activity-item"><div><strong>{{ event.action || 'Activity recorded' }}</strong><p>{{ event.entityType || 'Campaign' }} · {{ event.actorSubjectId || 'Workspace member' }}</p></div><time :datetime="event.occurredAt">{{ formatDateTime(event.occurredAt) }}</time></article></div></section>
      </section>
      <p v-if="result.lastSuccessfulAt" class="stale-note">Last successful refresh {{ formatDateTime(result.lastSuccessfulAt) }}.</p>
    </template>
  </main>
</template>

<style scoped>
.page { max-width: 1440px; margin: 0 auto; padding: 24px; color: var(--text-primary, #182230); }
.back-link { margin-bottom: 20px; padding: 0; border: 0; background: transparent; color: var(--accent, #176b64); cursor: pointer; font: inherit; font-weight: 700; }
.back-link:hover { text-decoration: underline; }
.back-link:focus-visible, button:focus-visible { outline: 3px solid color-mix(in srgb, var(--focus, #2e90fa) 35%, transparent); outline-offset: 2px; }
.detail-header { display: flex; align-items: flex-start; justify-content: space-between; gap: 24px; margin-bottom: 24px; }
.eyebrow { margin: 0 0 5px; color: var(--text-secondary, #667085); font-size: 12px; font-weight: 700; letter-spacing: .06em; text-transform: uppercase; }
h1 { margin: 0; font-size: clamp(24px, 3vw, 28px); line-height: 1.2; }
.date-range, .muted { color: var(--text-secondary, #667085); }
.header-meta { display: flex; align-items: center; flex-wrap: wrap; justify-content: flex-end; gap: 14px; }
.budget { display: grid; gap: 2px; padding-left: 14px; border-left: 1px solid var(--border, #e4e7ec); }
.budget span { color: var(--text-secondary, #667085); font-size: 12px; }
.budget strong { font-variant-numeric: tabular-nums; }
.primary-button { min-height: 40px; padding: 8px 13px; border: 1px solid var(--accent, #176b64); border-radius: 6px; background: var(--accent, #176b64); color: #fff; cursor: pointer; font: inherit; font-weight: 700; }
.primary-button:hover:not(:disabled) { background: var(--accent-hover, #12564f); }
.secondary-button { min-height: 40px; padding: 8px 13px; border: 1px solid var(--border, #e4e7ec); border-radius: 6px; background: var(--surface, #fff); color: var(--text-primary, #182230); cursor: pointer; font: inherit; font-weight: 700; }
.tab-header { display: flex; align-items: flex-start; justify-content: space-between; gap: 16px; flex-wrap: wrap; }
.tab-header .muted { margin: 6px 0 0; }
.alert-link { color: var(--accent, #176b64); font-weight: 600; text-decoration: none; }
.alert-link:hover { color: var(--accent-hover, #12564f); text-decoration: underline; }
.alert-link:focus-visible { outline: 3px solid color-mix(in srgb, var(--focus, #2e90fa) 35%, transparent); outline-offset: 2px; border-radius: 4px; }
button:disabled { cursor: not-allowed; opacity: .65; }
.content-section { display: grid; gap: 20px; }
.tab-content { display: grid; gap: 16px; }
h2 { margin: 0; font-size: 18px; }
h3 { margin: 0 0 14px; font-size: 15px; }
.kpi-grid { display: grid; grid-template-columns: repeat(5, minmax(0, 1fr)); gap: 12px; }
.kpi, .panel { padding: 16px; border: 1px solid var(--border, #e4e7ec); border-radius: 8px; background: var(--surface, #fff); }
.kpi { display: grid; gap: 8px; }
.kpi span { color: var(--text-secondary, #667085); font-size: 12px; }
.kpi strong { font-size: 20px; font-variant-numeric: tabular-nums; }
.two-column { display: grid; grid-template-columns: minmax(0, 1.4fr) minmax(280px, 1fr); gap: 16px; }
.metric-list, .compact-list { display: grid; gap: 0; }
.metric-row { display: grid; grid-template-columns: 1fr auto auto; gap: 16px; padding: 10px 0; border-bottom: 1px solid var(--border, #e4e7ec); color: var(--text-secondary, #667085); font-size: 13px; }
.metric-row:last-child { border-bottom: 0; }
.metric-row strong { color: var(--text-primary, #182230); }
.compact-list { margin: 0; padding: 0; list-style: none; }
.compact-list li { display: flex; align-items: center; gap: 10px; padding: 10px 0; border-bottom: 1px solid var(--border, #e4e7ec); }
.compact-list li:last-child { border-bottom: 0; }
.severity { display: inline-flex; min-width: 58px; justify-content: center; padding: 3px 7px; border-radius: 999px; background: var(--surface-subtle, #f8fafc); color: var(--text-secondary, #667085); font-size: 11px; font-weight: 700; text-transform: capitalize; }
.severity--high, .severity--critical { background: #fff1f0; color: var(--danger, #b42318); }
.severity--medium { background: #fff8eb; color: var(--warning, #a15c07); }
.severity--low { background: #effaf3; color: var(--success, #16794a); }
.table-wrap { overflow-x: auto; border: 1px solid var(--border, #e4e7ec); border-radius: 8px; background: var(--surface, #fff); }
table { width: 100%; min-width: 760px; border-collapse: collapse; font-size: 13px; }
th, td { padding: 13px 14px; border-bottom: 1px solid var(--border, #e4e7ec); text-align: left; }
th { background: var(--surface-subtle, #f8fafc); color: var(--text-secondary, #667085); font-size: 12px; font-weight: 700; white-space: nowrap; }
tr:last-child td { border-bottom: 0; }
.numeric { text-align: right; font-variant-numeric: tabular-nums; white-space: nowrap; }
.state-panel { padding: 40px 20px; border: 1px dashed var(--border, #e4e7ec); border-radius: 8px; background: var(--surface, #fff); text-align: center; }
.state-panel h3 { margin-bottom: 8px; }
.state-panel p { margin: 0; color: var(--text-secondary, #667085); }
.state-panel--error { border-style: solid; border-color: #f2b8b5; background: #fffafa; }
.support-reference { font-family: ui-monospace, SFMono-Regular, Consolas, monospace; font-size: 12px; }
.notice, .partial-banner, .refreshing { padding: 11px 14px; border-radius: 6px; font-size: 13px; }
.notice { margin-bottom: 16px; }
.notice--success { border: 1px solid #a6e3c1; background: #effaf3; color: var(--success, #16794a); }
.notice--error, .partial-banner { border: 1px solid #f2b8b5; background: #fff6f5; color: var(--danger, #b42318); }
.partial-banner { display: flex; flex-wrap: wrap; align-items: center; gap: 6px; }
.partial-banner button { margin-left: auto; border: 0; background: transparent; color: inherit; cursor: pointer; font: inherit; font-weight: 700; text-decoration: underline; }
.refreshing { color: var(--info, #175cd3); background: #eff4ff; }
.activity-list { display: grid; gap: 0; border: 1px solid var(--border, #e4e7ec); border-radius: 8px; background: var(--surface, #fff); }
.activity-item { display: flex; align-items: flex-start; justify-content: space-between; gap: 16px; padding: 16px; border-bottom: 1px solid var(--border, #e4e7ec); }
.activity-item:last-child { border-bottom: 0; }
.activity-item p { margin: 5px 0 0; color: var(--text-secondary, #667085); font-size: 13px; }
.activity-item time { color: var(--text-secondary, #667085); font-size: 12px; white-space: nowrap; }
.loading-panel { display: grid; gap: 12px; padding: 20px; border: 1px solid var(--border, #e4e7ec); border-radius: 8px; background: var(--surface, #fff); }
.loading-panel span { display: block; height: 28px; border-radius: 4px; background: #eef1f4; }
.stale-note { margin: 20px 0 0; color: var(--text-secondary, #667085); font-size: 12px; }
.empty-inline { color: var(--text-secondary, #667085); font-size: 13px; }
.sr-only { position: absolute; width: 1px; height: 1px; padding: 0; margin: -1px; overflow: hidden; clip: rect(0, 0, 0, 0); white-space: nowrap; border: 0; }
@media (max-width: 860px) { .detail-header { flex-direction: column; } .header-meta { justify-content: flex-start; } .kpi-grid { grid-template-columns: repeat(3, minmax(0, 1fr)); } .two-column { grid-template-columns: 1fr; } }
@media (max-width: 520px) { .page { padding: 16px 12px; } .kpi-grid { grid-template-columns: repeat(2, minmax(0, 1fr)); } .header-meta { align-items: flex-start; flex-direction: column; } .budget { padding-left: 0; border-left: 0; } .metric-row { grid-template-columns: 1fr auto; } .metric-row span:last-child { grid-column: span 2; } }
</style>
