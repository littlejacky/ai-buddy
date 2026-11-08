# Phase 1 Current State Audit

**项目名称：** AI Buddy  
**审查基线：** `main` 分支代码库完整审计  
**日期：** 2026-11-08  

---

## 一、当前已有能力 (As-Is)

### 1.1 前端核心组件与逻辑
- **`VoiceOrb.vue`**：具备声学可视化能力的呼吸光球，利用 AnalyserNode 计算用户与远端音量 RMS，具备指数平滑滤波，支持成人内敛态与儿童可爱眨眼态。
- **`ParentGate.vue`**：轻量级防误触算术门禁，防止儿童意外切入成人模式或开启系统设置。
- **`SettingsSheet.vue` & `TranscriptSheet.vue`**：抽屉式设置与历史字幕查看面板。
- **`useConversation.ts`**：状态管理组合式函数，统一协调连接、计时（儿童上限 15 分钟 + 自然收尾）、麦克风静音切换、打断、音量分析与字幕聚合。
- **`useSettings.ts`**：基于 `localStorage` 的偏好存储（模式、音色、调试开关）。
- **`realtimeClient.ts` (`client/src/lib/`)**：封装 WebRTC PeerConnection、DataChannel、AudioContext 同步初始化、8s 重连宽限期、Wake Lock 管理与错误捕获。
- **Demo / Mock 链路**：支持无 Azure 环境下的完整交互原型验证 (`?demo=1`)。

### 1.2 后端服务与配置
- **`Program.cs`**：ASP.NET Core 10 Minimal API，包含启动期配置强校验（`ValidateOnStart`）、IP 级限流（`RateLimiter` 10req/min）、可配置 CORS、异常统一收敛（`UseExceptionHandler` 隐藏堆栈）、SPA 静态文件托管与路由 fallback。
- **`RealtimeSessionService.cs`**：负责与 Azure OpenAI `POST /openai/v1/realtime/client_secrets` 通信，签发 10～600 秒短期凭据；注入适龄 Persona 指令与儿童 800ms 静音判定配置。
- **`PersonaService.cs`**：提供成人朋友与儿童伙伴（5～12岁）两套精心调试的 System Instructions，包含口语化规则、安全指引与收尾引导。

---

## 二、当前缺失能力与工程缺口 (To-Be Gap)

| 领域 | 当前状态 | Phase 1 目标需求 | 严重等级 |
|---|---|---|:---:|
| **数据持久化** | 无数据库，无 ORM，全内存/无状态 | 引入 EF Core + Azure SQL / SQLite 双适配，统一持久化模型 | **P0** |
| **家长身份与账号** | 仅有前端算术题防误触，无登录、无会话 | 建立 Guardian 身份抽象、Development Stub、PIN 辅助安全校验 | **P0** |
| **家庭与儿童档案** | 仅在前端切换 adult/child 字符串 | Family / Guardian / ChildProfile / Device 关系模型，支持跨设备绑定 | **P0** |
| **授权与隐私合规** | 仅单条免责声明文本 | 细粒度 ConsentRecord (基础语音/记忆/日报/转写/位置/搜索) 与服务层鉴权拦截 | **P0** |
| **会话持久化与转写** | 会话结束即销毁，转写丢弃 | Session 业务实体、Checkpoint、Finalize 幂等持久化与结构化转写归档 | **P0** |
| **异步后台任务** | 无任务队列，无持久化异步调度 | Outbox 模式 + 数据库事务 + BackgroundService 健壮消费状态机 | **P0** |
| **跨设备长期记忆** | 无跨 Session 记忆，每次均为白板对话 | 三层记忆架构 (L1事实/L2近期摘要/L3兴趣待续)，Session Instructions 注入，Session Lease 互斥租约 | **P0** |
| **家长探索简报** | 无简报生成与查看界面 | Today's Discovery 探索简报生成器、证据关联、家长端专属查看与删除 API | **P1** |
| **儿童安全机制** | 仅依靠 Prompt 软性提示 | S0/S1/S2 风险等级分类、SafetyEvent 独立加密与家长访问审计 | **P1** |
| **Context 环境层** | 无时间/时区感知，无位置感知 | 动态 ContextSnapshot (UTC/本地时间/IANA时区/城市)，严格与长期记忆解耦 | **P1** |
| **Capability 工具层** | 无 Function Calling，无工具注册表 | IBuddyTool 注册中心、工具权限过滤、时间和确定性计算支持 | **P1** |
| **联网搜索服务** | 无联网检索能力 | IPublicSearchProvider 接口抽象、可信摘要提炼、最小化儿童隐私查询 | **P1** |
| **BuddyCanvas 动态UI** | 仅有纯语音和字幕气泡 | 结构化交互卡片 (知识卡/选择问答/翻转卡/故事分支)，双向事件送回对话 | **P1** |
| **自动化测试体系** | 0 单元测试，0 集成测试 | xUnit 后端测试 + Vitest 前端测试 + 端到端全链路集成覆盖 | **P0** |

---

## 三、代码级风险与技术债务
1. **遗留死代码**：`client/src/services/` 下留存早期未修复的 `realtimeClient.ts` 与 `api.ts`，未被任何组件使用，容易造成代码误判。
2. **WebRTC `/calls` 传参**：缺少 `?webrtcfilter=on`，应显式补齐。
3. **NuGet 依赖漏洞警告**：`Microsoft.OpenApi 2.0.0` 存在已知安全建议，需更新或受控处理。
4. **数据库设计前置**：必须保证服务层强行校验 Tenant/Family 隔离，杜绝越权访问儿童数据。
