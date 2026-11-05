import { reactive, watch } from 'vue'
import { VOICES, type Mode, type Voice } from '../lib/types'

interface Settings {
  mode: Mode
  voice: Voice
  captions: boolean
}

const KEY = 'buddy.settings.v1'

function load(): Settings {
  const fallback: Settings = { mode: 'adult', voice: 'marin', captions: true }
  try {
    const raw = localStorage.getItem(KEY)
    if (!raw) return fallback
    const p = JSON.parse(raw) as Partial<Settings>
    return {
      mode: p.mode === 'child' ? 'child' : 'adult',
      voice: VOICES.includes(p.voice as Voice) ? (p.voice as Voice) : fallback.voice,
      captions: p.captions !== false,
    }
  } catch {
    return fallback // 隐私模式或存储被禁用时，照常运行，只是不记忆
  }
}

/** 设置持久化：记住“孩子模式”，下次打开不用再切一遍。 */
export function useSettings() {
  const settings = reactive<Settings>(load())
  watch(settings, (s) => {
    try {
      localStorage.setItem(KEY, JSON.stringify(s))
    } catch {
      /* ignore */
    }
  })
  return settings
}
