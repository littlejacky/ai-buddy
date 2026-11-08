# Phase 1 Memory Architecture & JSON Schema

**项目名称：** AI Buddy  
**记忆模型：** 三层轻量级记忆体系 (L1/L2/L3)，零向量数据库开销，300~600 Token 控制  

---

## 一、三层记忆分层规范

1. **L1: 稳定事实画像 (Stable Persona Facts)**：
   - 儿童明确表达的长期事实、稳定偏好、家长授权的基本资料（如“喜欢恐龙”、“养了一只叫花花的小狗”）。
   - 具有高确定性与事实依据，生命周期长，需定期（如 180 天）通过对话或家长确认维持活性。
2. **L2: 近期会话摘要 (Recent Session Summaries)**：
   - 提取最近 1~3 次有效通话的关键摘要，包含讨论的核心话题、开放式疑问（如“上次问到为什么月亮会跟着人走，还没讲完”）。
3. **L3: 持续探索兴趣与待续主题 (Active Topics & Interests)**：
   - 跨多次对话反复涌现的知识焦点（如深海生物、古生物、天文航天）。

---

## 二、MemoryFact 实体 JSON Schema

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "title": "MemoryFact",
  "type": "object",
  "required": [
    "id",
    "childProfileId",
    "factType",
    "content",
    "sourceType",
    "confidence",
    "memoryVersion",
    "isActive",
    "createdAtUtc"
  ],
  "properties": {
    "id": { "type": "string", "format": "uuid" },
    "childProfileId": { "type": "string", "format": "uuid" },
    "factType": {
      "type": "string",
      "enum": ["Interest", "Preference", "Habit", "OpenQuestion", "FriendOrFamily"]
    },
    "content": {
      "type": "string",
      "maxLength": 120,
      "description": "精炼陈述，避免长篇叙述"
    },
    "sourceSessionId": { "type": "string", "format": "uuid", "nullable": true },
    "sourceType": {
      "type": "string",
      "enum": ["child_explicit", "guardian_provided", "model_inferred"]
    },
    "confidence": {
      "type": "number",
      "minimum": 0.0,
      "maximum": 1.0
    },
    "memoryVersion": { "type": "integer", "minimum": 1 },
    "isActive": { "type": "boolean" },
    "createdAtUtc": { "type": "string", "format": "date-time" },
    "lastConfirmedAtUtc": { "type": "string", "format": "date-time", "nullable": true },
    "expiresAtUtc": { "type": "string", "format": "date-time", "nullable": true }
  }
}
```

---

## 三、Session Instructions 注入模板

```text
【关于这个小朋友的记忆（仅在自然相关时提起，不要刻意背诵）】
- 称呼：{Nickname}（{AgeBand}）
- 稳定兴趣：{L1_Facts_Formatted}
- 上次聊到：{L2_Recent_Summary}
- 待继续探索的话题：{L3_Active_Topics}

【记忆运用原则】
1. 当小朋友主动提到相关话题时，自然接话（例如“上次你说的霸王龙……”）；绝不要一开场就背书。
2. 小朋友如果纠正记忆（例如“我现在不喜欢恐龙了，喜欢汽车”），立刻顺应，不要辩解。
3. 未确定的推断不要当作既定事实。
```
