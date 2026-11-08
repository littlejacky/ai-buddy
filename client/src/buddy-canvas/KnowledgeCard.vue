<script setup lang="ts">
import type { KnowledgeBlock } from './types'

defineProps<{
  block: KnowledgeBlock
}>()

const emit = defineEmits<{
  (e: 'close'): void
}>()
</script>

<template>
  <div class="knowledge-card">
    <div class="card-header">
      <div class="badge">💡 好奇心百科</div>
      <button class="close-btn" @click="emit('close')" aria-label="关闭卡片">✕</button>
    </div>
    <h3 class="card-title">{{ block.title }}</h3>
    <p class="card-content">{{ block.content }}</p>
    <div class="fact-highlight">
      <span class="icon">✨</span>
      <span>{{ block.factPoint }}</span>
    </div>
    <div v-if="block.source" class="card-source">
      来源: {{ block.source }}
    </div>
  </div>
</template>

<style scoped>
.knowledge-card {
  background: linear-gradient(135deg, #fffbeb 0%, #fef3c7 100%);
  border: 2px solid #fde68a;
  border-radius: 16px;
  padding: 16px 20px;
  color: #78350f;
  box-shadow: 0 8px 24px rgba(217, 119, 6, 0.15);
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
  background: #f59e0b;
  color: #fff;
  padding: 2px 8px;
  border-radius: 12px;
}
.close-btn {
  background: none;
  border: none;
  font-size: 16px;
  color: #92400e;
  cursor: pointer;
  padding: 4px;
}
.card-title {
  margin: 4px 0 8px;
  font-size: 17px;
  font-weight: 700;
  color: #92400e;
}
.card-content {
  margin: 0 0 12px;
  font-size: 14px;
  line-height: 1.5;
  color: #78350f;
}
.fact-highlight {
  display: flex;
  align-items: center;
  gap: 8px;
  background: rgba(255, 255, 255, 0.7);
  padding: 8px 12px;
  border-radius: 10px;
  font-size: 13px;
  font-weight: 600;
  color: #b45309;
}
.card-source {
  margin-top: 8px;
  font-size: 11px;
  color: #b45309;
  opacity: 0.8;
}
</style>
