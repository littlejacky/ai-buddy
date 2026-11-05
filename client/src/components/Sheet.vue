<script setup lang="ts">
import { ref, watch, nextTick } from 'vue'

// 基于原生 <dialog>：自带焦点陷阱、Esc 关闭、背景遮罩，无需额外库。
const props = defineProps<{ open: boolean; title: string }>()
const emit = defineEmits<{ close: [] }>()
const el = ref<HTMLDialogElement | null>(null)

watch(
  () => props.open,
  async (v) => {
    await nextTick()
    if (!el.value) return
    if (v && !el.value.open) el.value.showModal()
    if (!v && el.value.open) el.value.close()
  },
)

function onBackdrop(e: MouseEvent) {
  if (e.target === el.value) emit('close')
}
</script>

<template>
  <dialog ref="el" class="sheet" :aria-label="title" @close="emit('close')" @click="onBackdrop">
    <div class="panel">
      <div class="grab" aria-hidden="true"></div>
      <header>
        <h2>{{ title }}</h2>
        <button class="close" type="button" aria-label="关闭" @click="emit('close')">✕</button>
      </header>
      <div class="body"><slot /></div>
    </div>
  </dialog>
</template>

<style scoped>
.sheet {
  border: 0;
  padding: 0;
  background: transparent;
  color: var(--text);
  width: 100%;
  max-width: 560px;
  margin: auto auto 0;
  max-height: 85dvh;
}
.sheet::backdrop {
  background: rgba(0, 0, 0, 0.5);
}
.panel {
  background: var(--surface);
  border-radius: var(--radius) var(--radius) 0 0;
  padding: 8px 20px calc(20px + env(safe-area-inset-bottom));
  max-height: 85dvh;
  display: flex;
  flex-direction: column;
  box-shadow: 0 -8px 40px rgba(0, 0, 0, 0.25);
  animation: up 0.22s ease-out;
}
.grab {
  width: 40px;
  height: 4px;
  border-radius: 2px;
  background: var(--line);
  margin: 4px auto 8px;
}
header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding-bottom: 8px;
}
h2 {
  font-size: 18px;
  font-weight: 650;
}
.close {
  width: 44px;
  height: 44px;
  border-radius: 50%;
  color: var(--text-2);
  font-size: 18px;
}
.close:hover {
  background: var(--surface-2);
}
.body {
  overflow-y: auto;
  overscroll-behavior: contain;
}
@keyframes up {
  from {
    transform: translateY(24px);
    opacity: 0;
  }
}
</style>
