# Phase 1 REST & Realtime API Contract Specification

**项目名称：** AI Buddy  
**基础规范：** ASP.NET Core 10 Minimal API / JSON CamelCase / 标准 HTTP 状态码  

---

## 一、API 端点清单与契约

### 1.1 认证与监护人 (Auth & Guardian)

```http
POST /api/auth/guardian/login
Content-Type: application/json
{
  "subjectIdentifier": "guardian@example.com",
  "authCode": "123456"
}
Response 200 OK:
{
  "token": "jwt_or_session_token",
  "guardianId": "uuid",
  "familyId": "uuid",
  "displayName": "小明的爸爸"
}

POST /api/auth/guardian/verify-pin
Content-Type: application/json
{
  "pin": "123456"
}
Response 200 OK:
{ "verified": true }
```

### 1.2 家庭与儿童档案 (Family & ChildProfile)

```http
GET /api/families/current
Response 200 OK:
{
  "familyId": "uuid",
  "children": [
    {
      "id": "uuid",
      "nickname": "乐乐",
      "ageBand": "7-8",
      "preferredVoice": "marin",
      "currentMemoryVersion": 3
    }
  ],
  "devices": [
    { "id": "uuid", "deviceName": "家庭 iPad", "isAuthorized": true }
  ]
}

POST /api/children
Content-Type: application/json
{
  "nickname": "乐乐",
  "ageBand": "7-8",
  "preferredVoice": "marin"
}
Response 201 Created:
{ "childProfileId": "uuid" }
```

### 1.3 授权与合规 (Consents)

```http
GET /api/children/{childId}/consents
Response 200 OK:
[
  { "consentType": "CrossSessionMemory", "isGranted": true, "version": "1.0" },
  { "consentType": "ParentDiscoveryDigest", "isGranted": true, "version": "1.0" }
]

PUT /api/children/{childId}/consents
Content-Type: application/json
{
  "consentType": "CrossSessionMemory",
  "isGranted": false
}
Response 200 OK
```

### 1.4 实时会话与生命周期 (Sessions)

```http
POST /api/realtime/session
Content-Type: application/json
{
  "childProfileId": "uuid",
  "deviceId": "uuid",
  "mode": "child",
  "voice": "marin",
  "clientContext": {
    "timeZone": "Asia/Shanghai",
    "localTime": "2026-11-08T10:00:00+08:00"
  }
}
Response 200 OK:
{
  "ephemeralKey": "ek_...",
  "expiresAt": 1794285000,
  "applicationSessionId": "uuid",
  "endpoint": "https://<resource>.openai.azure.com",
  "maxSessionSeconds": 900
}

POST /api/sessions/{sessionId}/checkpoint
Content-Type: application/json
{
  "turns": [
    { "id": "item_1", "role": "user", "text": "鲸鱼为什么这么大？", "at": 1794284500 }
  ]
}
Response 204 NoContent

POST /api/sessions/{sessionId}/finalize
Content-Type: application/json
{
  "durationSeconds": 320,
  "finalTurns": [ ... ]
}
Response 200 OK:
{ "status": "Finalized", "outboxJobId": "uuid" }
```

### 1.5 探索简报与记忆管理 (Digests & Memories)

```http
GET /api/children/{childId}/digests
Response 200 OK:
[
  {
    "id": "uuid",
    "sessionId": "uuid",
    "topics": ["海洋生物", "蓝鲸", "浮力"],
    "curiosityHighlight": "乐乐好奇为什么蓝鲸能在水里游，但在陆地上会被自己压扁。",
    "familyActivitySuggestion": "晚饭时可以一起拿一个盛水的小碗放积木，感受浮力的力量。",
    "generatedAtUtc": "2026-11-08T10:15:00Z"
  }
]

GET /api/children/{childId}/memories
Response 200 OK:
[
  { "id": "uuid", "factType": "Interest", "content": "特别喜欢深海巨鲸和海洋探险", "confidence": 0.95 }
]

DELETE /api/children/{childId}/memories/{memoryId}
Response 204 NoContent
```

### 1.6 BuddyCanvas 互动活动 (Activities)

```http
GET /api/sessions/{sessionId}/events  (SSE Stream)
Event: activity_spawned
Data: { "activityId": "uuid", "kind": "quiz", "title": "谁是海洋巨人？", ... }

POST /api/sessions/{sessionId}/activities/{activityId}/actions
Content-Type: application/json
{
  "actionType": "submit_answer",
  "selectedOptionId": "b"
}
Response 200 OK
```
