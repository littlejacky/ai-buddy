# Phase 1 BuddyCanvas JSON Schema & Component Specification

**项目名称：** AI Buddy  
**交互理念：** 语音为主、触控为辅；动态结构化呈现；严禁动态代码执行  

---

## 一、BuddyCanvas 架构原则

1. **白名单组件驱动**：前端仅注册和渲染预定义的 Vue 组件，禁止任何 `v-html` 渲染不受信富文本，禁止 `eval()`，禁止跨域未知 iframe。
2. **Schema 强类型校验**：所有由模型或后端下发的交互卡片均须通过版本化 JSON Schema 验证，非法字段直接抛弃。
3. **适龄化视觉风格**：大触控区域（≥48px）、高对比度、友好动效。

---

## 二、四大基础组件与 Schema 规范

### 2.1 顶层容器 Activity JSON Schema

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "title": "BuddyActivity",
  "type": "object",
  "required": ["schemaVersion", "activityId", "sessionId", "kind", "title", "blocks"],
  "properties": {
    "schemaVersion": { "type": "string", "const": "1.0" },
    "activityId": { "type": "string", "format": "uuid" },
    "sessionId": { "type": "string", "format": "uuid" },
    "kind": { "type": "string", "enum": ["knowledge", "quiz", "flashcard", "story"] },
    "title": { "type": "string", "maxLength": 60 },
    "ageBand": { "type": "string", "enum": ["4-6", "7-8", "9-10"] },
    "blocks": { "type": "array", "minItems": 1 }
  }
}
```

### 2.2 核心组件规格

1. **`KnowledgeCard` (知识百科卡)**：
   - 标题、高清图示/插画、精简解释（50字内口语化阐述）、来源证明。
2. **`MultipleChoiceQuiz` (趣味互动选择题)**：
   - 问题文本、2~3 个适龄选项、正确答案标识、激励性即时反馈与解释。
3. **`FlashCards` (探索翻转卡)**：
   - 正面提出好奇心疑问（“企鹅会飞吗？”）、点击 3D 翻转呈现答案（“在水里飞翔哦！”）。
4. **`InteractiveStory` (互动分支故事)**：
   - 故事片段、2 个分支抉择（“走进森林小木屋” / “顺着小溪看小鹿”），点击后将抉择事件送回，驱动后续故事情节。
