<script setup lang="ts">
import { computed } from 'vue'
import type { Mode, SessionState } from '../lib/types'

const props = defineProps<{
  mode: Mode
  state: SessionState
  /** 0~1，说话人当前的音量 */
  level: number
  /** speaking 时可点击打断；idle 时可点击开始 */
  actionLabel: string
  actionable: boolean
}>()
defineEmits<{ press: [] }>()

const style = computed(() => ({ '--level': props.level.toFixed(3) }))
</script>

<template>
  <button
    type="button"
    class="orb"
    :class="[`s-${state}`, `m-${mode}`]"
    :style="style"
    :aria-label="actionLabel"
    :disabled="!actionable"
    @click="$emit('press')"
  >
    <span class="halo" aria-hidden="true"></span>
    <span class="ripple r1" aria-hidden="true"></span>
    <span class="ripple r2" aria-hidden="true"></span>
    <span class="spin" aria-hidden="true"></span>

    <span class="body" aria-hidden="true">
      <span class="sheen"></span>

      <!-- 孩子模式：一张会眨眼、会跟着声音张嘴的小脸 -->
      <svg v-if="mode === 'child'" class="face" viewBox="0 0 100 100">
        <g class="eyes">
          <ellipse class="eye" cx="36" cy="44" rx="5.5" ry="7" />
          <ellipse class="eye" cx="64" cy="44" rx="5.5" ry="7" />
        </g>
        <ellipse class="cheek" cx="25" cy="58" rx="6" ry="4" />
        <ellipse class="cheek" cx="75" cy="58" rx="6" ry="4" />
        <path class="smile" d="M38 60 Q50 72 62 60" />
        <ellipse class="talk" cx="50" cy="65" rx="9" ry="7" />
      </svg>
    </span>
  </button>
</template>

<style scoped>
.orb {
  --size: min(64vw, 40dvh, 300px);
  position: relative;
  width: var(--size);
  height: var(--size);
  display: grid;
  place-items: center;
  border-radius: 50%;
  cursor: default;
}
.orb:not(:disabled) {
  cursor: pointer;
}
.orb:disabled {
  opacity: 1;
}

.body {
  position: relative;
  width: 72%;
  height: 72%;
  border-radius: 50%;
  background: radial-gradient(circle at 32% 28%, var(--orb-a), var(--orb-b) 55%, var(--orb-c) 105%);
  box-shadow:
    0 0 calc(30px + var(--level) * 60px) color-mix(in srgb, var(--orb-b) 55%, transparent),
    inset 0 -10px 30px rgba(0, 0, 0, 0.12);
  transform: scale(calc(1 + var(--level) * 0.16));
  transition:
    transform 0.09s linear,
    box-shadow 0.15s linear,
    filter 0.4s;
  animation: breathe 5s ease-in-out infinite;
}
.sheen {
  position: absolute;
  inset: 8% 16% auto 14%;
  height: 34%;
  border-radius: 50%;
  background: linear-gradient(to bottom, rgba(255, 255, 255, 0.55), transparent);
  filter: blur(2px);
}
.halo {
  position: absolute;
  inset: 4%;
  border-radius: 50%;
  background: radial-gradient(circle, color-mix(in srgb, var(--orb-b) 30%, transparent), transparent 68%);
  opacity: 0.8;
}

/* 涟漪：只在“用户在说话”时出现，青色 = 我听见你了 */
.ripple {
  position: absolute;
  inset: 14%;
  border-radius: 50%;
  border: 2px solid var(--hear);
  opacity: 0;
}
.s-user_speaking .ripple {
  animation: ripple 1.8s ease-out infinite;
}
.s-user_speaking .r2 {
  animation-delay: 0.9s;
}

/* 连接中：一圈旋转的弧线 */
.spin {
  position: absolute;
  inset: 8%;
  border-radius: 50%;
  border: 3px solid transparent;
  border-top-color: var(--accent);
  opacity: 0;
}
.s-connecting .spin,
.s-reconnecting .spin {
  opacity: 1;
  animation: rot 1s linear infinite;
}

.s-idle .body,
.s-ended .body {
  filter: saturate(0.85);
}
.s-connecting .body,
.s-reconnecting .body {
  animation: breathe 1.6s ease-in-out infinite;
  filter: saturate(0.6);
}
.s-user_speaking .body {
  filter: hue-rotate(-12deg) saturate(1.1);
}
.s-thinking .body {
  animation: think 1.8s ease-in-out infinite;
}
.s-error .body {
  filter: grayscale(0.85);
  opacity: 0.7;
  animation: none;
}

/* 孩子的小脸 */
.face {
  position: absolute;
  inset: 0;
  width: 100%;
  height: 100%;
  overflow: visible;
}
.eye {
  fill: #3a2614;
  transform-box: fill-box;
  transform-origin: center;
  animation: blink 4.2s infinite;
  transition: transform 0.3s;
}
.cheek {
  fill: #ff8f7a;
  opacity: 0.55;
}
.smile {
  fill: none;
  stroke: #3a2614;
  stroke-width: 3.2;
  stroke-linecap: round;
}
.talk {
  fill: #7a2b1c;
  opacity: 0;
  transform-box: fill-box;
  transform-origin: center;
  transform: scaleY(calc(0.25 + var(--level) * 1.6));
  transition: transform 0.07s linear;
}
.s-speaking .talk {
  opacity: 1;
}
.s-speaking .smile {
  opacity: 0;
}
.s-user_speaking .eyes {
  transform: scale(1.12);
  transform-box: fill-box;
  transform-origin: center;
}
.s-thinking .eyes {
  transform: translate(2px, -5px);
}
.eyes {
  transition: transform 0.3s;
}

@keyframes breathe {
  0%,
  100% {
    scale: 1;
  }
  50% {
    scale: 1.035;
  }
}
@keyframes think {
  0%,
  100% {
    scale: 0.97;
  }
  50% {
    scale: 1;
    filter: brightness(1.08);
  }
}
@keyframes ripple {
  from {
    transform: scale(0.9);
    opacity: 0.7;
  }
  to {
    transform: scale(1.45);
    opacity: 0;
  }
}
@keyframes rot {
  to {
    transform: rotate(360deg);
  }
}
@keyframes blink {
  0%,
  94%,
  100% {
    transform: scaleY(1);
  }
  97% {
    transform: scaleY(0.1);
  }
}
</style>
