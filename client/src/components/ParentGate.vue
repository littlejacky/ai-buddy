<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import Sheet from './Sheet.vue'

// 家长验证：一道孩子难以随手答对的乘法题。
// 注意：这是“防孩子误触”的门槛，不是安全边界 —— 真正的访问控制需要账号体系。
const props = defineProps<{ open: boolean }>()
const emit = defineEmits<{ close: []; pass: [] }>()

const a = ref(13)
const b = ref(7)
const answer = ref('')
const wrong = ref(false)

watch(
  () => props.open,
  (v) => {
    if (!v) return
    a.value = 12 + Math.floor(Math.random() * 8)
    b.value = 6 + Math.floor(Math.random() * 4)
    answer.value = ''
    wrong.value = false
  },
)

const ok = computed(() => Number(answer.value) === a.value * b.value)

function submit() {
  if (ok.value) emit('pass')
  else {
    wrong.value = true
    answer.value = ''
  }
}
</script>

<template>
  <Sheet :open="open" title="家长验证" @close="emit('close')">
    <form @submit.prevent="submit">
      <p class="q">请计算：<strong>{{ a }} × {{ b }} = ?</strong></p>
      <input
        v-model="answer"
        inputmode="numeric"
        pattern="[0-9]*"
        autocomplete="off"
        aria-label="答案"
        :aria-invalid="wrong"
      />
      <p v-if="wrong" class="err" role="alert">不对哦，再试一次。</p>
      <button class="go" type="submit">确认</button>
    </form>
  </Sheet>
</template>

<style scoped>
form {
  display: flex;
  flex-direction: column;
  gap: 12px;
  padding: 4px 0 8px;
}
.q {
  font-size: 20px;
}
input {
  min-height: 52px;
  font-size: 22px;
  padding: 0 14px;
  border-radius: 14px;
  border: 2px solid var(--line);
  background: var(--surface-2);
  color: var(--text);
}
input[aria-invalid='true'] {
  border-color: var(--danger);
}
.err {
  color: var(--danger);
  font-size: 15px;
}
.go {
  min-height: 52px;
  border-radius: 14px;
  background: var(--accent);
  color: var(--accent-ink);
  font-weight: 650;
}
</style>
