<script setup lang="ts">
import { nextTick, ref, watch } from 'vue'
import type { Turn } from '../lib/types'
import Sheet from './Sheet.vue'

const props = defineProps<{ open: boolean; turns: Turn[] }>()
defineEmits<{ close: [] }>()
const list = ref<HTMLElement | null>(null)

function time(ms: number) {
  return new Date(ms).toTimeString().slice(0, 5)
}

// 新内容到来时滚到底
watch(
  () => [props.open, props.turns.length, props.turns.at(-1)?.text],
  async () => {
    await nextTick()
    list.value?.scrollTo({ top: list.value.scrollHeight })
  },
)
</script>

<template>
  <Sheet :open="open" title="对话文字" @close="$emit('close')">
    <p v-if="!turns.length" class="empty">还没有内容。开始聊天后，你说的和 Buddy 说的都会出现在这里。</p>
    <ol v-else ref="list" class="list">
      <li v-for="t in turns" :key="t.id" :class="t.role">
        <span class="who">{{ t.role === 'user' ? '你' : 'Buddy' }} · {{ time(t.at) }}</span>
        <p v-if="t.pending && !t.text" class="pending" aria-label="正在识别">···</p>
        <p v-else>
          {{ t.text }}<span v-if="t.interrupted" class="cut"> …（被打断）</span>
        </p>
      </li>
    </ol>
    <p class="note">文字只保存在当前页面，关闭或刷新后即清空。</p>
  </Sheet>
</template>

<style scoped>
.list {
  list-style: none;
  display: flex;
  flex-direction: column;
  gap: 14px;
  max-height: 50dvh;
  overflow-y: auto;
  padding: 4px 2px 12px;
}
li {
  max-width: 88%;
  padding: 10px 14px;
  border-radius: 16px;
  background: var(--surface-2);
  font-size: var(--fs-body);
  line-height: 1.55;
  overflow-wrap: anywhere;
}
li.user {
  align-self: flex-end;
  background: color-mix(in srgb, var(--accent) 22%, var(--surface));
  border-bottom-right-radius: 5px;
}
li.assistant {
  align-self: flex-start;
  border-bottom-left-radius: 5px;
}
.who {
  display: block;
  font-size: 12.5px;
  color: var(--text-2);
  margin-bottom: 2px;
}
.pending {
  letter-spacing: 3px;
  color: var(--text-2);
}
.cut {
  color: var(--text-2);
}
.empty,
.note {
  color: var(--text-2);
  font-size: 14px;
  padding: 8px 2px;
}
</style>
