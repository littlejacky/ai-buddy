# Phase 1 Test Plan & Automated Verification Matrix

**项目名称：** AI Buddy  
**测试方针：** 后端 xUnit 驱动集成验收 + 前端 Vitest 组件测试 + 端到端跨会话场景自动化  

---

## 一、自动化测试工程布局

```text
tests/
├── Buddy.Api.Tests/               # 后端测试项目 (xUnit + WebApplicationFactory)
│   ├── Integration/
│   │   ├── AuthAndTenantTests.cs  # 家长认证、PIN、Family 隔离测试
│   │   ├── ConsentTests.cs        # 细粒度授权开闭与拦截测试
│   │   ├── SessionLifecycleTests.cs # Checkpoint / Finalize 幂等性测试
│   │   ├── OutboxWorkerTests.cs   # 后台任务状态机与恢复测试
│   │   └── MemoryContextTests.cs  # L1/L2/L3 记忆注入与时区测试
│   └── Unit/
│       ├── ToolRegistryTests.cs   # 工具入参校验与安全隔离测试
│       └── SafetyRuleTests.cs     # S0/S1/S2 风险等级评定测试
│
client/
└── src/
    └── __tests__/                 # 前端测试套件 (Vitest + Vue Test Utils)
        ├── VoiceOrb.spec.ts       # 声音振幅与动画状态机映射
        ├── ParentGate.spec.ts     # 算术门禁输入与校验
        ├── useConversation.spec.ts # 计时、重连与打断状态转换
        └── BuddyCanvas.spec.ts    # 结构化 JSON Schema 渲染与点击回传
```

---

## 二、必须覆盖的核心验收场景 (End-to-End Scenarios)

1. **多租户与数据隔离验证**：
   - Family A 的监护人绝对无法通过篡改 `ChildProfileId` 获取 Family B 的儿童记忆或探索简报（期望 HTTP 403 / 404）。
2. **跨设备记忆延续性场景**：
   - 步骤 1：在设备 1（平板）上完成会话 A，孩子表示“我最喜欢的恐龙是三角龙”。
   - 步骤 2：会话 A 提交 `finalize`，Outbox 处理生成 `MemoryFact (Interest: 三角龙)`。
   - 步骤 3：在设备 2（手机）上开启相同儿童的新会话 B。
   - 步骤 4：验证 Session Instructions 成功注入“稳定兴趣：三角龙”，AI 能自然承接。
3. **撤回授权立竿见影**：
   - 家长关闭 `CrossSessionMemory` 授权，后续会话即便历史有记录，也必须以纯白板人格启动。
4. **单儿童并发会话互斥 (Session Lease)**：
   - 设备 1 正在通话，设备 2 尝试启动同一儿童的语音，设备 2 收到 409 Conflict 或租约拒绝友好提示。
5. **Outbox 幂等性与崩溃恢复**：
   - 模拟网络超时重复发送两次 `finalize`，数据库只生成一份探索简报与记忆。
   - 模拟 Worker 执行到一半崩溃重启，任务在 `LockedUntilUtc` 过期后被重新领回继续执行。
