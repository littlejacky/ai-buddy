<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import ParentGate from './components/ParentGate.vue'
import SettingsSheet from './components/SettingsSheet.vue'
import TranscriptSheet from './components/TranscriptSheet.vue'
import VoiceOrb from './components/VoiceOrb.vue'
import { useConversation } from './composables/useConversation'
import { useSettings } from './composables/useSettings'
import type { Mode } from './lib/types'

const params = new URLSearchParams(location.search)
const settings = useSettings()
const conv = useConversation({
  demo: params.has('demo'),
  demoFail: params.get('fail'),
  debug: params.has('debug'),
})

const showTranscript = ref(false)
const showSettings = ref(false)
const gate = ref<null | 'settings' | 'adult'>(null)

const isChild = computed(() => settings.mode === 'child')
const idleLike = computed(() => ['idle', 'ended', 'error'].includes(conv.state.value))

// ── 文案：同一状态，成人 / 孩子用不同语气 ──
const COPY: Record<Mode, Record<string, string>> = {
  adult: {
    idle: '想聊点什么？',
    connecting: '正在接通…',
    listening: '我在听',
    user_speaking: '嗯，你说',
    thinking: '想一想…',
    speaking: '我在说',
    reconnecting: '网络不稳，正在重连…',
    ended: '聊得不错',
    error: '',
  },
  child: {
    idle: '我们来聊聊天吧！',
    connecting: '马上就好…',
    listening: '我在听哦',
    user_speaking: '你说，我听着呢',
    thinking: '让我想一想…',
    speaking: '听我说哦',
    reconnecting: '网络有点卡，等一下哦…',
    ended: '今天聊得真开心！',
    error: '',
  },
}
const headline = computed(() => (conv.error.value && conv.state.value === 'error' ? conv.error.value.title : COPY[settings.mode][conv.state.value]))

const orbLevel = computed(() =>
  conv.state.value === 'speaking' ? conv.level.remote : conv.state.value === 'user_speaking' ? conv.level.mic : 0,
)
const orbActionable = computed(() => ['idle', 'ended', 'error', 'speaking', 'thinking'].includes(conv.state.value))
const orbLabel = computed(() => {
  if (idleLike.value) return '开始聊天'
  if (conv.state.value === 'speaking' || conv.state.value === 'thinking') return '打断 Buddy'
  return 'Buddy'
})
const orbHint = computed(() => {
  if (conv.state.value === 'speaking') return isChild.value ? '轻点我，可以打断哦' : '轻点圆球可以打断'
  if (idleLike.value && conv.state.value !== 'error') return '轻点圆球，或按下面的按钮'
  return ''
})

function onOrbPress() {
  if (idleLike.value) void begin()
  else conv.interrupt()
}

async function begin() {
  conv.muted.value && conv.toggleMute() // 新会话默认不静音
  await conv.start({ mode: settings.mode, voice: settings.voice })
}

// ── 字幕：最近一条有内容的发言 ──
const lastTurn = computed(() => [...conv.turns].reverse().find((t) => t.text))
const showCaption = computed(() => settings.captions && !idleLike.value && !!lastTurn.value)

// ── 计时 ──
function mmss(s: number) {
  return `${String(Math.floor(s / 60)).padStart(2, '0')}:${String(s % 60).padStart(2, '0')}`
}
const clock = computed(() => {
  if (conv.remaining.value !== null) return { text: `还剩 ${mmss(conv.remaining.value)}`, warn: conv.remaining.value <= 60 }
  return { text: mmss(conv.elapsed.value), warn: false }
})
const endedSummary = computed(() => {
  const s = conv.lastDuration.value
  return s ? `这次聊了 ${Math.max(1, Math.round(s / 60))} 分钟` : ''
})

// ── 模式切换 / 设置的家长门槛 ──
function chooseMode(m: Mode) {
  if (m === settings.mode) return
  if (m === 'adult' && isChild.value) gate.value = 'adult'
  else settings.mode = m
}
function openSettings() {
  if (isChild.value) gate.value = 'settings'
  else showSettings.value = true
}
function gatePassed() {
  const g = gate.value
  gate.value = null
  if (g === 'adult') settings.mode = 'adult'
  if (g === 'settings') showSettings.value = true
}

// 切换主题色时同步浏览器 UI 颜色
watch(
  () => settings.mode,
  (m) => document.querySelector('meta[name="theme-color"]')?.setAttribute('content', m === 'child' ? '#fff6e8' : '#0e0f17'),
  { immediate: true },
)
</script>

