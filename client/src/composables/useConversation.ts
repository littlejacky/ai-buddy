import { computed, onBeforeUnmount, reactive, ref } from 'vue'
import { MockClient } from '../lib/mockClient'
import { RealtimeClient } from '../lib/realtimeClient'
import type {
  DebugStats,
  SessionState,
  StartOptions,
  Turn,
  UserFacingError,
  VoiceClient,
  VoiceClientEvents,
} from '../lib/types'

const WRAP_UP_LEAD_S = 30 // 到时前 30 秒让 AI 自然收尾
const HARD_STOP_AFTER_S = 25 // 收尾后再给 25 秒，仍未结束就强制挂断

export interface ConversationOptions {
  demo: boolean
  demoFail: string | null
  debug: boolean
}

/** 把 VoiceClient 的事件流收敛成 UI 需要的响应式状态。 */
export function useConversation(opts: ConversationOptions) {
  const state = ref<SessionState>('idle')
  const turns = reactive<Turn[]>([])
  const level = reactive({ mic: 0, remote: 0 })
  const muted = ref(false)
  const error = ref<UserFacingError | null>(null)
  const audioBlocked = ref(false)
  const elapsed = ref(0)
  const maxSeconds = ref(0)
  const lastDuration = ref(0)
  const debugStats = ref<DebugStats | null>(null)
  const wrapped = ref(false)

  let clockTimer: ReturnType<typeof setInterval> | null = null

  const events: VoiceClientEvents = {
    onState: (s) => {
      state.value = s
      if (s === 'listening' && !clockTimer) startClock()
      if (s === 'ended' || s === 'error') stopClock()
    },
    onTurn: (p) => {
      const i = turns.findIndex((t) => t.id === p.id)
      if (i === -1) {
        turns.push({ text: '', at: Date.now(), ...p })
      } else {
        Object.assign(turns[i], p)
      }
    },
    onLevel: (l) => {
      level.mic = l.mic
      level.remote = l.remote
    },
    onError: (e) => {
      error.value = e
    },
    onSessionInfo: (i) => {
      maxSeconds.value = i.maxSeconds
    },
    onAudioBlocked: (b) => {
      audioBlocked.value = b
    },
    onDebug: opts.debug ? (d) => (debugStats.value = d) : undefined,
  }

  const client: VoiceClient = opts.demo ? new MockClient(events, opts.demoFail) : new RealtimeClient(events)

  function startClock() {
    stopClock()
    elapsed.value = 0
    wrapped.value = false
    clockTimer = setInterval(() => {
      if (state.value === 'reconnecting') return // 断线重连期间不计时
      elapsed.value++
      const max = maxSeconds.value
      if (max > 0) {
        if (elapsed.value >= max - WRAP_UP_LEAD_S && !wrapped.value) {
          wrapped.value = true
          client.wrapUp()
        }
        if (elapsed.value >= max + HARD_STOP_AFTER_S) stop()
      }
    }, 1000)
  }

  function stopClock() {
    if (clockTimer) clearInterval(clockTimer)
    clockTimer = null
    if (elapsed.value > 0) lastDuration.value = elapsed.value
  }

  async function start(o: StartOptions) {
    error.value = null
    turns.splice(0)
    elapsed.value = 0
    await client.start(o)
  }

  function stop() {
    client.stop()
  }

  function toggleMute() {
    muted.value = !muted.value
    client.setMuted(muted.value)
  }

  onBeforeUnmount(() => {
    client.stop()
    stopClock()
  })

  const inSession = computed(() => !['idle', 'ended', 'error'].includes(state.value))
  const remaining = computed(() => (maxSeconds.value > 0 ? Math.max(0, maxSeconds.value - elapsed.value) : null))

  return {
    state,
    turns,
    level,
    muted,
    error,
    audioBlocked,
    elapsed,
    remaining,
    lastDuration,
    debugStats,
    inSession,
    start,
    stop,
    toggleMute,
    interrupt: () => client.interrupt(),
    resumeAudio: () => client.resumeAudio(),
  }
}
