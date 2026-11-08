<script setup lang="ts">
import { onMounted, ref } from 'vue'

const props = defineProps<{
  childProfileId?: string
  familyId?: string
}>()

const emit = defineEmits<{
  (e: 'close'): void
}>()

const activeTab = ref<'digests' | 'memories' | 'consents' | 'profile'>('digests')

// ── State ──
const loading = ref(false)
const digests = ref<any[]>([])
const memories = ref<any[]>([])
const consents = ref<any[]>([])
const newMemoryText = ref('')

const defaultChildId = props.childProfileId || '33333333-3333-3333-3333-333333333333'
const defaultFamilyId = props.familyId || '11111111-1111-1111-1111-111111111111'

onMounted(async () => {
  await loadData()
})

async function loadData() {
  loading.value = true
  try {
    const headers = { 'X-Family-Id': defaultFamilyId }

    // Load digests
    const dRes = await fetch(`/api/children/${defaultChildId}/digests`, { headers })
    if (dRes.ok) digests.value = await dRes.json()

    // Load memories
    const mRes = await fetch(`/api/children/${defaultChildId}/memories`, { headers })
    if (mRes.ok) memories.value = await mRes.json()

    // Load consents
    const cRes = await fetch(`/api/children/${defaultChildId}/consents`, { headers })
    if (cRes.ok) consents.value = await cRes.json()
  } catch (e) {
    console.error('Failed to load parent dashboard data', e)
  } finally {
    loading.value = false
  }
}

async function addMemory() {
  if (!newMemoryText.value.trim()) return
  try {
    const res = await fetch(`/api/children/${defaultChildId}/memories`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', 'X-Family-Id': defaultFamilyId },
      body: JSON.stringify({
        factType: 'Interest',
        content: newMemoryText.value.trim(),
        sourceType: 'guardian_provided',
      }),
    })
    if (res.ok) {
      newMemoryText.value = ''
      await loadData()
    }
  } catch (e) {
    console.error('Failed to add memory', e)
  }
}

async function deleteMemory(id: string) {
  try {
    const res = await fetch(`/api/children/${defaultChildId}/memories/${id}`, {
      method: 'DELETE',
      headers: { 'X-Family-Id': defaultFamilyId },
    })
    if (res.ok) await loadData()
  } catch (e) {
    console.error('Failed to delete memory', e)
  }
}

async function toggleConsent(consentType: string, currentVal: boolean) {
  try {
    const res = await fetch(`/api/children/${defaultChildId}/consents`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json', 'X-Family-Id': defaultFamilyId },
      body: JSON.stringify({
        consentType,
        isGranted: !currentVal,
      }),
    })
    if (res.ok) await loadData()
  } catch (e) {
    console.error('Failed to update consent', e)
  }
}
</script>

