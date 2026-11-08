<script setup lang="ts">
import { RECOMMENDED_VOICES, VOICES, type Mode, type Voice } from '../lib/types'
import Sheet from './Sheet.vue'

defineProps<{ open: boolean; voice: Voice; mode: Mode; captions: boolean }>()
const emit = defineEmits<{
  close: []
  'update:voice': [Voice]
  'update:captions': [boolean]
  'switch-mode': [Mode]
  'open-parent-dashboard': []
}>()
</script>

<template>
  <Sheet :open="open" title="设置" @close="emit('close')">
    <section>
      <h3>谁在聊天</h3>
      <div class="seg" role="radiogroup" aria-label="使用者">
        <button
          type="button"
          role="radio"
          :aria-checked="mode === 'adult'"
          :class="{ on: mode === 'adult' }"
          @click="emit('switch-mode', 'adult')"
        >
          成人
        </button>
        <button
          type="button"
          role="radio"
          :aria-checked="mode === 'child'"
          :class="{ on: mode === 'child' }"
          @click="emit('switch-mode', 'child')"
        >
          小朋友
        </button>
      </div>
      <p class="hint" v-if="mode === 'child'">小朋友模式：语气更温柔、内容更安全，单次聊天有时长上限。切换回成人模式需要家长验证。</p>
    </section>

    <section>
      <h3>声音</h3>
      <div class="voices" role="radiogroup" aria-label="声音">
        <button
          v-for="v in VOICES"
          :key="v"
          type="button"
          role="radio"
          :aria-checked="voice === v"
          :class="{ on: voice === v }"
          @click="emit('update:voice', v)"
        >
          {{ v }}<small v-if="RECOMMENDED_VOICES.includes(v)">推荐</small>
        </button>
      </div>
      <p class="hint">下次开始聊天时生效。</p>
    </section>

    <section>
      <label class="row">
        <span>
          <strong>在圆球下方显示字幕</strong>
          <small>聊天时同步显示最近一句话</small>
        </span>
        <input
          type="checkbox"
          role="switch"
          :checked="captions"
          @change="emit('update:captions', ($event.target as HTMLInputElement).checked)"
        />
      </label>
    </section>

    <section>
      <h3>家长中心</h3>
      <button type="button" class="parent-center-btn" @click="emit('open-parent-dashboard')">
        👨‍👩‍👧 家长成长陪伴中心（简报 / 记忆 / 权限）
      </button>
    </section>

    <section>
      <h3>隐私</h3>
      <p class="hint">
        你的语音会通过加密连接发送到云端 AI 进行处理。本应用不保存录音，对话文字只留在当前页面。
      </p>
    </section>
  </Sheet>
</template>

<style scoped>
section {
  padding: 12px 0;
  border-top: 1px solid var(--line);
}
section:first-child {
  border-top: 0;
}
h3 {
  font-size: 14px;
  color: var(--text-2);
  font-weight: 600;
  margin-bottom: 10px;
}
.hint {
  color: var(--text-2);
  font-size: 14px;
  margin-top: 10px;
}
.parent-center-btn {
  width: 100%;
  padding: 12px 14px;
  background: var(--surface-2);
  border: 1px solid var(--line);
  border-radius: 12px;
  color: var(--text-1);
  font-size: 14px;
  font-weight: 600;
  cursor: pointer;
  text-align: left;
  transition: all 0.2s;
}
.parent-center-btn:hover {
  background: var(--surface-3);
  border-color: var(--accent);
}
.seg {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 4px;
  padding: 4px;
  background: var(--surface-2);
  border-radius: 14px;
}
.seg button {
  min-height: 46px;
  border-radius: 11px;
  font-weight: 600;
}
.seg button.on {
  background: var(--accent);
  color: var(--accent-ink);
}
.voices {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(104px, 1fr));
  gap: 8px;
}
.voices button {
  min-height: 46px;
  border-radius: 12px;
  background: var(--surface-2);
  border: 2px solid transparent;
  text-transform: capitalize;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 6px;
}
.voices button.on {
  border-color: var(--accent);
}
small {
  font-size: 11.5px;
  color: var(--text-2);
}
.voices small {
  background: var(--surface);
  padding: 1px 6px;
  border-radius: 8px;
}
.row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  min-height: 48px;
}
.row span {
  display: flex;
  flex-direction: column;
}
.row input {
  width: 22px;
  height: 22px;
  accent-color: var(--accent);
}
</style>