<template>
  <div class="app" :data-theme="settings.mode">
    <header class="top">
      <div class="brand">
        <span class="dot" aria-hidden="true"></span>
        <span>Buddy</span>
      </div>

      <p v-if="!idleLike" class="clock" :class="{ warn: clock.warn }" aria-live="off">{{ clock.text }}</p>
      <p v-if="conv.debugStats.value" class="debug">
        {{ conv.debugStats.value.rttMs }}ms · 抖动 {{ conv.debugStats.value.jitterMs }} · 丢包 {{ conv.debugStats.value.packetsLost }}
      </p>

      <button class="icon-btn" type="button" aria-label="设置" :disabled="!idleLike" @click="openSettings">
        <svg viewBox="0 0 24 24" aria-hidden="true">
          <path d="M4 7h10M18 7h2M4 17h2M10 17h10" />
          <circle cx="16" cy="7" r="2" />
          <circle cx="8" cy="17" r="2" />
        </svg>
      </button>
    </header>

    <main class="stage">
      <VoiceOrb
        :mode="settings.mode"
        :state="conv.state.value"
        :level="orbLevel"
        :action-label="orbLabel"
        :actionable="orbActionable"
        @press="onOrbPress"
      />

      <h1 class="headline" aria-live="polite">{{ headline }}</h1>
      <p v-if="orbHint" class="hint">{{ orbHint }}</p>
      <p v-else-if="conv.state.value === 'ended' && endedSummary" class="hint">{{ endedSummary }}</p>

      <!-- 实时字幕：只显示最近一句，完整记录在“字幕”面板里 -->
      <figure v-if="showCaption && lastTurn" class="caption" :class="lastTurn.role">
        <figcaption>{{ lastTurn.role === 'user' ? '你' : 'Buddy' }}</figcaption>
        <p>{{ lastTurn.text }}</p>
      </figure>

      <!-- 错误：说清楚怎么办，技术细节折叠起来 -->
      <div v-if="conv.state.value === 'error' && conv.error.value" class="error" role="alert">
        <p>{{ conv.error.value.hint }}</p>
        <details v-if="conv.error.value.detail">
          <summary>技术详情</summary>
          <code>{{ conv.error.value.detail }}</code>
        </details>
      </div>

      <button v-if="conv.audioBlocked.value" class="audio-blocked" type="button" @click="conv.resumeAudio()">
        🔈 点一下，打开声音
      </button>
    </main>

    <footer class="dock">
      <!-- 未在通话：选择谁来聊 + 主按钮 -->
      <template v-if="idleLike">
        <div class="seg" role="radiogroup" aria-label="谁来聊">
          <button type="button" role="radio" :aria-checked="!isChild" :class="{ on: !isChild }" @click="chooseMode('adult')">
            我来聊
          </button>
          <button type="button" role="radio" :aria-checked="isChild" :class="{ on: isChild }" @click="chooseMode('child')">
            小朋友来聊
          </button>
        </div>
        <button class="primary" type="button" @click="begin">
          {{ conv.state.value === 'error' ? '重试' : conv.state.value === 'ended' ? '再聊一次' : '开始聊天' }}
        </button>
        <p class="fine">语音会发送到云端 AI 处理；不保存录音。</p>
      </template>

      <!-- 通话中：三个按钮，各有文字标签 -->
      <div v-else class="call">
        <button class="ctl" type="button" :aria-pressed="conv.muted.value" @click="conv.toggleMute()">
          <span class="ico" :class="{ on: conv.muted.value }">
            <svg viewBox="0 0 24 24" aria-hidden="true">
              <path d="M12 3a3 3 0 0 0-3 3v6a3 3 0 0 0 6 0V6a3 3 0 0 0-3-3z" />
              <path d="M19 11a7 7 0 0 1-14 0M12 18v3" />
              <path v-if="conv.muted.value" d="M4 4l16 16" />
            </svg>
          </span>
          <span class="lbl">{{ conv.muted.value ? '已静音' : '静音' }}</span>
        </button>

        <button class="ctl end" type="button" @click="conv.stop()">
          <span class="ico">
            <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M6 6l12 12M18 6L6 18" /></svg>
          </span>
          <span class="lbl">结束</span>
        </button>

        <button class="ctl" type="button" @click="showTranscript = true">
          <span class="ico">
            <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M21 12a8 8 0 0 1-11.6 7.1L4 20l1-4.6A8 8 0 1 1 21 12z" /></svg>
          </span>
          <span class="lbl">全部文字</span>
        </button>
      </div>
    </footer>

    <TranscriptSheet :open="showTranscript" :turns="conv.turns" @close="showTranscript = false" />
    <SettingsSheet
      :open="showSettings"
      :voice="settings.voice"
      :mode="settings.mode"
      :captions="settings.captions"
      @close="showSettings = false"
      @update:voice="settings.voice = $event"
      @update:captions="settings.captions = $event"
      @switch-mode="chooseMode"
    />
    <ParentGate :open="gate !== null" @close="gate = null" @pass="gatePassed" />
  </div>
</template>

<style scoped>
.app {
  height: 100dvh;
  max-width: 560px;
  margin: 0 auto;
  display: grid;
  grid-template-rows: auto 1fr auto;
  padding: env(safe-area-inset-top) 20px 0;
  background: var(--bg);
  color: var(--text);
  transition: background 0.3s;
}
/* 让主题色铺满整个视口，而不只是 560px 的列 */
:global(body) {
  background: var(--bg);
}

