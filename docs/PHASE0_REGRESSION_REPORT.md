# Phase 0 Baseline & Regression Report

**项目名称：** AI Buddy  
**测试基线：** Phase 0 + Gate 1 完整实现 (Commit `120ca0c`)  
**测试环境：** Windows 11 / .NET 10.0.100-preview / Node v24+ / Vite 8.3.5 / Vue 3.5.44  
**日期：** 2026-11-08  

---

## 一、本地构建与静态类型验证

### 1.1 前端客户端构建 (`client`)
- **命令：** `cd client && npm run build` (`vue-tsc -b && vite build`)
- **输出结果：**
  ```text
  vite v8.3.5 building client environment for production...
  transforming...
  ✓ 36 modules transformed.
  rendering chunks...
  computing gzip size...
  dist/registerSW.js               0.13 kB
  dist/manifest.webmanifest        0.41 kB
  dist/index.html                  1.18 kB │ gzip:  0.61 kB
  dist/assets/index-Bz7dleZg.css  12.98 kB │ gzip:  3.57 kB
  dist/assets/index-ISz8h7Vf.js   93.07 kB │ gzip: 36.20 kB
  ✓ built in 2.18s
  ```
- **结论：** 前端 TypeScript 类型检查 100% 通过，生产包打包成功，无语法与类型错误。

### 1.2 后端服务构建 (`server/Buddy.Api`)
- **命令：** `cd server/Buddy.Api && dotnet build`
- **输出结果：**
  ```text
  已还原 D:\Lab\test\buddy\server\Buddy.Api\Buddy.Api.csproj (用时 1.55 秒)。
  Buddy.Api -> D:\Lab\test\buddy\server\Buddy.Api\bin\Debug\net10.0\Buddy.Api.dll
  已成功生成。
      0 个错误，2 个警告（NU1903 Microsoft.OpenApi 2.0.0 建议升级）
  已用时间 00:00:11.55
  ```
- **结论：** 后端 C# 13 / .NET 10 Minimal API 编译 0 错误，可直接托管启动。

---

## 二、真实 Azure 链路与实测性能基线

根据 Phase 0 技术评估报告及探针实测数据，记录当前生产验证基线：

### 2.1 Azure 模型与部署拓扑
- **模型版本：** `gpt-realtime-2.1`
- **接入点：** East US 2 Global Standard 部署
- **协议栈：**
  - 服务端信令：`POST /openai/v1/realtime/client_secrets` 获取临时凭据 `ek_...` (GA REST)
  - 浏览器直连：WebRTC SDP Offer/Answer 提交至 `/openai/v1/realtime/calls` (HTTP 201)
  - 媒体信道：双向 DTLS/SRTP 音频媒体流，启用 Opus 48kHz，端侧开启 AEC (回声消除)、NS (降噪)、AGC (自动增益)
  - 数据信道：`RTCDataChannel ('oai-events')`

### 2.2 实测 TTFS (Time-to-First-Speech) 基线
- **统计样本：** 50 次有效语音交互摸底
- **时延中位数 (P50)：** 610 ms
- **时延区间：** 580 ms ～ 750 ms
- **四段式度量拆解：**
  1. $\Delta T_{\text{net+server}}$（话毕检测与云端接收）：~150ms（含 Server VAD 300ms prefix padding 及静音判定）
  2. $\Delta T_{\text{inference}}$（模型首 Token 及音频生成）：~280ms
  3. $\Delta T_{\text{media\_recv}}$（媒体网络下行至端侧）：~80ms
  4. $\Delta T_{\text{decode}}$（端侧 Web Audio 解码与缓冲区排空）：~100ms

### 2.3 WebRTC 底层指标 (`getStats()`)
- **RTT (UDP/DTLS往返)：** 45ms ～ 90ms（跨洋/全球调度节点波动）
- **丢包率 (Packet Loss)：** < 0.2%
- **音频下行抖动 (Jitter)：** < 8ms
- **打断 (Barge-in) 表现：** 本地检测打断到云端排空下行，听觉截断感约为 120ms ～ 200ms。

---

## 三、回归检查结论

| 测试项 | 验证手段 | 结果 | 备注 |
|---|---|:---:|---|
| 前端静态打包 | `vue-tsc` + `vite build` | PASS | 0 类型报错，资源尺寸在轻量范围内 |
| 后端构建 | `dotnet build` | PASS | 0 错误，支持 Minimal API 与 SPA 静态挂载 |
| Demo 模式 | `?demo=1` | PASS | 状态机平滑运转，Orb 动画随音量起伏 |
| 弱网重连 | 模拟断网与重连 | PASS | 8s 宽限期生效，UI 正确显示重连提示 |
| 家长门禁 | `ParentGate.vue` | PASS | 防误触算术题拦截成人模式及设置面板 |

**回归结论：** Phase 0 基线功能完整且健康，满足进入 Phase 1 的前置条件。
