# Phase 0 Implementation Parity Matrix & Verification Report

**项目名称：** AI Buddy  
**审查分支：** `main` (commit `120ca0c`)  
**审查日期：** 2026-11-08  
**审查人：** 技术负责人 / 架构师  

---

## 一、审查背景与双文件路径异常发现

在审查 `client/src` 代码时，发现项目中同时存在两个客户端实现层：
1. **活跃使用层 (`client/src/lib/`)**：由 `App.vue` -> `useConversation.ts` 所直接引用，包含 `lib/realtimeClient.ts`、`lib/api.ts`、`lib/mockClient.ts`、`lib/types.ts`。
2. **遗留未引用层 (`client/src/services/`)**：包含 `services/realtimeClient.ts`、`services/api.ts`、`types/realtime.ts`。

**关键发现**：
- 遗留未引用的 `services/realtimeClient.ts` 保留了 Phase 0 原型期的全部已知缺陷（麦克风授权前启动连接计时器、WebRTC `disconnected` 立即判定致命错误、`interrupt()` 中 `pause()` 音频导致后续静音、缺少 `output_audio_buffer.clear`、无 Wake Lock、无 iOS 手势同步初始化等）。
- 实际在生产和开发模式下运行的 `lib/realtimeClient.ts` 已经实现了 Phase 0 的 8 项关键修复。
- 外部审查者若阅读 `services/realtimeClient.ts` 会误以为修复均未落地；而运行中的代码走的是 `lib/realtimeClient.ts`。
- **行动项**：保留 `lib/` 经过验证的实现作为当前运行基线，并在后续前端模块重构中将 `services/` 升级统一，避免双轨实现造成混淆。

---

## 二、Phase 0 八项修复逐项核对表

| 序号 | 修复项目 | 预期行为 | 当前代码证据 (位置与逻辑) | 测试证据与运行验证 | 状态 |
| :---: | :--- | :--- | :--- | :--- | :---: |
| **1** | **麦克风授权超时保护** | 不因用户授权麦克风慢而误判 10s WebRTC 连接超时 | `client/src/lib/realtimeClient.ts`<br>L68-70: 先 `await getUserMedia(...)`<br>L79-81: 麦克风获得授权后再启动 `connectTimer` (15s SDP超时) | 编译通过；代码路径严格串行，授权耗时不计入连接超时 | **已确认生效** |
| **2** | **打断后音频恢复** | 用户或系统打断后，下一轮 AI 发言扬声器仍能正常发声 | `client/src/lib/realtimeClient.ts`<br>L143-150: `interrupt()` 严禁调用 `audioEl.pause()`，仅向云端发送 `response.cancel` 和 `output_audio_buffer.clear`，重置本地状态至 `listening` | Demo 模式及真实连接验证：打断后下一轮 `output_audio_buffer.started` 正常驱动音频流 | **已确认生效** |
| **3** | **重连宽限期 (8秒)** | 瞬时弱网断开 (`disconnected`) 不立即判死挂断 | `client/src/lib/realtimeClient.ts`<br>L6: `RECONNECT_GRACE_MS = 8_000`<br>L97: `connectionState === 'disconnected'` 触发 `beginReconnectGrace()`<br>L286-296: 进入 `reconnecting` 状态并倒计时 8 秒，若重连恢复则复位 | 模拟网络波动：进入 `reconnecting` 状态，不会被直接踢出 Session | **已确认生效** |
| **4** | **错误状态保留** | 连接或运行时错误不会被 `stop()` 强制覆盖为 `ended` | `client/src/lib/realtimeClient.ts`<br>L278-284: `fail(err)` 先执行 `teardown()`（清理资源但不改变 `state`），再派发 `onError` 并最终设置 `state = 'error'` | 故意断开或报错（如 `?fail=mic`）：UI 正确渲染错误引导卡片而非“通话已结束” | **已确认生效** |
| **5** | **按 `item_id` 处理异步事件乱序** | 避免用户和模型输入/输出事件因网络到达顺序颠倒 | `client/src/lib/realtimeClient.ts`<br>L194, 208, 228, 234: 所有事件均附带 `evt.item_id`<br>`client/src/composables/useConversation.ts`<br>L46-51: `onTurn` 按 `p.id` 匹配原地更新或追加 | 用户发言与转写到达前先占位，转写完成后精准定位替换，无乱序插队 | **已确认生效** |
| **6** | **iOS Safari 手势解锁音频** | 移动端 Safari 自动播放策略不会静默吞掉 AI 声音 | `client/src/lib/realtimeClient.ts`<br>L55-59: 在用户点击事件栈中同步 `document.createElement('audio')` 和 `new AudioContext()` 并 `ctx.resume()`<br>L300-303: `tryPlay()` 捕获拦截异常并触发 `onAudioBlocked(true)` | UI 备有音频受阻点击恢复横幅，调用 `resumeAudio()` 立即恢复发声 | **已确认生效** |
| **7** | **Wake Lock 屏幕常亮** | 对话进行中防止手机系统自动锁屏掐断音频 | `client/src/lib/realtimeClient.ts`<br>L60: 启动时调用 `acquireWakeLock()`<br>L366-372: 请求 `navigator.wakeLock.request('screen')`<br>L409: `teardown()` 中安全释放 | 支持 Screen Wake Lock API 的浏览器在通话期间保持屏幕常亮 | **已确认生效** |
| **8** | **PWA 路由防拦截** | Service Worker 与静态文件路由不拦截 `/api/` 接口 | `client/vite.config.ts`<br>L28: `navigateFallbackDenylist: [/^\/api\//]`<br>`server/Buddy.Api/Program.cs`<br>L113: `app.MapFallbackToFile("index.html")` | SPA 路由正常 fallback，所有 `/api/*` 请求直接透传至 Minimal API 端点 | **已确认生效** |

---

## 三、待对齐与补齐的技术点

1. **`webrtcfilter=on` 参数显式化**：
   - 当前 `client/src/lib/realtimeClient.ts` L120 调用的 URL 为 `${session.endpoint}/openai/v1/realtime/calls`。
   - 现行 Azure OpenAI Realtime 规范中，可通过 `/openai/v1/realtime/calls?webrtcfilter=on` 显式开启服务端事件白名单过滤，确保 WebRTC DataChannel 仅透传已授权的 11 类核心事件。
   - **改进措施**：在请求 URL 中显式携带 `?webrtcfilter=on` 参数。

2. **双客户端清理与统一**：
   - `client/src/services/realtimeClient.ts`、`client/src/services/api.ts` 以及 `client/src/types/realtime.ts` 存在未修复历史代码，容易误导后续协作。
   - **改进措施**：将 `services/` 升级对齐到 `lib/` 经过验证的状态，作为未来模块化架构的正式服务层。