<template>
  <div class="parent-dashboard-overlay">
    <div class="dashboard-card">
      <header class="dash-header">
        <div class="dash-title">
          <h2>👨‍👩‍👧 家长成长陪伴中心</h2>
          <span class="sub">了解孩子的好奇心与成长轨迹</span>
        </div>
        <button class="close-btn" @click="emit('close')" aria-label="关闭家长端">✕</button>
      </header>

      <!-- 选项卡导航 -->
      <nav class="dash-nav">
        <button
          class="nav-tab"
          :class="{ active: activeTab === 'digests' }"
          @click="activeTab = 'digests'"
        >
          今日探索简报
        </button>
        <button
          class="nav-tab"
          :class="{ active: activeTab === 'memories' }"
          @click="activeTab = 'memories'"
        >
          长期陪伴记忆
        </button>
        <button
          class="nav-tab"
          :class="{ active: activeTab === 'consents' }"
          @click="activeTab = 'consents'"
        >
          隐私与权限
        </button>
      </nav>

      <!-- 主视图区 -->
      <main class="dash-body">
        <div v-if="loading" class="loading">加载中...</div>

        <!-- 1. 今日探索简报 -->
        <section v-else-if="activeTab === 'digests'" class="tab-pane">
          <div class="pane-desc">每次通话结束后，AI 会提炼孩子感兴趣的闪光点与亲子互动建议（绝无监控打分）。</div>
          <div v-if="digests.length === 0" class="empty">
            最近暂无探索简报。和小朋友聊聊天后就会出现哦！
          </div>
          <div v-for="d in digests" :key="d.id" class="digest-item">
            <div class="digest-time">{{ new Date(d.generatedAtUtc).toLocaleDateString('zh-CN') }} 对话探索</div>
            <div class="tags">
              <span v-for="t in d.topics" :key="t" class="topic-tag">{{ t }}</span>
            </div>
            <div class="digest-highlight">
              <strong>✨ 好奇心亮点：</strong>{{ d.curiosityHighlight }}
            </div>
            <div class="digest-activity">
              <strong>💡 亲子小建议：</strong>{{ d.familyActivitySuggestion }}
            </div>
          </div>
        </section>

        <!-- 2. 长期陪伴记忆 -->
        <section v-else-if="activeTab === 'memories'" class="tab-pane">
          <div class="pane-desc">Buddy 在跨设备对话中记住的小朋友偏好。家长可随时查看、添加或删除。</div>
          <div class="add-memory-bar">
            <input
              v-model="newMemoryText"
              type="text"
              placeholder="例如：喜欢画小恐龙、爱吃苹果"
              @keyup.enter="addMemory"
            />
            <button class="add-btn" @click="addMemory">添加记忆</button>
          </div>
          <div v-if="memories.length === 0" class="empty">暂无记忆。</div>
          <ul class="memory-list">
            <li v-for="m in memories" :key="m.id" class="memory-item">
              <div class="mem-info">
                <span class="mem-content">{{ m.content }}</span>
                <span class="mem-source">({{ m.sourceType === 'guardian_provided' ? '家长提供' : '孩子表达' }})</span>
              </div>
              <button class="del-mem-btn" @click="deleteMemory(m.id)" title="删除此条记忆">删除</button>
            </li>
          </ul>
        </section>

        <!-- 3. 隐私与权限 -->
        <section v-else-if="activeTab === 'consents'" class="tab-pane">
          <div class="pane-desc">严格的细粒度授权保护。随时开启或关闭某项能力，关闭后对应数据立即停止处理。</div>
          <div class="consent-list">
            <div v-for="c in consents" :key="c.consentType" class="consent-item">
              <div class="consent-info">
                <div class="consent-title">
                  {{
                    c.consentType === 'VoiceService' ? '基础实时语音' :
                    c.consentType === 'CrossSessionMemory' ? '跨会话长期记忆' :
                    c.consentType === 'ParentDiscoveryDigest' ? '家长探索简报' :
                    c.consentType === 'TranscriptProcessing' ? '对话转写处理' :
                    c.consentType === 'ChildSafetyAudit' ? '儿童安全防护' :
                    c.consentType === 'ApproximateLocation' ? '近似位置感知' :
                    c.consentType === 'PublicWebSearch' ? '公开网络知识检索' : c.consentType
                  }}
                </div>
                <div class="consent-status-desc">
                  {{ c.isGranted ? '已开启' : '已关闭' }}
                </div>
              </div>
              <button
                class="switch-btn"
                :class="{ on: c.isGranted }"
                @click="toggleConsent(c.consentType, c.isGranted)"
              >
                {{ c.isGranted ? '关闭' : '开启' }}
              </button>
            </div>
          </div>
        </section>
      </main>
    </div>
  </div>
</template>

