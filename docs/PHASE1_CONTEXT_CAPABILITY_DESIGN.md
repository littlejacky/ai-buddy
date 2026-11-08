# Phase 1 Context & Capability Layer Design

**项目名称：** AI Buddy  
**设计目标：** 现实环境感知 (Context) + 受控工具调用 (Capabilities)  

---

## 一、ContextSnapshot 动态环境上下文

```json
{
  "capturedAtUtc": "2026-11-08T02:00:00Z",
  "localDateTime": "2026-11-08T10:00:00+08:00",
  "dayOfWeek": "Sunday",
  "timeZone": "Asia/Shanghai",
  "locale": "zh-CN",
  "language": "zh-CN",
  "location": {
    "available": true,
    "source": "guardian_set",
    "city": "北京市",
    "region": "北京",
    "country": "中国"
  }
}
```

### 1.1 时间与相对日期计算原则
- 客户端在启动握手时上报当前设备的时区（`Intl.DateTimeFormat().resolvedOptions().timeZone`）与本地时间。
- 服务端据此构建“当前现实时空锚点”并注入 Session Instructions：
  ```text
  【现实时空背景】
  - 当前北京时间：2026年11月8日，星期日 上午 10:00
  - 今天是星期天，昨天是星期六（7号），明天是星期一（9号）
  - 请基于此时空准确回答小朋友关于今天、明天、日期的提问。
  ```

---

## 二、受控 Capability Registry 架构

```csharp
public interface IBuddyTool
{
    string Name { get; }
    string Description { get; }
    JsonObject ParameterSchema { get; }
    string RequiredConsent { get; }
    Task<BuddyToolResult> ExecuteAsync(BuddyToolContext context, JsonElement arguments, CancellationToken ct);
}
```

### 2.1 首批登记工具清单

1. **`get_current_time`**：直接由 Context Provider 内存解析，0 外部网络耗时。
2. **`deterministic_calculator`**：支持加减乘除、简单单位换算，解决大模型计算幻觉。
3. **`query_weather`**：基于监护人设定的城市或单次授权的近似位置获取最新气温与天气，附带数据观测时间戳。
4. **`public_web_search`**：基于 `IPublicSearchProvider` 的可信网络知识库检索，严格过滤广告与儿童不宜内容，提炼可信来源。
5. **`spawn_canvas_activity`**：触发前端 BuddyCanvas 动态互动卡片。

### 2.2 防护网：防范 Prompt Injection
- 所有来自互联网搜索或外部 API 的返回内容，均标记为 `Untrusted External Data`。
- 在工具结果注入前进行安全剥离，过滤任何试图覆盖系统 Persona 或绕过儿童安全规则的恶意文本。
