<script setup lang="ts">
import CampaignStatusBadge from './CampaignStatusBadge.vue';
import { formatDate, formatDecimal, formatMoney, type CampaignRecord } from './campaignApi';

defineProps<{ campaigns: CampaignRecord[]; busy?: boolean }>();
const emit = defineEmits<{ open: [campaign: CampaignRecord]; edit: [campaign: CampaignRecord] }>();
</script>

<template>
  <div class="table-wrap" :aria-busy="busy ? 'true' : 'false'"><table><caption class="sr-only">Campaign performance</caption><thead><tr><th scope="col">Campaign</th><th scope="col">Status</th><th scope="col" class="numeric">Budget</th><th scope="col" class="numeric">Spend</th><th scope="col" class="numeric">CPC</th><th scope="col" class="numeric">CPA</th><th scope="col" class="numeric">ROAS</th><th scope="col">Last metric</th><th scope="col"><span class="sr-only">Actions</span></th></tr></thead><tbody><tr v-for="campaign in campaigns" :key="campaign.id"><td><button class="name-button" type="button" @click="emit('open', campaign)">{{ campaign.name || 'Unnamed campaign' }}</button><span class="date-range">{{ formatDate(campaign.startsOn) }} – {{ formatDate(campaign.endsOn) }}</span></td><td><CampaignStatusBadge :status="campaign.status" /></td><td class="numeric">{{ formatMoney(campaign.budget, campaign.currency) }}</td><td class="numeric">{{ formatMoney(campaign.spend, campaign.currency) }}</td><td class="numeric">{{ formatMoney(campaign.cpc, campaign.currency) }}</td><td class="numeric">{{ formatMoney(campaign.cpa, campaign.currency) }}</td><td class="numeric">{{ formatDecimal(campaign.roas) }}</td><td>{{ formatDate(campaign.lastMetricDate) }}</td><td><details class="row-actions"><summary aria-label="Actions for campaign">Actions</summary><div class="row-actions__menu"><button type="button" @click="emit('edit', campaign)">Edit</button></div></details></td></tr></tbody></table></div>
</template>

<style scoped>
.table-wrap { overflow-x: auto; border: 1px solid var(--border, #e4e7ec); border-radius: 8px; background: var(--surface, #fff); }
table { width: 100%; min-width: 980px; border-collapse: collapse; font-size: 13px; }
th, td { padding: 13px 14px; border-bottom: 1px solid var(--border, #e4e7ec); text-align: left; vertical-align: middle; }
th { background: var(--surface-subtle, #f8fafc); color: var(--text-secondary, #667085); font-size: 12px; font-weight: 700; white-space: nowrap; }
tr:last-child td { border-bottom: 0; }
.numeric { text-align: right; font-variant-numeric: tabular-nums; white-space: nowrap; }
.name-button { display: block; padding: 0; border: 0; background: transparent; color: var(--accent, #176b64); cursor: pointer; font: inherit; font-weight: 700; text-align: left; }
.name-button:hover { color: var(--accent-hover, #12564f); text-decoration: underline; }
.name-button:focus-visible, summary:focus-visible, .row-actions button:focus-visible { outline: 3px solid color-mix(in srgb, var(--focus, #2e90fa) 35%, transparent); outline-offset: 2px; }
.date-range { display: block; margin-top: 3px; color: var(--text-secondary, #667085); font-size: 12px; }
.row-actions { position: relative; }
summary { min-height: 32px; padding: 5px 8px; border-radius: 6px; color: var(--text-secondary, #667085); cursor: pointer; list-style: none; white-space: nowrap; }
summary::-webkit-details-marker { display: none; }
.row-actions__menu { position: absolute; z-index: 2; right: 0; display: grid; min-width: 120px; padding: 4px; border: 1px solid var(--border, #e4e7ec); border-radius: 6px; background: var(--surface, #fff); box-shadow: 0 8px 24px rgb(16 24 40 / 12%); }
.row-actions button { padding: 8px; border: 0; background: transparent; color: var(--text-primary, #182230); cursor: pointer; font: inherit; text-align: left; }
.row-actions button:hover { background: var(--surface-subtle, #f8fafc); }
.sr-only { position: absolute; width: 1px; height: 1px; padding: 0; margin: -1px; overflow: hidden; clip: rect(0, 0, 0, 0); white-space: nowrap; border: 0; }
</style>
