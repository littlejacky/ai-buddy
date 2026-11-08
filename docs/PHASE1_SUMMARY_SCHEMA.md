# Phase 1 Parent Discovery Digest Schema & Guidelines

**项目名称：** AI Buddy  
**定位：** 今日探索简报（Today's Discovery）—— 助力家长读懂孩子好奇心，杜绝监控式打分  

---

## 一、简报设计规范与伦理底线

1. **正向赋能，严禁监控画像**：
   - 简报宗旨是架起亲子沟通桥梁，提供晚饭或睡前共同探索话题。
   - **严禁生成**：智商/情商测评、心理障碍诊断、注意力缺陷评级、儿童行为排名或负面性格标签。
2. **证据可追溯**：
   - 简报中每一个记录的“好奇心闪光点”，必须在临时转写中存在对应的上下文真实依据，严禁 AI 凭空臆造。
3. **隐私克制**：
   - 不向家长展示逐字逐句的对话录像，仅提取结构化洞察。

---

## 二、SessionSummary 实体 JSON Schema

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "title": "SessionSummary",
  "type": "object",
  "required": [
    "id",
    "sessionId",
    "childProfileId",
    "topics",
    "curiosityHighlight",
    "familyActivitySuggestion",
    "nextTopics",
    "generatedAtUtc"
  ],
  "properties": {
    "id": { "type": "string", "format": "uuid" },
    "sessionId": { "type": "string", "format": "uuid" },
    "childProfileId": { "type": "string", "format": "uuid" },
    "topics": {
      "type": "array",
      "items": { "type": "string" },
      "maxItems": 5,
      "description": "本次通话涉及的 1~5 个主题关键词"
    },
    "curiosityHighlight": {
      "type": "string",
      "maxLength": 300,
      "description": "孩子今天展现出好奇心或奇思妙想的具体片段"
    },
    "familyActivitySuggestion": {
      "type": "string",
      "maxLength": 300,
      "description": "适合家长今晚或周末和孩子一起探索的轻量级互动建议"
    },
    "nextTopics": {
      "type": "array",
      "items": { "type": "string" },
      "maxItems": 3,
      "description": "未探讨完、下次可以继续深挖的知识引子"
    },
    "expressedFeelings": {
      "type": "string",
      "maxLength": 150,
      "nullable": true,
      "description": "孩子主动表达的显式情绪倾向（中性客观描述）"
    },
    "generatedAtUtc": { "type": "string", "format": "date-time" }
  }
}
```
