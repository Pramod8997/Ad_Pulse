import { ref } from 'vue'
import { defineStore } from 'pinia'

export type DensityPreference = 'comfortable' | 'compact'

export const usePreferencesStore = defineStore('preferences', () => {
  const density = ref<DensityPreference>('comfortable')

  function setDensity(nextDensity: DensityPreference): void {
    density.value = nextDensity
  }

  return { density, setDensity }
})
