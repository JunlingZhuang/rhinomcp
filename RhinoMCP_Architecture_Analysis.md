# RhinoMCP 项目架构分析文档

## 项目概述

**RhinoMCP** 是一个创新的集成项目，它通过 **Model Context Protocol (MCP)** 将 AI 代理（如 Claude Desktop 和 Cursor）与 **Rhino 3D** 建模软件连接起来，实现 AI 辅助的 3D 建模功能。

## 系统组件详解

### 1. **AI 代理层 (Client Side)**

- **支持的平台**: Claude Desktop, Cursor IDE
- **通信协议**: Model Context Protocol (MCP)
- **配置方式**: 通过 JSON 配置文件设置 MCP 服务器连接

### 2. **MCP 服务器 (Python)**

**位置**: `rhino_mcp_server/`

**核心文件**:

- `src/rhinomcp/server.py` - 主服务器实现，管理与 AI 代理的 MCP 通信
- `main.py` - 入口点，启动服务器
- `tools/` - 包含所有可用的工具函数（如 create_object.py）

**功能特性**:

- 实现 MCP 协议标准
- 管理与 Rhino 插件的 TCP 连接
- 提供丰富的工具函数供 AI 调用
- 错误处理和连接重试机制

### 3. **TCP 通信层**

- **协议**: JSON over TCP Socket
- **默认地址**: 127.0.0.1:1999
- **数据格式**:
  ```json
  {
    "type": "command_type",
    "params": { ... }
  }
  ```

### 4. **Rhino 插件 (C#)**

**位置**: `rhino_mcp_plugin/`

**核心组件**:

- `RhinoMCPServer.cs` - TCP 服务器实现，监听来自 MCP 服务器的请求
- `RhinoMCPPlugin.cs` - 主插件类
- `Functions/` - 功能实现目录
- `Commands/` - Rhino 命令实现

**主要功能**:

- 在 Rhino 内部创建 TCP 服务器
- 解析 JSON 命令并执行相应操作
- 直接操作 Rhino 文档和几何体
- 返回操作结果给 MCP 服务器

### 5. **Rhino 3D 软件**

- 最终的 3D 建模环境
- 接收插件的几何体创建/修改指令
- 实时更新视图显示

## 数据流工作原理

```
用户输入 → AI 代理 → MCP 协议 → Python 服务器 → TCP Socket → Rhino 插件 → Rhino 3D → 结果反馈
```

## 支持的功能

### **几何体操作**

- **创建**: 点、线、多段线、圆、弧、椭圆、曲线、盒子、球体、锥体、圆柱体、曲面
- **修改**: 移动、旋转、缩放、颜色设置
- **删除**: 按 ID 或名称删除对象

### **文档管理**

- 获取文档信息
- 选择对象（支持多条件筛选）
- 图层管理（创建、删除、设置当前图层）

### **脚本执行**

- 执行 RhinoScript Python 代码
- 获取 RhinoScript 函数文档
- 动态代码生成和执行

## 详细的类设计架构

## Python 端类设计

### 1. **FastMCP 服务器类**

- **作用**: MCP 协议的核心实现
- **关键特性**:
  - 使用装饰器模式注册工具函数
  - 生命周期管理（startup/shutdown）
  - 异步处理 AI 代理请求

### 2. **RhinoConnection 类**

```python
@dataclass
class RhinoConnection:
    host: str
    port: int
    sock: socket.socket | None = None
```

- **职责**: 管理与 Rhino 插件的 TCP 连接
- **核心方法**:
  - `connect()`: 建立连接，错误处理
  - `send_command()`: 发送 JSON 命令，等待响应
  - `receive_full_response()`: 处理分块接收和超时

### 3. **工具函数 (装饰器模式)**

```python
@mcp.tool()
def create_object(ctx: Context, type: str, params: Dict, ...):
```

- **设计模式**: 装饰器模式，自动注册为 MCP 工具
- **特点**: 每个函数独立，易于扩展和维护

## C# 端类设计

### 1. **插件主类 - RhinoMCPPlugin**

```csharp
public class RhinoMCPPlugin : Rhino.PlugIns.PlugIn
{
    public static RhinoMCPPlugin Instance { get; private set; }
}
```

- **继承关系**: 继承自 Rhino.PlugIns.PlugIn
- **设计模式**: 单例模式
- **职责**: Rhino 插件的入口点

### 2. **服务器控制器 - RhinoMCPServerController**

