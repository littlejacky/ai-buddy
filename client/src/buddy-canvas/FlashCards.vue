<script setup lang="ts">
import { ref } from 'vue'
import type { FlashCardBlock } from './types'

defineProps<{
  block: FlashCardBlock
}>()

const emit = defineEmits<{
  (e: 'close'): void
}>()

const flipped = ref(false)
</script>

<template>
  <div class="flashcard-container">
    <div class="card-header">
      <div class="badge">🃏 翻转探索卡</div>
      <button class="close-btn" @click="emit('close')" aria-label="关闭卡片">✕</button>
    </div>

    <div class="card-surface" :class="{ flipped }" @click="flipped = !flipped">
      <div v-if="!flipped" class="front">
        <div class="card-icon">❓</div>
        <div class="card-text">{{ block.question }}</div>
        <div class="tip-tap">点一下翻面看看答案 👆</div>
      </div>
      <div v-else class="back">
        <div class="card-icon">💡</div>
        <div class="card-text">{{ block.answer }}</div>
        <div v-if="block.hint" class="hint">{{ block.hint }}</div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.flashcard-container {
  background: linear-gradient(135deg, #eff6ff 0%, #dbeafe 100%);
  border: 2px solid #93c5fd;
  border-radius: 16px;
  padding: 16px 20px;
  color: #1e3a8a;
  box-shadow: 0 8px 24px rgba(37, 99, 235, 0.15);
  margin: 12px 0;
  text-align: center;
}
.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 8px;
}
.badge {
  font-size: 12px;
  font-weight: 700;
  background: #2563eb;
  color: #fff;
  padding: 2px 8px;
  border-radius: 12px;
}
.close-btn {
  background: none;
  border: none;
  font-size: 16px;
  color: #1e40af;
  cursor: pointer;
  padding: 4px;
}
.card-surface {
  background: #fff;
  border: 2px solid #bfdbfe;
  border-radius: 14px;
  padding: 20px 16px;
  cursor: pointer;
  transition: transform 0.2s ease, box-shadow 0.2s ease;
  user-select: none;
}
.card-surface:hover {
  transform: translateY(-2px);
  box-shadow: 0 4px 12px rgba(37, 99, 235, 0.1);
}
.card-icon {
  font-size: 28px;
  margin-bottom: 8px;
}
.card-text {
  font-size: 16px;
  font-weight: 700;
  color: #1e40af;
  line-height: 1.4;
}
.tip-tap {
  margin-top: 10px;
  font-size: 12px;
  color: #60a5fa;
  font-weight: 600;
}
.hint {
  margin-top: 8px;
  font-size: 12px;
  color: #3b82f6;
}
</style>
