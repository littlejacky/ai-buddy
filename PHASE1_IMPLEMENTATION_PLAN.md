# Phase 1 Implementation Plan & Gate Milestones

**项目名称：** AI Buddy  
**计划周期：** Phase 1 全周期  
**执行原则：** 代码为准、测试前置、各 Gate 依次突破、不产生死锁依赖  

---

## 一、阶段里程碑概览 (Gate Roadmap)

```text
[Gate 0: 现状核对与技术基线] (DONE)
      ↓
[Gate 0.5: 工具调用与信令架构验证]
      ↓
[Gate 1: 家庭、家长认证与儿童数据底座]
      ↓
[Gate 2: 会话持久化、转写结构化与 Outbox 异步引擎]
      ↓
[Gate 3: 跨设备共享记忆体系 (L1/L2/L3) 与会话互斥租约]
      ↓
[Gate 4: 家长端“今日探索简报”与证据溯源]
      ↓
[Gate 5: 动态环境上下文 (Context) 与受控工具调用 (Capabilities)]
      ↓
[Gate 6: BuddyCanvas 动态互动 UI 与双向事件回传]
      ↓
[Gate 7: 儿童安全防护、系统级回归与可部署工程交付]
```

---

## 二、逐 Gate 详细实施任务与产出物

### Gate 0: 现状代码审计与基线核对 (已交付)
- [x] 代码仓库与 Git 状态检查
- [x] Phase 0 八项修复代码逐行比对 (`PHASE0_IMPLEMENTATION_PARITY.md`)
- [x] 构建与回归基线报告 (`PHASE0_REGRESSION_REPORT.md`)
- [x] 当前能力审计 (`PHASE1_CURRENT_STATE_AUDIT.md`)
- [x] 目标架构蓝图 (`PHASE1_ARCHITECTURE.md`)
- [x] 技术差距分析 (`PHASE1_GAP_ANALYSIS.md`)
- [x] 整体实施规划 (`PHASE1_IMPLEMENTATION_PLAN.md`)

### Gate 0.5: Realtime Function Calling / 信令中继架构设计
- 验证 Azure GPT-Realtime-2.1 工具配置与 Schema
- 设计 SDP 协商中继 / Sideband 通道 / SSE UI 控制总线备选方案
- 产出交付物：`docs/PHASE1_REALTIME_TOOLCALLING_POC.md`

### Gate 1: 家庭、家长与儿童数据底座
- 引入 EF Core 支持（支持 Azure SQL 生产与 SQLite 研发环境）
- 建立核心实体：`Family`, `Guardian`, `ChildProfile`, `DeviceBinding`, `ConsentRecord`
- 实现 Guardian 身份抽象 `IGuardianIdentityProvider` 及 Development Stub
- 实现家长端 6 位安全 PIN 码及哈希限流
- 严格的服务层数据隔离（租户隔离拦截器）
- 编写数据库迁移脚本与初始数据种子
- 产出交付物：`docs/PHASE1_ERD.md`, `docs/PHASE1_CONSENT_MATRIX.md`, `docs/PHASE1_DATA_CLASSIFICATION.md` 及后端 API

### Gate 2: 会话持久化、转写结构化与 Outbox 异步引擎
- 设计与实现 `Session` 实体与生命周期管理
- 实现客户端转写流式聚合与整理（处理乱序、重复、Barge-in）
- 实现服务端 `/api/sessions/{id}/checkpoint` 与 `/api/sessions/{id}/finalize` 接口（幂等、防篡改）
- 建立 `OutboxJob` 实体与持久化任务表
- 实现 `OutboxBackgroundService`（分布式原子认领、租约超时、指数退避重试）
- 建立转写安全临时清理机制

### Gate 3: 跨设备共享记忆体系 (L1/L2/L3) 与会话互斥租约
- 建立三层记忆结构实体：`MemoryFact`、`SessionSummary`、`ActiveTopic`
- 实现 `MemoryContextBuilder`：提炼 300~600 Token 精炼记忆上下文并注入 `session.instructions`
- 实现 Memory CRUD 及按 `ChildProfile` 事务控制
- 实现 `ChildSessionLease` 互斥租约机制（跨设备单会话互斥，友好排他提示）
- 产出交付物：`docs/PHASE1_MEMORY_SCHEMA.md` 及 20+ 跨 Session 自动化测试用例

### Gate 4: 家长端“今日探索简报”与证据溯源
- 设计今日探索简报 Schema（话题、好奇心闪光点、亲子互动建议、待续话题）
- 研发简报异步生成管线（严格关联会话证据，严禁虚构与心理打分）
- 实现家长端查看与删除 API
- 实现前端家长端专属简报卡片 UI
- 产出交付物：`docs/PHASE1_SUMMARY_SCHEMA.md`

### Gate 5: 动态环境上下文 (Context) 与受控工具调用 (Capabilities)
- 实现 `ContextSnapshot`（UTC 基准、本地时间、IANA 时区、经授权城市）
- 实现 `ToolRegistry` 与受控接口 `IBuddyTool`
- 接入时间计算、确定性数学计算工具
- 抽象 `IPublicSearchProvider` 检索接口，最小化脱敏查询
- 产出交付物：`docs/PHASE1_CONTEXT_CAPABILITY_DESIGN.md`

### Gate 6: BuddyCanvas 动态互动 UI 与双向事件回传
- 建立受控 JSON Schema 规范（支持 `KnowledgeCard`, `MultipleChoiceQuiz`, `FlashCards`, `InteractiveStory`）
- 前端研发 `BuddyCanvasRenderer.vue` 白名单组件库
- 建立双向 UI 通信机制（SSE 事件流 + POST 交互动作回传）
- 确保交互答题结果实时送回对话上下文继续引导
- 产出交付物：`docs/PHASE1_BUDDYCANVAS_SCHEMA.md`

### Gate 7: 儿童安全防护、系统级回归与工程交付
- 建立 S0/S1/S2 风险等级分类器与 `SafetyEvent` 独立持久化
- 前后端全面单元与集成测试跑通
- 修复遗留死代码与依赖漏洞
- 自动化打包脚本与 Azure App Service 部署配置验收
- 产出交付物：`docs/PHASE1_SAFETY_DESIGN.md`, `docs/PHASE1_TEST_PLAN.md`, `docs/PHASE1_API_CONTRACT.md`