```csharp
class RhinoMCPServerController
{
    private static RhinoMCPServer server;
    public static void StartServer()
    public static void StopServer()
    public static bool IsServerRunning()
}
```

- **设计模式**: 静态工厂模式
- **职责**: 管理服务器实例的生命周期

### 3. **TCP 服务器 - RhinoMCPServer**

```csharp
public class RhinoMCPServer
{
    private TcpListener listener;
    private Thread serverThread;
    private RhinoMCPFunctions handler;
}
```

- **核心组件**:
  - `TcpListener`: .NET 的 TCP 监听器
  - `Thread`: 后台服务线程
  - `RhinoMCPFunctions`: 功能处理器
- **设计特点**: 多线程处理，每个客户端连接独立线程

### 4. **功能实现类 - RhinoMCPFunctions**

```csharp
public partial class RhinoMCPFunctions
{
    // 主要功能方法
    public JObject CreateObject(JObject parameters)
    public JObject ModifyObject(JObject parameters)

    // 工具方法
    private double castToDouble(JToken token)
    private RhinoObject getObjectByIdOrName(JObject parameters)
    private Transform applyRotation(JObject parameters, GeometryBase geometry)
}
```

- **关键设计**:
  - **部分类 (partial class)**: 支持功能模块化
  - **类型转换**: 统一的 JSON 到 C# 类型转换
  - **几何变换**: 封装平移、旋转、缩放操作

### 5. **序列化工具 - Serializer**

```csharp
public static class Serializer
{
    public static JObject SerializeColor(Color color)
    public static JArray SerializePoint(Point3d pt)
    public static JObject RhinoObject(RhinoObject obj)
}
```

- **设计模式**: 静态工具类
- **职责**: Rhino 对象到 JSON 的双向转换

### 6. **命令类 - Command 继承体系**

```csharp
public class MCPStartCommand : Command
{
    public static MCPStartCommand Instance { get; private set; }
    public override string EnglishName => "mcpstart";
    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
}
```

- **继承关系**: 继承自 Rhino.Commands.Command
- **设计模式**: 单例模式 + 命令模式
- **命令类型**: MCPStart, MCPStop, MCPVersion

## 架构设计亮点

### 1. **分层设计**

- **表示层**: AI 代理界面
- **协议层**: MCP 标准协议
- **业务层**: Python 工具函数
- **通信层**: TCP Socket JSON
- **执行层**: C# Rhino 操作
- **数据层**: Rhino 文档对象

### 2. **设计模式应用**

- **单例模式**: 插件实例、命令实例
- **工厂模式**: 服务器控制器
- **装饰器模式**: Python 工具注册
- **命令模式**: Rhino 命令系统
- **策略模式**: 不同几何体创建策略

### 3. **错误处理与容错**

- **连接重试**: RhinoConnection 自动重连
- **超时处理**: Socket 操作超时保护
- **异常传播**: 从 C# 到 Python 的错误信息传递
- **资源清理**: 连接和线程的正确释放

### 4. **扩展性设计**

- **部分类**: C# 功能类支持模块化扩展
- **装饰器**: Python 工具函数易于添加
- **JSON 协议**: 松耦合的通信格式
- **类型转换**: 统一的数据类型处理框架

## 技术特点

### **优势**

1. **双向通信**: AI 可以读取 Rhino 状态并执行操作
2. **实时性**: 通过 Socket 连接实现低延迟通信
3. **扩展性**: 模块化设计，易于添加新功能
4. **跨平台**: 支持 Windows 和 Mac 上的 Rhino

### **安全考虑**

- 本地连接（127.0.0.1），避免网络暴露
- 命令验证和错误处理
- 超时机制防止死锁

## 安装和使用流程

1. **安装 Rhino 插件** → Package Manager 搜索 "rhinomcp"
2. **配置 MCP 服务器** → AI 代理的配置文件中添加 rhinomcp
3. **启动连接** → Rhino 中运行 `mcpstart` 命令
4. **开始使用** → 通过 AI 代理发送 3D 建模指令

## 总结

这个架构的设计非常巧妙，它利用 MCP 协议作为标准化的 AI-工具通信接口，通过本地 Socket 连接实现了 AI 与专业 3D 软件的深度集成，为 AI 辅助设计开辟了新的可能性。整个系统实现了高内聚、低耦合的模块化结构，既保证了系统的稳定性，又提供了良好的扩展性，是一个非常优秀的跨语言、跨平台集成方案。

---

_文档生成时间: 2024 年_
_分析对象: RhinoMCP 项目架构_
