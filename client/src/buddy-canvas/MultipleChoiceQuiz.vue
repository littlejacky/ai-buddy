<script setup lang="ts">
import { ref } from 'vue'
import type { QuizBlock } from './types'

const props = defineProps<{
  block: QuizBlock
}>()

const emit = defineEmits<{
  (e: 'respond', selectedId: string, isCorrect: boolean): void
  (e: 'close'): void
}>()

const selected = ref<string | null>(null)
const answered = ref(false)

function choose(id: string) {
  if (answered.value) return
  selected.value = id
  answered.value = true
  const correct = id === props.block.correctOptionId
  emit('respond', id, correct)
}
</script>

<template>
  <div class="quiz-card">
    <div class="card-header">
      <div class="badge">❓ 互动小挑战</div>
      <button class="close-btn" @click="emit('close')" aria-label="关闭问答">✕</button>
    </div>
    <h3 class="question">{{ block.question }}</h3>
    
    <div class="options">
      <button
        v-for="opt in block.options"
        :key="opt.id"
        class="opt-btn"
        :class="{
          selected: selected === opt.id,
          correct: answered && opt.id === block.correctOptionId,
          wrong: answered && selected === opt.id && opt.id !== block.correctOptionId
        }"
        @click="choose(opt.id)"
      >
        <span class="opt-label">{{ opt.label }}</span>
        <span v-if="answered && opt.id === block.correctOptionId" class="mark">✓</span>
        <span v-else-if="answered && selected === opt.id && opt.id !== block.correctOptionId" class="mark">✕</span>
      </button>
    </div>

    <div v-if="answered" class="feedback">
      <div v-if="selected === block.correctOptionId" class="feedback-text correct-msg">
        🎉 太棒啦！你答对了！
      </div>
      <div v-else class="feedback-text wrong-msg">
        💡 没关系哦！再来看看解释吧：
      </div>
      <p class="explanation">{{ block.explanation }}</p>
    </div>
  </div>
</template>

<style scoped>
.quiz-card {
  background: linear-gradient(135deg, #f0fdf4 0%, #dcfce7 100%);
  border: 2px solid #86efac;
  border-radius: 16px;
  padding: 16px 20px;
  color: #14532d;
  box-shadow: 0 8px 24px rgba(22, 163, 74, 0.15);
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
  background: #16a34a;
  color: #fff;
  padding: 2px 8px;
  border-radius: 12px;
}
.close-btn {
  background: none;
  border: none;
  font-size: 16px;
  color: #166534;
  cursor: pointer;
  padding: 4px;
}
.question {
  margin: 4px 0 12px;
  font-size: 16px;
  font-weight: 700;
}
.options {
  display: flex;
  flex-direction: column;
  gap: 8px;
}
.opt-btn {
  display: flex;
  justify-content: space-between;
  align-items: center;
  background: #fff;
  border: 2px solid #bbf7d0;
  border-radius: 12px;
  padding: 10px 14px;
  font-size: 14px;
  font-weight: 600;
  color: #166534;
  cursor: pointer;
  transition: all 0.2s ease;
}
.opt-btn:hover:not(:disabled) {
  border-color: #22c55e;
  background: #f0fdf4;
}
.opt-btn.correct {
  background: #22c55e;
  color: #fff;
  border-color: #16a34a;
}
.opt-btn.wrong {
  background: #ef4444;
  color: #fff;
  border-color: #dc2626;
}
.feedback {
  margin-top: 12px;
  padding-top: 12px;
  border-top: 1px dashed #86efac;
}
.feedback-text {
  font-size: 14px;
  font-weight: 700;
  margin-bottom: 4px;
}
.correct-msg { color: #15803d; }
.wrong-msg { color: #b91c1c; }
.explanation {
  font-size: 13px;
  line-height: 1.4;
  margin: 0;
  color: #166534;
}
</style>
