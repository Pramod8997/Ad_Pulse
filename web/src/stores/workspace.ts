import { ref } from 'vue'
import { defineStore } from 'pinia'

export const useWorkspaceStore = defineStore('workspace', () => {
  const selectedWorkspaceId = ref<string | null>(null)

  function selectWorkspace(workspaceId: string | null): void {
    selectedWorkspaceId.value = workspaceId?.trim() || null
  }

  return { selectedWorkspaceId, selectWorkspace }
})
