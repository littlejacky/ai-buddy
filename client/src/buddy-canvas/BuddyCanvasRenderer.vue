<script setup lang="ts">
import { computed } from 'vue'
import FlashCards from './FlashCards.vue'
import InteractiveStory from './InteractiveStory.vue'
import KnowledgeCard from './KnowledgeCard.vue'
import MultipleChoiceQuiz from './MultipleChoiceQuiz.vue'
import type { BuddyActivity, StoryChoice } from './types'

const props = defineProps<{
  activity: BuddyActivity | null
}>()

const emit = defineEmits<{
  (e: 'action', activityId: string, actionPayload: Record<string, any>): void
  (e: 'dismiss'): void
}>()

const blocks = computed(() => props.activity?.blocks ?? [])

function handleQuizResponse(selectedId: string, isCorrect: boolean) {
  if (!props.activity) return
  emit('action', props.activity.activityId, {
    actionType: 'quiz_answer',
    selectedId,
    isCorrect,
  })
}

function handleStoryChoice(choice: StoryChoice) {
  if (!props.activity) return
  emit('action', props.activity.activityId, {
    actionType: 'story_branch',
    choiceId: choice.id,
    choiceLabel: choice.label,
  })
}
</script>

<template>
  <div v-if="activity" class="buddy-canvas-overlay">
    <div class="canvas-content">
      <div v-for="(block, idx) in blocks" :key="idx" class="block-wrapper">
        <KnowledgeCard
          v-if="block.type === 'knowledge'"
          :block="block"
          @close="emit('dismiss')"
        />
        <MultipleChoiceQuiz
          v-else-if="block.type === 'quiz'"
          :block="block"
          @respond="handleQuizResponse"
          @close="emit('dismiss')"
        />
        <FlashCards
          v-else-if="block.type === 'flashcard'"
          :block="block"
          @close="emit('dismiss')"
        />
        <InteractiveStory
          v-else-if="block.type === 'story'"
          :block="block"
          @choose="handleStoryChoice"
          @close="emit('dismiss')"
        />
      </div>
    </div>
  </div>
</template>

<style scoped>
.buddy-canvas-overlay {
  width: 100%;
  max-width: 420px;
  margin: 0 auto;
  padding: 0 16px;
  animation: slide-up 0.3s cubic-bezier(0.16, 1, 0.3, 1);
}
@keyframes slide-up {
  from {
    opacity: 0;
    transform: translateY(16px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}
</style>