.top {
  display: flex;
  align-items: center;
  gap: 12px;
  min-height: 64px;
}
.brand {
  display: flex;
  align-items: center;
  gap: 8px;
  font-weight: 700;
  font-size: 18px;
  letter-spacing: 0.2px;
}
.dot {
  width: 12px;
  height: 12px;
  border-radius: 50%;
  background: linear-gradient(135deg, var(--orb-a), var(--orb-c));
}
.clock {
  margin-left: auto;
  font-variant-numeric: tabular-nums;
  font-size: 15px;
  color: var(--text-2);
  padding: 4px 12px;
  border-radius: 999px;
  background: var(--surface);
}
.clock.warn {
  color: var(--danger-ink);
  background: var(--danger);
}
.debug {
  font-size: 11px;
  color: var(--text-2);
}
.icon-btn {
  margin-left: auto;
  width: 48px;
  height: 48px;
  border-radius: 50%;
  display: grid;
  place-items: center;
  color: var(--text-2);
}
.clock + .icon-btn,
.debug + .icon-btn {
  margin-left: 0;
}
.icon-btn:hover:not(:disabled) {
  background: var(--surface);
  color: var(--text);
}
.icon-btn:disabled {
  opacity: 0.3;
}
svg {
  width: 24px;
  height: 24px;
  fill: none;
  stroke: currentColor;
  stroke-width: 2;
  stroke-linecap: round;
  stroke-linejoin: round;
}

.stage {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 8px;
  text-align: center;
  min-height: 0;
  padding-bottom: 8px;
}
.headline {
  font-size: var(--fs-head);
  font-weight: 700;
  letter-spacing: 0.3px;
  margin-top: 12px;
  min-height: 1.4em;
}
.hint {
  color: var(--text-2);
  font-size: 15px;
  min-height: 1.4em;
}
.caption {
  margin-top: 12px;
  width: 100%;
  padding: 14px 18px;
  border-radius: var(--radius);
  background: var(--surface);
  text-align: left;
  max-height: 22dvh;
  overflow: hidden;
}
.caption figcaption {
  font-size: 12.5px;
  color: var(--text-2);
  margin-bottom: 2px;
}
.caption p {
  font-size: var(--fs-caption);
  line-height: 1.5;
  display: -webkit-box;
  -webkit-line-clamp: 4;
  line-clamp: 4;
  -webkit-box-orient: vertical;
  overflow: hidden;
}
.caption.user p {
  color: var(--text-2);
}
.error {
  width: 100%;
  padding: 14px 18px;
  border-radius: var(--radius);
  background: var(--surface);
  border: 1px solid color-mix(in srgb, var(--danger) 55%, transparent);
  text-align: left;
  font-size: var(--fs-body);
}
.error details {
  margin-top: 8px;
  font-size: 13px;
  color: var(--text-2);
}
.error summary {
  cursor: pointer;
  min-height: 28px;
}
.error code {
  display: block;
  margin-top: 4px;
  word-break: break-all;
}
.audio-blocked {
  margin-top: 8px;
  min-height: 48px;
  padding: 0 22px;
  border-radius: 999px;
  background: var(--accent);
  color: var(--accent-ink);
  font-weight: 650;
}

.dock {
  padding: 8px 0 calc(20px + env(safe-area-inset-bottom));
  display: flex;
  flex-direction: column;
  gap: 12px;
}
.seg {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 4px;
  padding: 4px;
  background: var(--surface);
  border-radius: 999px;
}
.seg button {
  min-height: 46px;
  border-radius: 999px;
  font-weight: 600;
  color: var(--text-2);
}
.seg button.on {
  background: var(--surface-2);
  color: var(--text);
  box-shadow: 0 0 0 2px var(--accent) inset;
}
.primary {
  min-height: 62px;
  border-radius: 999px;
  background: var(--accent);
  color: var(--accent-ink);
  font-size: 20px;
  font-weight: 700;
  box-shadow: 0 8px 24px color-mix(in srgb, var(--accent) 35%, transparent);
  transition: transform 0.1s;
}
.primary:active {
  transform: scale(0.98);
}
.fine {
  text-align: center;
  font-size: 13px;
  color: var(--text-2);
}

.call {
  display: grid;
  grid-template-columns: 1fr 1fr 1fr;
  align-items: start;
  justify-items: center;
  padding-top: 8px;
}
.ctl {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 6px;
  min-width: 88px;
  min-height: 44px;
}
.ico {
  width: 64px;
  height: 64px;
  border-radius: 50%;
  display: grid;
  place-items: center;
  background: var(--surface);
  border: 1px solid var(--line);
  transition:
    background 0.15s,
    transform 0.1s;
}
.ctl:active .ico {
  transform: scale(0.94);
}
.ico.on {
  background: var(--text);
  color: var(--bg);
}
.ctl.end .ico {
  width: 76px;
  height: 76px;
  background: var(--danger);
  color: var(--danger-ink);
  border: 0;
}
.lbl {
  font-size: 14px;
  color: var(--text-2);
}
</style>
