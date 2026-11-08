# Phase 1 Guardian Consent Matrix & Authorization Model

**项目名称：** AI Buddy  
**规范核心：** 细粒度、非捆绑、可撤销、按儿童独立授权  

---

## 一、授权项目矩阵 (Consent Matrix)

| 授权标识 (ConsentKey) | 授权名称与用途 | 默认状态 | 依赖前置条件 | 撤回后系统行为 | 是否阻断基础语音 |
|---|---|:---:|---|---|:---:|
| **`VoiceService`** | **基础实时语音通话**<br>建立 WebRTC 连接并与 AI 进行双向语音交谈 | 默认开启 | Guardian 账号与儿童建档 | 无法发起实时语音会话 | **是** |
| **`CrossSessionMemory`** | **跨会话长期记忆**<br>记录儿童兴趣、偏好与待续话题并在后续对话中唤起 | 默认关闭 | `VoiceService` 开启 | Buddy 每次通话均作为全新伙伴，不带入历史记忆 | **否** |
| **`ParentDiscoveryDigest`** | **家长今日探索简报**<br>会话结束后提取好奇心亮点与亲子互动建议 | 默认关闭 | `TranscriptProcessing` 开启 | 会话完结后不生成简报，家长端无历史记录 | **否** |
| **`TranscriptProcessing`** | **对话文字转写处理**<br>允许接收并处理会话文字用于记忆提炼与简报生成 | 默认关闭 | `VoiceService` 开启 | 不采集、不暂存转写文本，Outbox 跳过提炼 | **否** |
| **`ChildSafetyAudit`** | **儿童安全与困境保护**<br>扫描高风险危险言行并在紧急时通知家长 | 默认开启 | `VoiceService` 开启 | 维持基础 System Instruction，不记录结构化事件 | **否** |
| **`ApproximateLocation`** | **近似城市地理位置**<br>单次获取大致城市以支持查询天气与本地活动 | 默认关闭 | 监护人手动输入或单次授权 | 无法自动定位，Buddy 将礼貌询问小朋友所在城市 | **否** |
| **`PublicWebSearch`** | **公开网络知识检索**<br>允许 Buddy 查询最新公共事实、科学发现 | 默认关闭 | `VoiceService` 开启 | 仅依靠模型既有知识库，不发起外部网络查询 | **否** |

---

## 二、授权实施规则

1. **绝对禁止捆绑式一揽子授权**：
   - 界面上每一项授权必须提供独立的 Switch 开关与清晰通俗的用途说明。
   - 基础语音服务绝不因家长未开启“长期记忆”或“今日简报”而拒绝服务。
2. **后端鉴权闭环拦截**：
   - `/api/sessions/checkpoint` 与 `/finalize` 在持久化转写时，强制检查 `TranscriptProcessing` 授权状态；若未授权，转写字段一律置空。
   - `MemoryContextBuilder` 在组装新会话 Prompt 时，强制检查 `CrossSessionMemory` 授权；未授权时注入空记忆，仅保留原生安全指令。
   - `ToolRegistry` 在执行 `web_search` 或 `weather` 工具时，强制检查对应 `PublicWebSearch` 与 `ApproximateLocation` 授权。
3. **撤回即时生效**：
   - 监护人一旦在家长端界面关闭授权，`ConsentRecord.IsGranted` 立即更新为 `false`，并记录 `RevokedAtUtc`。
   - 正在进行中的活跃会话在下一次检查点或工具执行时实时感知撤回。
