<script setup lang="ts">
export interface AuditEvent {
  id: string
  occurredAt: string
  actorSubjectId: string
  action: string
  entityType: string
  entityId: string
}

defineProps<{
  events: AuditEvent[]
  busy?: boolean
  partial?: boolean
}>()

function formatTimestamp(value: string) {
  const date = new Date(value)
  return Number.isNaN(date.valueOf())
    ? 'Unknown time'
    : new Intl.DateTimeFormat(undefined, {
      dateStyle: 'medium',
      timeStyle: 'short',
      timeZone: 'UTC',
    }).format(date)
}
</script>

<template>
  <div class="table-wrap" :aria-busy="busy">
    <p v-if="partial" class="partial-state" role="status">
      Some audit fields are unavailable in the current response. The records below are still shown.
    </p>

    <table>
      <caption class="sr-only">Tenant audit events</caption>
      <thead>
        <tr>
          <th scope="col">Time (UTC)</th>
          <th scope="col">Actor</th>
          <th scope="col">Action</th>
          <th scope="col">Entity</th>
          <th scope="col">Entity ID</th>
        </tr>
      </thead>
      <tbody v-if="busy">
        <tr v-for="row in 5" :key="row" class="skeleton-row">
          <td colspan="5"><span aria-hidden="true"></span></td>
        </tr>
      </tbody>
      <tbody v-else>
        <tr v-for="event in events" :key="event.id">
          <td data-label="Time (UTC)">{{ formatTimestamp(event.occurredAt) }}</td>
          <td data-label="Actor" class="monospace">{{ event.actorSubjectId }}</td>
          <td data-label="Action"><strong>{{ event.action }}</strong></td>
          <td data-label="Entity">{{ event.entityType }}</td>
          <td data-label="Entity ID" class="monospace">{{ event.entityId }}</td>
        </tr>
      </tbody>
    </table>
  </div>
</template>

<style scoped>
.table-wrap {
  overflow-x: auto;
  border: 1px solid var(--border);
  border-radius: 8px;
  background: var(--surface);
}

table {
  width: 100%;
  min-width: 720px;
  border-collapse: collapse;
  color: var(--text-primary);
  font-size: 13px;
}

caption {
  text-align: left;
}

th,
td {
  border-bottom: 1px solid var(--border);
  padding: 13px 16px;
  text-align: left;
  vertical-align: middle;
}

th {
  background: var(--surface-subtle);
  color: var(--text-secondary);
  font-size: 12px;
  font-weight: 600;
  letter-spacing: 0.02em;
  white-space: nowrap;
}

tbody tr:last-child td {
  border-bottom: 0;
}

.monospace {
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  overflow-wrap: anywhere;
}

.skeleton-row td {
  height: 50px;
}

.skeleton-row span {
  display: block;
  height: 12px;
  width: 70%;
  border-radius: 3px;
  background: var(--surface-subtle);
}

.partial-state {
  margin: 0;
  border-bottom: 1px solid var(--border);
  background: var(--surface-subtle);
  color: var(--warning);
  font-size: 13px;
  padding: 12px 16px;
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

@media (max-width: 720px) {
  .table-wrap {
    overflow: visible;
    border: 0;
    background: transparent;
  }

  table {
    min-width: 0;
  }

  thead {
    position: absolute;
    width: 1px;
    height: 1px;
    overflow: hidden;
    clip: rect(0, 0, 0, 0);
  }

  tbody {
    display: grid;
    gap: 10px;
  }

  tr {
    display: block;
    border: 1px solid var(--border);
    border-radius: 8px;
    background: var(--surface);
  }

  td {
    display: grid;
    grid-template-columns: 7rem minmax(0, 1fr);
    gap: 12px;
    border-bottom: 1px solid var(--border);
    padding: 10px 12px;
  }

  td:last-child {
    border-bottom: 0;
  }

  td::before {
    content: attr(data-label);
    color: var(--text-secondary);
    font-size: 12px;
    font-weight: 600;
  }

  .skeleton-row td {
    display: block;
    height: 48px;
  }

  .skeleton-row td::before {
    content: none;
  }
}
</style>
