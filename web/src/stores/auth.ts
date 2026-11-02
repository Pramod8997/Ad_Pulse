import { computed, ref } from 'vue'
import { defineStore } from 'pinia'

export type AuthStatus = 'unknown' | 'checking' | 'authenticated' | 'unauthenticated'

export interface SignedInIdentity {
  subject: string
  tenantId: string
  displayName?: string
  email?: string
  roles: string[]
  permissions: string[]
}

function requireNonBlank(value: string, field: string): string {
  if (!value.trim()) {
    throw new TypeError(`${field} must be non-blank.`)
  }
  return value
}

export const useAuthStore = defineStore('auth', () => {
  const status = ref<AuthStatus>('unknown')
  const identity = ref<SignedInIdentity | null>(null)
  const isAuthenticated = computed(() => status.value === 'authenticated' && identity.value !== null)

  function markChecking(): void {
    status.value = 'checking'
  }

  function setIdentity(nextIdentity: SignedInIdentity): void {
    identity.value = {
      ...nextIdentity,
      subject: requireNonBlank(nextIdentity.subject, 'subject'),
      tenantId: requireNonBlank(nextIdentity.tenantId, 'tenantId'),
      roles: [...nextIdentity.roles],
      permissions: [...nextIdentity.permissions],
    }
    status.value = 'authenticated'
  }

  function markUnauthenticated(): void {
    identity.value = null
    status.value = 'unauthenticated'
  }

  function signOut(): void {
    markUnauthenticated()
  }

  function hasRole(role: string): boolean {
    return identity.value?.roles.includes(role) ?? false
  }

  function hasPermission(permission: string): boolean {
    return identity.value?.permissions.includes(permission) ?? false
  }

  return {
    status,
    identity,
    isAuthenticated,
    markChecking,
    setIdentity,
    markUnauthenticated,
    signOut,
    hasRole,
    hasPermission,
  }
})
