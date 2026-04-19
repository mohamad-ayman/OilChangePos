import { create } from 'zustand'

type UiState = {
  /** Shown in shell until dedicated layout branding exists */
  appTitle: string
  setAppTitle: (title: string) => void
}

export const useUiStore = create<UiState>((set) => ({
  appTitle: 'OilChangePOS',
  setAppTitle: (appTitle) => set({ appTitle }),
}))
