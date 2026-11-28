<script setup lang="ts">
import { KeyRound, ShieldCheck } from 'lucide-vue-next'
import { computed, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { configureOidcTokenProvider, createOidcTokenProvider } from '@/api/token-provider'
import { useAuthStore } from '@/stores/auth'
import { useWorkspaceStore } from '@/stores/workspace'

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()
const workspace = useWorkspaceStore()

const token = ref('')
const subject = ref('')
const tenantId = ref('')
const displayName = ref('')
const email = ref('')
const rolesInput = ref('')
const permissionsInput = ref('')
const jwtDecodeNote = ref<string | null>(null)
const formError = ref<string | null>(null)
const submitting = ref(false)

function decodeJwtPayload(rawToken: string): Record<string, unknown> | null {
  const parts = rawToken.trim().split('.')
  if (parts.length !== 3 || !parts[1]) return null

  try {
    const normalized = parts[1].replace(/-/g, '+').replace(/_/g, '/')
    const padded = normalized.padEnd(normalized.length + ((4 - (normalized.length % 4)) % 4), '=')
    const binary = atob(padded)
    const bytes = Uint8Array.from(binary, (char) => char.charCodeAt(0))
    const parsed: unknown = JSON.parse(new TextDecoder().decode(bytes))
    return typeof parsed === 'object' && parsed !== null ? (parsed as Record<string, unknown>) : null
  } catch {
    return null
  }
}

function readStringClaim(claims: Record<string, unknown>, keys: string[]): string | null {
  for (const key of keys) {
    const value = claims[key]
    if (typeof value === 'string' && value.trim()) return value.trim()
  }
  return null
}

function readListClaim(claims: Record<string, unknown>, keys: string[], allowSpaceSplit = false): string[] | null {
  for (const key of keys) {
    const value = claims[key]
    if (Array.isArray(value)) {
      const items = value.filter((item): item is string => typeof item === 'string' && Boolean(item.trim())).map((item) => item.trim())
      if (items.length) return items
    }
    if (typeof value === 'string' && value.trim()) {
      const splitter = allowSpaceSplit && !value.includes(',') ? /[\s,]+/ : /,/
      const items = value.split(splitter).map((item) => item.trim()).filter(Boolean)
      if (items.length) return items
    }
  }
  return null
}

function parseCommaList(value: string): string[] {
  return [...new Set(value.split(',').map((item) => item.trim()).filter(Boolean))]
}

function safeRedirectPath(value: unknown): string {
  const candidate = Array.isArray(value) ? value[0] : value
  if (typeof candidate === 'string' && candidate.startsWith('/') && !candidate.startsWith('//') && !candidate.startsWith('/sign-in')) {
    return candidate
  }
  return '/app'
}

watch(token, (raw) => {
  const trimmed = raw.trim()
  if (!trimmed) {
    jwtDecodeNote.value = null
    return
  }
  const claims = decodeJwtPayload(trimmed)
  if (!claims) {
    jwtDecodeNote.value = trimmed.includes('.') ? 'Token payload could not be decoded as JWT JSON; verify fields below manually.' : null
    return
  }

  const nextSubject = readStringClaim(claims, ['sub'])
  const nextTenant = readStringClaim(claims, ['tenant_id', 'tid'])
  const nextName = readStringClaim(claims, ['name', 'preferred_username'])
  const nextEmail = readStringClaim(claims, ['email'])
  const nextRoles = readListClaim(claims, ['roles', 'role'])
  const nextPermissions = readListClaim(claims, ['permissions', 'permission', 'scp'], true)

  if (nextSubject) subject.value = nextSubject
  if (nextTenant) tenantId.value = nextTenant
  if (nextName) displayName.value = nextName
  if (nextEmail) email.value = nextEmail
  if (nextRoles) rolesInput.value = nextRoles.join(', ')
  if (nextPermissions) permissionsInput.value = nextPermissions.join(', ')
  jwtDecodeNote.value = 'JWT payload claims decoded into the fields below. Fields remain editable for local verification.'
})

const redirectTarget = computed(() => safeRedirectPath(route.query.redirect))

async function submit() {
  formError.value = null
  const trimmedToken = token.value.trim()
  const trimmedSubject = subject.value.trim()
  const trimmedTenantId = tenantId.value.trim()

  if (!trimmedToken) {
    formError.value = 'An OIDC access token is required.'
    return
  }
  if (!trimmedSubject) {
    formError.value = 'Subject (sub) is required.'
    return
  }
  if (!trimmedTenantId) {
    formError.value = 'Tenant ID is required.'
    return
  }

  submitting.value = true
  try {
    configureOidcTokenProvider(createOidcTokenProvider(() => Promise.resolve(trimmedToken)))
    auth.setIdentity({
      subject: trimmedSubject,
      tenantId: trimmedTenantId,
      displayName: displayName.value.trim() || undefined,
      email: email.value.trim() || undefined,
      roles: parseCommaList(rolesInput.value),
      permissions: parseCommaList(permissionsInput.value),
    })
    workspace.selectWorkspace(trimmedTenantId)
    await router.push(redirectTarget.value)
  } catch (error) {
    formError.value = error instanceof Error ? error.message : 'Unable to establish sign-in session.'
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <main class="auth-page" aria-labelledby="sign-in-heading">
    <section class="auth-card">
      <header class="auth-card__header">
        <p class="auth-card__brand">AdPulse</p>
        <p class="auth-card__eyebrow">Identity &amp; workspace access</p>
        <h1 id="sign-in-heading">Sign in</h1>
        <p class="auth-card__lede">Provide an OIDC bearer token and identity context to open your workspace.</p>
      </header>

      <div class="auth-notice" role="note">
        <ShieldCheck :size="18" :stroke-width="1.8" aria-hidden="true" />
        <p>
          Tokens are held strictly in memory and are never stored in <code>localStorage</code> or <code>sessionStorage</code>.
          The ASP.NET API enforces all tenant isolation and authorization rules server-side.
        </p>
      </div>

      <div v-if="formError" class="auth-error" role="alert">
        {{ formError }}
      </div>

      <form class="auth-form" novalidate @submit.prevent="submit">
        <label class="auth-field" for="oidc-token">
          <span>OIDC access token <abbr title="required">*</abbr></span>
          <textarea
            id="oidc-token"
            v-model="token"
            rows="4"
            required
            autocomplete="off"
            spellcheck="false"
            placeholder="Paste bearer JWT or local OIDC access token"
          />
          <small v-if="jwtDecodeNote" class="auth-field__hint">{{ jwtDecodeNote }}</small>
        </label>

        <div class="auth-grid">
          <label class="auth-field" for="oidc-subject">
            <span>Subject (<code>sub</code>) <abbr title="required">*</abbr></span>
            <input id="oidc-subject" v-model="subject" type="text" required autocomplete="off" placeholder="user-123" />
          </label>

          <label class="auth-field" for="oidc-tenant">
            <span>Tenant ID <abbr title="required">*</abbr></span>
            <input id="oidc-tenant" v-model="tenantId" type="text" required autocomplete="off" placeholder="tenant-acme" />
          </label>
        </div>

        <div class="auth-grid">
          <label class="auth-field" for="oidc-name">
            <span>Display name</span>
            <input id="oidc-name" v-model="displayName" type="text" autocomplete="name" placeholder="Operations Analyst" />
          </label>

          <label class="auth-field" for="oidc-email">
            <span>Email</span>
            <input id="oidc-email" v-model="email" type="email" autocomplete="email" placeholder="analyst@company.example" />
          </label>
        </div>

        <div class="auth-grid">
          <label class="auth-field" for="oidc-roles">
            <span>Roles (comma-separated)</span>
            <input id="oidc-roles" v-model="rolesInput" type="text" autocomplete="off" placeholder="tenant-admin, analyst" />
          </label>

          <label class="auth-field" for="oidc-permissions">
            <span>Permissions (comma-separated)</span>
            <input id="oidc-permissions" v-model="permissionsInput" type="text" autocomplete="off" placeholder="import, audit" />
          </label>
        </div>

        <button class="auth-submit" type="submit" :disabled="submitting">
          <KeyRound :size="17" :stroke-width="1.8" aria-hidden="true" />
          <span>{{ submitting ? 'Signing in…' : 'Sign in to workspace' }}</span>
        </button>
      </form>
    </section>
  </main>
</template>

<style scoped>
.auth-page {
  min-height: 100vh;
  display: grid;
  place-items: center;
  padding: 24px 16px;
  background: var(--app-bg, #f5f6f8);
  color: var(--text-primary, #182230);
}

.auth-card {
  width: min(620px, 100%);
  padding: 28px;
  border: 1px solid var(--border, #e4e7ec);
  border-radius: var(--radius-panel, 8px);
  background: var(--surface, #fff);
}

.auth-card__brand {
  margin: 0 0 12px;
  color: var(--accent, #176b64);
  font-size: 16px;
  font-weight: 750;
  letter-spacing: -0.02em;
}

.auth-card__eyebrow {
  margin: 0 0 4px;
  color: var(--text-secondary, #667085);
  font-size: 12px;
  font-weight: 700;
  letter-spacing: 0.06em;
  text-transform: uppercase;
}

.auth-card__header h1 {
  margin: 0;
  font-size: 26px;
  line-height: 1.2;
  font-weight: 650;
}

.auth-card__lede {
  margin: 8px 0 0;
  color: var(--text-secondary, #667085);
  font-size: 14px;
}

.auth-notice {
  margin-top: 18px;
  padding: 12px 14px;
  display: flex;
  align-items: flex-start;
  gap: 10px;
  border: 1px solid var(--border, #e4e7ec);
  border-radius: var(--radius-control, 6px);
  background: var(--surface-subtle, #f8fafc);
  color: var(--text-secondary, #667085);
  font-size: 13px;
}

.auth-notice > svg {
  flex: 0 0 auto;
  margin-top: 1px;
  color: var(--accent, #176b64);
}

.auth-notice p {
  margin: 0;
}

.auth-error {
  margin-top: 16px;
  padding: 11px 14px;
  border: 1px solid color-mix(in srgb, var(--danger, #b42318) 35%, var(--border, #e4e7ec));
  border-radius: var(--radius-control, 6px);
  background: color-mix(in srgb, var(--danger, #b42318) 5%, var(--surface, #fff));
  color: var(--danger, #b42318);
  font-size: 13px;
  font-weight: 600;
}

.auth-form {
  margin-top: 20px;
  display: grid;
  gap: 16px;
}

.auth-grid {
  display: grid;
  gap: 14px;
  grid-template-columns: repeat(2, minmax(0, 1fr));
}

.auth-field {
  display: grid;
  gap: 6px;
}

.auth-field > span {
  color: var(--text-primary, #182230);
  font-size: 13px;
  font-weight: 600;
}

.auth-field abbr {
  color: var(--danger, #b42318);
  text-decoration: none;
}

.auth-field input,
.auth-field textarea {
  width: 100%;
  min-height: 40px;
  padding: 9px 11px;
  border: 1px solid var(--border, #e4e7ec);
  border-radius: var(--radius-control, 6px);
  background: var(--surface, #fff);
  color: var(--text-primary, #182230);
  font: inherit;
}

.auth-field textarea {
  resize: vertical;
  font-family: ui-monospace, SFMono-Regular, Consolas, monospace;
  font-size: 12px;
  line-height: 1.45;
}

.auth-field input:focus-visible,
.auth-field textarea:focus-visible,
.auth-submit:focus-visible {
  outline: 3px solid var(--focus, #2e90fa);
  outline-offset: 2px;
}

.auth-field__hint {
  color: var(--text-secondary, #667085);
  font-size: 12px;
}

.auth-submit {
  min-height: 42px;
  padding: 10px 16px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  border: 1px solid var(--accent, #176b64);
  border-radius: var(--radius-control, 6px);
  background: var(--accent, #176b64);
  color: #fff;
  font: inherit;
  font-weight: 600;
  cursor: pointer;
}

.auth-submit:hover:not(:disabled) {
  border-color: var(--accent-hover, #12564f);
  background: var(--accent-hover, #12564f);
}

.auth-submit:disabled {
  cursor: not-allowed;
  opacity: 0.6;
}

@media (max-width: 620px) {
  .auth-card {
    padding: 20px 16px;
  }

  .auth-grid {
    grid-template-columns: 1fr;
  }
}
</style>
