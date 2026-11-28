<script setup lang="ts">
import { useQueryClient } from '@tanstack/vue-query'
import { ArrowLeft, LogOut, ShieldAlert } from 'lucide-vue-next'
import { computed } from 'vue'
import { RouterLink, useRoute, useRouter } from 'vue-router'
import { resetOidcTokenProvider } from '@/api/token-provider'
import { useAuthStore } from '@/stores/auth'
import { useWorkspaceStore } from '@/stores/workspace'

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()
const workspace = useWorkspaceStore()
const queryClient = useQueryClient()

const missingRole = computed(() => (typeof route.query.role === 'string' ? route.query.role : null))
const missingPermission = computed(() => (typeof route.query.permission === 'string' ? route.query.permission : null))
const requestedPath = computed(() => (typeof route.query.from === 'string' ? route.query.from : null))

const requirementDetail = computed(() => {
  if (missingRole.value) return `This route requires the "${missingRole.value}" workspace role.`
  if (missingPermission.value) return `This route requires the "${missingPermission.value}" permission.`
  return 'Your current session does not hold the required role or permission for this area.'
})

async function signOutAndSwitch() {
  resetOidcTokenProvider()
  auth.signOut()
  workspace.selectWorkspace(null)
  queryClient.clear()
  await router.push('/sign-in')
}
</script>

<template>
  <main class="forbidden-page" aria-labelledby="forbidden-heading">
    <section class="forbidden-card" role="alert">
      <div class="forbidden-icon" aria-hidden="true">
        <ShieldAlert :size="22" :stroke-width="1.8" />
      </div>
      <p class="forbidden-eyebrow">Authorization</p>
      <h1 id="forbidden-heading">Access denied</h1>
      <p class="forbidden-copy">{{ requirementDetail }}</p>

      <dl v-if="auth.identity || requestedPath" class="forbidden-details">
        <div v-if="requestedPath">
          <dt>Requested route</dt>
          <dd>{{ requestedPath }}</dd>
        </div>
        <div v-if="auth.identity">
          <dt>Signed-in subject</dt>
          <dd>{{ auth.identity.displayName || auth.identity.subject }}</dd>
        </div>
        <div v-if="auth.identity">
          <dt>Tenant workspace</dt>
          <dd>{{ auth.identity.tenantId }}</dd>
        </div>
      </dl>

      <p class="forbidden-note">
        Route guards reflect your local session claims; the ASP.NET API enforces all tenant membership and permission checks server-side.
      </p>

      <div class="forbidden-actions">
        <RouterLink class="forbidden-button forbidden-button--primary" to="/app">
          <ArrowLeft :size="17" :stroke-width="1.8" aria-hidden="true" />
          <span>Back to overview</span>
        </RouterLink>
        <button class="forbidden-button forbidden-button--secondary" type="button" @click="signOutAndSwitch">
          <LogOut :size="17" :stroke-width="1.8" aria-hidden="true" />
          <span>Sign out and switch account</span>
        </button>
      </div>
    </section>
  </main>
</template>

<style scoped>
.forbidden-page {
  min-height: 100vh;
  display: grid;
  place-items: center;
  padding: 24px 16px;
  background: var(--app-bg, #f5f6f8);
  color: var(--text-primary, #182230);
}

.forbidden-card {
  width: min(560px, 100%);
  padding: 28px;
  border: 1px solid var(--border, #e4e7ec);
  border-radius: var(--radius-panel, 8px);
  background: var(--surface, #fff);
}

.forbidden-icon {
  width: 42px;
  height: 42px;
  margin-bottom: 16px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border: 1px solid color-mix(in srgb, var(--danger, #b42318) 30%, var(--border, #e4e7ec));
  border-radius: 8px;
  background: color-mix(in srgb, var(--danger, #b42318) 6%, var(--surface, #fff));
  color: var(--danger, #b42318);
}

.forbidden-eyebrow {
  margin: 0 0 4px;
  color: var(--text-secondary, #667085);
  font-size: 12px;
  font-weight: 700;
  letter-spacing: 0.06em;
  text-transform: uppercase;
}

.forbidden-card h1 {
  margin: 0;
  font-size: 24px;
  line-height: 1.25;
  font-weight: 650;
}

.forbidden-copy {
  margin: 10px 0 0;
  color: var(--text-primary, #182230);
  font-size: 14px;
}

.forbidden-details {
  margin: 18px 0 0;
  padding: 12px 14px;
  display: grid;
  gap: 8px;
  border: 1px solid var(--border, #e4e7ec);
  border-radius: var(--radius-control, 6px);
  background: var(--surface-subtle, #f8fafc);
}

.forbidden-details > div {
  display: flex;
  justify-content: space-between;
  gap: 12px;
}

.forbidden-details dt {
  color: var(--text-secondary, #667085);
  font-size: 12px;
  font-weight: 600;
}

.forbidden-details dd {
  margin: 0;
  font-size: 13px;
  font-weight: 600;
  text-align: right;
  overflow-wrap: anywhere;
}

.forbidden-note {
  margin: 16px 0 0;
  color: var(--text-secondary, #667085);
  font-size: 13px;
}

.forbidden-actions {
  margin-top: 22px;
  display: flex;
  flex-wrap: wrap;
  gap: 10px;
}

.forbidden-button {
  min-height: 40px;
  padding: 8px 14px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  border: 1px solid transparent;
  border-radius: var(--radius-control, 6px);
  font: inherit;
  font-weight: 600;
  text-decoration: none;
  cursor: pointer;
}

.forbidden-button--primary {
  border-color: var(--accent, #176b64);
  background: var(--accent, #176b64);
  color: #fff;
}

.forbidden-button--primary:hover {
  border-color: var(--accent-hover, #12564f);
  background: var(--accent-hover, #12564f);
}

.forbidden-button--secondary {
  border-color: var(--border, #e4e7ec);
  background: var(--surface, #fff);
  color: var(--text-primary, #182230);
}

.forbidden-button--secondary:hover {
  background: var(--surface-subtle, #f8fafc);
}

.forbidden-button:focus-visible {
  outline: 3px solid var(--focus, #2e90fa);
  outline-offset: 2px;
}

@media (max-width: 620px) {
  .forbidden-card {
    padding: 20px 16px;
  }

  .forbidden-details > div {
    flex-direction: column;
    gap: 2px;
  }

  .forbidden-details dd {
    text-align: left;
  }

  .forbidden-actions {
    flex-direction: column;
  }
}
</style>
