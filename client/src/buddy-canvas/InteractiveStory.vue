<script setup lang="ts">
import { ref } from 'vue'
import type { StoryBlock, StoryChoice } from './types'

defineProps<{
  block: StoryBlock
}>()

const emit = defineEmits<{
  (e: 'choose', choice: StoryChoice): void
  (e: 'close'): void
}>()

const selectedChoice = ref<StoryChoice | null>(null)

function makeChoice(c: StoryChoice) {
  selectedChoice.value = c
  emit('choose', c)
}
</script>

<template>
  <div class="story-card">
    <div class="card-header">
      <div class="badge">📖 互动故事森林</div>
      <button class="close-btn" @click="emit('close')" aria-label="关闭故事">✕</button>
    </div>

    <p class="paragraph">{{ block.paragraph }}</p>

    <div v-if="!selectedChoice" class="choices-container">
      <div class="prompt">接下来会发生什么呢？你来选：</div>
      <button
        v-for="c in block.choices"
        :key="c.id"
        class="choice-btn"
        @click="makeChoice(c)"
      >
        {{ c.label }}
      </button>
    </div>

    <div v-else class="outcome-box">
      <div class="chosen-badge">👉 你的选择：{{ selectedChoice.label }}</div>
      <p class="outcome-text">{{ selectedChoice.outcome }}</p>
    </div>
  </div>
</template>

<style scoped>
.story-card {
  background: linear-gradient(135deg, #faf5ff 0%, #f3e8ff 100%);
  border: 2px solid #d8b4fe;
  border-radius: 16px;
  padding: 16px 20px;
  color: #581c87;
  box-shadow: 0 8px 24px rgba(147, 51, 234, 0.15);
  margin: 12px 0;
  text-align: left;
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
  background: #9333ea;
  color: #fff;
  padding: 2px 8px;
  border-radius: 12px;
}
.close-btn {
  background: none;
  border: none;
  font-size: 16px;
  color: #7e22ce;
  cursor: pointer;
  padding: 4px;
}
.paragraph {
  font-size: 15px;
  line-height: 1.6;
  margin: 6px 0 14px;
}
.prompt {
  font-size: 13px;
  font-weight: 700;
  color: #7e22ce;
  margin-bottom: 8px;
}
.choices-container {
  display: flex;
  flex-direction: column;
  gap: 8px;
}
.choice-btn {
  background: #fff;
  border: 2px solid #e9d5ff;
  border-radius: 12px;
  padding: 10px 14px;
  font-size: 14px;
  font-weight: 600;
  color: #6b21a8;
  cursor: pointer;
  text-align: left;
  transition: all 0.2s ease;
}
.choice-btn:hover {
  background: #fdf4ff;
  border-color: #c084fc;
}
.outcome-box {
  background: rgba(255, 255, 255, 0.8);
  border-radius: 12px;
  padding: 12px 14px;
  margin-top: 8px;
}
.chosen-badge {
  font-size: 13px;
  font-weight: 700;
  color: #9333ea;
  margin-bottom: 4px;
}
.outcome-text {
  font-size: 14px;
  line-height: 1.5;
  margin: 0;
  color: #581c87;
}
</style>
