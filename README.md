# Buddy · AI 语音伙伴

浏览器（Vue PWA）⇄ WebRTC ⇄ Azure OpenAI Realtime；后端（ASP.NET Core）只负责签发**一次性临时密钥**，真正的 API Key 不出服务器。

```
client/   Vue 3 + Vite PWA（手机优先）
server/   ASP.NET Core 10 Minimal API
```

## 先看界面（无需 Azure）

```bash
cd client && npm install && npm run dev
# 浏览器打开 http://localhost:5173/?demo=1
# 预览错误态：?demo=1&fail=mic    调试面板：?debug=1
```

## 连接真实服务

```bash
cd server/Buddy.Api
dotnet user-secrets init
dotnet user-secrets set "AzureRealtime:Endpoint"       "https://<资源名>.openai.azure.com"
dotnet user-secrets set "AzureRealtime:ApiKey"         "<key>"
dotnet user-secrets set "AzureRealtime:DeploymentName" "<realtime 部署名>"
dotnet run            # http://localhost:5266；缺配置会在启动时直接报错
# 另一个终端：cd client && npm run dev   （/api 已代理到 5266）
```

## 约定

- 可调策略放服务端配置：`ChildMaxSessionSeconds`（孩子单次时长）、CORS 白名单、限流（每 IP 每分钟 10 次）。
- 麦克风需要 HTTPS（localhost 除外）。手机真机调试请用 HTTPS 隧道或局域网证书。
- npm 镜像：本仓库不锁定镜像源；在国内可 `npm config set registry https://registry.npmmirror.com`。

## 已知限制 / 待办

见 `REVIEW.md` 的“尚未解决”一节。