<style scoped>
.parent-dashboard-overlay {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.6);
  backdrop-filter: blur(8px);
  z-index: 1000;
  display: flex;
  justify-content: center;
  align-items: center;
  padding: 16px;
}
.dashboard-card {
  background: #ffffff;
  border-radius: 20px;
  width: 100%;
  max-width: 540px;
  max-height: 85vh;
  display: flex;
  flex-direction: column;
  box-shadow: 0 20px 40px rgba(0, 0, 0, 0.2);
  overflow: hidden;
  color: #1e293b;
}
.dash-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  padding: 20px 24px 16px;
  border-bottom: 1px solid #e2e8f0;
}
.dash-title h2 {
  margin: 0;
  font-size: 18px;
  font-weight: 700;
  color: #0f172a;
}
.dash-title .sub {
  font-size: 13px;
  color: #64748b;
}
.close-btn {
  background: #f1f5f9;
  border: none;
  font-size: 16px;
  border-radius: 50%;
  width: 32px;
  height: 32px;
  cursor: pointer;
  color: #64748b;
}
.dash-nav {
  display: flex;
  background: #f8fafc;
  padding: 8px 16px;
  border-bottom: 1px solid #e2e8f0;
  gap: 8px;
}
.nav-tab {
  flex: 1;
  background: none;
  border: none;
  padding: 8px 12px;
  font-size: 13px;
  font-weight: 600;
  color: #64748b;
  border-radius: 8px;
  cursor: pointer;
  transition: all 0.2s;
}
.nav-tab.active {
  background: #ffffff;
  color: #0284c7;
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.05);
}
.dash-body {
  padding: 20px 24px;
  overflow-y: auto;
  flex: 1;
}
.pane-desc {
  font-size: 13px;
  color: #64748b;
  margin-bottom: 16px;
  line-height: 1.5;
}
.loading, .empty {
  text-align: center;
  color: #94a3b8;
  font-size: 14px;
  padding: 32px 0;
}
.digest-item {
  background: #f8fafc;
  border: 1px solid #e2e8f0;
  border-radius: 12px;
  padding: 14px;
  margin-bottom: 12px;
  text-align: left;
}
.digest-time {
  font-size: 12px;
  font-weight: 600;
  color: #0369a1;
  margin-bottom: 6px;
}
.tags {
  display: flex;
  gap: 6px;
  margin-bottom: 8px;
}
.topic-tag {
  background: #e0f2fe;
  color: #0284c7;
  font-size: 11px;
  padding: 2px 8px;
  border-radius: 6px;
  font-weight: 600;
}
.digest-highlight, .digest-activity {
  font-size: 13px;
  line-height: 1.5;
  margin-top: 6px;
  color: #334155;
}
.add-memory-bar {
  display: flex;
  gap: 8px;
  margin-bottom: 16px;
}
.add-memory-bar input {
  flex: 1;
  border: 1px solid #cbd5e1;
  border-radius: 8px;
  padding: 8px 12px;
  font-size: 13px;
}
.add-btn {
  background: #0284c7;
  color: #fff;
  border: none;
  border-radius: 8px;
  padding: 8px 16px;
  font-size: 13px;
  font-weight: 600;
  cursor: pointer;
}
.memory-list {
  list-style: none;
  padding: 0;
  margin: 0;
}
.memory-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 10px 12px;
  border-bottom: 1px solid #f1f5f9;
}
.mem-content {
  font-size: 14px;
  font-weight: 500;
  color: #1e293b;
}
.mem-source {
  font-size: 12px;
  color: #94a3b8;
  margin-left: 6px;
}
.del-mem-btn {
  background: none;
  border: none;
  color: #ef4444;
  font-size: 12px;
  cursor: pointer;
  padding: 4px 8px;
}
.consent-list {
  display: flex;
  flex-direction: column;
  gap: 10px;
}
.consent-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 12px 14px;
  background: #f8fafc;
  border-radius: 10px;
  border: 1px solid #e2e8f0;
}
.consent-title {
  font-size: 14px;
  font-weight: 600;
  color: #1e293b;
}
.consent-status-desc {
  font-size: 12px;
  color: #64748b;
  margin-top: 2px;
}
.switch-btn {
  border: none;
  border-radius: 6px;
  padding: 6px 14px;
  font-size: 12px;
  font-weight: 600;
  cursor: pointer;
  background: #e2e8f0;
  color: #475569;
}
.switch-btn.on {
  background: #0284c7;
  color: #fff;
}
</style>
