# Match-3 游戏性能优化研究报告

> **目标平台**: WebGL 2.0 (HTML5) + 移动端
> **游戏引擎**: Godot 4.x (Compatibility 渲染器)
> **游戏类型**: 2D Match-3 (消消乐)

---

## 目录

1. [WebGL 2.0 限制与最佳实践](#1-webgl-20-限制与最佳实践)
2. [Godot 4.x HTML5 导出优化](#2-godot-4x-html5-导出优化)
3. [Shader 优化策略](#3-shader-优化策略)
4. [粒子系统优化](#4-粒子系统优化)
5. [对象池模式 —— 瓦片复用](#5-对象池模式--瓦片复用)
6. [Texture Atlas 纹理图集](#6-texture-atlas-纹理图集)
7. [内存管理](#7-内存管理)
8. [帧率目标与渲染策略](#8-帧率目标与渲染策略)
9. [移动端专项优化](#9-移动端专项优化)
10. [Godot 4.x Compatibility 渲染器](#10-godot-4x-compatibility-渲染器)
11. [资源压缩策略](#11-资源压缩策略)
12. [渐进式加载策略](#12-渐进式加载策略)

---

## 1. WebGL 2.0 限制与最佳实践

### 1.1 核心限制

WebGL 2.0 基于 OpenGL ES 3.0，以下是各设备的最低保证限制（来源：MDN / WebGL 2.0 规范）：

| 限制项 | 最低保证值 | 说明 |
|--------|-----------|------|
| `MAX_TEXTURE_SIZE` | 4096 | 约 99% 设备支持 4096，仅 50% 支持 > 4096 |
| `MAX_CUBE_MAP_TEXTURE_SIZE` | 4096 | 立方体贴图最大尺寸 |
| `MAX_RENDERBUFFER_SIZE` | 4096 | 渲染缓冲区最大尺寸 |
| `MAX_VIEWPORT_DIMS` | [4096, 4096] | 视口最大维度 |
| `MAX_VERTEX_ATTRIBS` | 16 | 顶点属性最大数量 |
| `MAX_VARYING_VECTORS` | 16 | varying 变量最大数量 |
| `MAX_TEXTURE_IMAGE_UNITS` (FS) | 16 | 片段着色器纹理单元 |
| `MAX_VERTEX_TEXTURE_IMAGE_UNITS` | 16 | 顶点着色器纹理单元 |
| `MAX_COMBINED_TEXTURE_IMAGE_UNITS` | 32 | 总纹理单元数 |
| `MAX_VERTEX_UNIFORM_VECTORS` | 256 | 顶点着色器 uniform 向量 |
| `MAX_FRAGMENT_UNIFORM_VECTORS` | 224 | 片段着色器 uniform 向量 |
| `MAX_DRAW_BUFFERS` | 4 | 多渲染目标数量 |
| `MAX_3D_TEXTURE_SIZE` | 256 | 3D 纹理最大尺寸 |
| `MAX_ARRAY_TEXTURE_LAYERS` | 256 | 纹理数组最大层数 |

### 1.2 Draw Call 限制

**关键发现**：

- **桌面端**：WebGL draw call 比原生 OpenGL 有显著额外开销（浏览器验证 + marshalling），但可以承受 2000-4000 次/帧
- **移动端**：Mobile WebGL 对 draw call 极其敏感，建议控制在 **100-300 次/帧** 以内
- Safari (iOS/macOS) 额外问题：`bufferSubData()` 调用开销特别大

**WebGL Draw Call 开销来源**：
1. JavaScript → 浏览器进程 marshalling（序列化调用）
2. 浏览器安全检查（沙箱验证每个 WebGL 调用）
3. GPU 进程通信开销

**优化策略**：
```
目标：将 draw call 降低到 100 以下（移动端 WebGL）
方法：纹理图集 + 批处理 + 减少材质切换
```

### 1.3 Texture Memory 限制

- **iOS Safari**：浏览器标签页有严格的内存限制，旧款 iPhone 尤甚。超出限制会导致页面重新加载或冻结。
- **统一内存架构**：移动端 GPU 和 CPU 共享内存，纹理 + buffer + WASM 内存共同计入限制。
- 建议每像素 VRAM 预算策略（Google Maps 团队方法论）。

**Match-3 游戏纹理预算建议**：

| 纹理类型 | 建议尺寸 | 内存占用 (RGBA8) |
|----------|---------|-------------------|
| 晶体图集 (所有类型) | 1024×1024 | ~4 MB |
| UI 图集 | 512×512 | ~1 MB |
| 粒子精灵表 | 512×512 | ~1 MB |
| 背景 (分割为 TileMap) | 各 512×512 | 按需 |
| **总计预算** | | **< 15 MB** |

---

## 2. Godot 4.x HTML5 导出优化

### 2.1 渲染器选择

**Godot 4.x HTML5 导出只能使用 Compatibility 渲染器**（基于 OpenGL 3.3 / OpenGL ES 3.0 / WebGL 2.0）。Forward+ 和 Mobile 渲染器不支持 Web 平台（需要 Vulkan/Metal/D3D12，尚不支持 WebGPU）。

### 2.2 WASM 文件大小优化

Godot 4.3 官方 Web 导出模板的 `.wasm` 文件约 **40 MB 未压缩** / **5 MB Brotli 压缩后**。

**缩减方法**：

#### 方法一：编译自定义导出模板（去除不需要的模块）

```python
# custom.py — SCons 配置文件
platform = "web"
optimize = "size"
target = "template_release"

# 2D 游戏必备关闭项
disable_3d = "yes"                    # 禁用 3D 渲染器（省 ~7MB）
module_text_server_adv_enabled = "no"  # 禁用高级文本服务器
module_text_server_fb_enabled = "yes"  # 使用回退文本服务器
module_mono_enabled = "no"             # 禁用 C#/Mono（如果用 GDScript）
module_bmp_enabled = "no"             # 禁用 BMP 导入
module_csg_enabled = "no"             # 禁用 CSG
module_dds_enabled = "no"             # 禁用 DDS 纹理
module_enet_enabled = "no"            # 禁用 ENet 网络（如不需要）
module_gdscript_enabled = "yes"       # 确认保留 GDScript
module_gridmap_enabled = "no"         # 禁用 GridMap
module_hdr_enabled = "no"             # 禁用 HDR
module_jsonrpc_enabled = "no"         # 禁用 JSON-RPC
module_mobile_vr_enabled = "no"       # 禁用移动 VR
module_navigation_enabled = "no"      # 禁用导航网格（2D 不需要）
module_ogg_enabled = "yes"            # 保留 OGG（音频）
module_opensimplex_enabled = "no"     # 禁用 OpenSimplex 噪声
module_raycast_enabled = "yes"        # 保留射线检测
module_regex_enabled = "yes"          # 保留正则表达式
module_svg_enabled = "no"             # 禁用 SVG
module_theora_enabled = "no"          # 禁用 Theora 视频
module_tinyexr_enabled = "no"         # 禁用 EXR
module_upnp_enabled = "no"            # 禁用 UPnP
module_vhacd_enabled = "no"           # 禁用 VHACD
module_webrtc_enabled = "no"          # 禁用 WebRTC
module_webp_enabled = "yes"           # 保留 WebP
module_websocket_enabled = "no"       # 禁用 WebSocket（如不需要）
module_xatlas_unwrap_enabled = "no"   # 禁用 xatlas
```

编译命令：
```bash
scons platform=web target=template_release
# 效果：wasm 从 32MB 降至 ~15-19MB（视禁用模块数量）
```

#### 方法二：WASM 优化工具

```bash
# 使用 wasm-opt（Binaryen 工具链）进一步优化
wasm-opt -Oz input.wasm -o output.wasm
# 效果：额外减少 15-30%
```

#### 方法三：服务端压缩

| 压缩方式 | 压缩率 | 说明 |
|----------|--------|------|
| Brotli | ~87% | 40MB → ~5MB，推荐首选 |
| Gzip | ~80% | 40MB → ~8MB，兼容性最好 |
| Zopfli | ~82% | Gzip 兼容但压缩率更高 |

**重要**：确保 `.wasm` 文件使用 `application/wasm` MIME 类型以启用浏览器启动优化。

### 2.3 .pck 文件大小优化

.pck 文件包含所有项目资源（代码、图片、音频）。

#### 图片导入设置

| 压缩模式 | 内部格式 | 说明 |
|----------|---------|------|
| **Lossy（推荐）** | WebP | 通过 Lossy Quality 滑块控制质量，显著减小体积 |
| Lossless | PNG | 适合需要保持 PNG 格式的源文件 |
| VRAM Compressed | S3TC/ETC2 | GPU 直接使用，但占用磁盘空间更大 |

**关键建议**：
- 在项目文件夹中使用全质量资源，通过 Import 选项卡调整导出质量
- Lossy Quality 设为 75-85% 通常视觉差异不可察觉
- 批量修改：在 FileSystem 中多选图片 → 修改 Import 设置 → 点击 Reimport
- 关闭 "Export with Debug" 可减少 ~15% 体积

### 2.4 单线程 vs 多线程导出

| 导出模式 | 优点 | 缺点 |
|----------|------|------|
| **单线程（推荐，Godot 4.3+ 默认）** | 不需要 COOP/COEP 头设置，兼容 itch.io、Poki 等平台，macOS/iOS 兼容性好 | 无多线程，音频使用 Sample 模式 |
| 多线程 | 可使用多线程，低延迟 Stream 音频播放 | 需要服务器设置 COOP/COEP 跨域隔离头，无法集成第三方广告/API |

**Match-3 游戏建议**：使用单线程导出。Match-3 的计算负载不需要多线程，且单线程导出兼容性更好。

### 2.5 音频设置（Web 平台）

```gdscript
# 项目设置 → Audio → General
# Default Playback Type.web = "Sample"  (Godot 4.3+ 默认)
#
# Sample 模式优点：低延迟，不会因帧率下降产生杂音
# Sample 模式限制：不支持 AudioEffect，不支持混响/多普勒
#
# 如需使用 Stream 模式：
# Output Latency.web = 80  (增大缓冲防止卡顿)
```

**预注册音频样本**（低端设备避免首次播放卡顿）：
```gdscript
# 在关卡加载时预注册
AudioServer.register_stream_as_sample(preload("res://audio/sfx_match.ogg"))
AudioServer.register_stream_as_sample(preload("res://audio/sfx_swap.ogg"))
```

---

## 3. Shader 优化策略

### 3.1 GLSL ES 精度建议

WebGL 2.0 使用 ESSL 3.00，精度最低保证如下（MDN 数据）：

| 类型 | 精度 | 范围 | 精度（相对） |
|------|------|------|-------------|
| `highp float` | IEEE float32 | (-2^126, 2^127) | 2^-24 relative |
| `mediump float` | IEEE float16 | (-2^14, 2^14) | 2^-10 relative |
| `lowp float` | 10-bit signed fixed | (-2, 2) | 2^-8 absolute |

**关键警示**：
- 桌面端 GPU 始终用 `highp` 模拟 `mediump`/`lowp`——开发时看不出差异
- 移动端实际使用 `mediump`/`lowp`——可能导致渲染错误
- `mediump float` 范围仅 (-16384, 16384)，在 UV 坐标偏移时可能出现精度不足

**推荐模式（始终获取最高精度）**：
```glsl
// 片段着色器开头
#ifdef GL_FRAGMENT_PRECISION_HIGH
precision highp float;
#else
precision mediump float;
#endif
```

**Match-3 游戏中的精度策略**：

```glsl
// 顶点着色器默认已是 highp
// 片段着色器中：
precision mediump float;  // 颜色计算用 mediump 即可
// 但 sampler2D 默认为 lowp，需要时显式声明
precision highp sampler2D; // iOS 上必须！否则纹理采样可能是 lowp
```

### 3.2 避免昂贵操作

| 避免 | 替代方案 |
|------|---------|
| `pow(x, y)` 当 x < 0（未定义行为） | 使用条件判断 |
| 片段着色器中的复杂光照 | 移到顶点着色器计算 |
| 动态分支 (`if`/`switch`) | 使用 `mix()`/`step()` |
| `discard` 语句 | 使用 alpha blending |
| `sin()`/`cos()` 在片段着色器中 | 预计算或顶点着色器处理 |
| 多次纹理采样 | 合并纹理或使用 texture atlas |

### 3.3 Match-3 晶体着色器优化示例

```glsl
// crystal_optimized.gdshader — Compatibility 渲染器兼容
shader_type canvas_item;

uniform sampler2D crystal_atlas : source_color;
uniform vec4 modulate_color : source_color = vec4(1.0);
uniform float glow_amount = 0.0;
uniform float time_sec = 0.0;

void fragment() {
    // 直接从 atlas 采样（避免额外纹理查找）
    vec4 color = texture(crystal_atlas, UV);
    
    // 简单的调制颜色（避免 pow/exp）
    color.rgb *= modulate_color.rgb;
    
    // 发光效果：使用 smoothstep 而非 sin/cos（更快）
    float glow = smoothstep(0.0, 1.0, glow_amount);
    color.rgb += glow * 0.15;
    
    // 直接输出，不使用 discard（避免 Mobile GPU 性能损失）
    COLOR = color;
}
```

### 3.4 Compatibility 渲染器 Shader 限制

- **不支持** Compute Shaders
- **不支持** `hint_depth_texture` / `hint_normal_roughness_texture`
- **不支持** Decals、Particle Trails
- **最多 8 个 OmniLight / SpotLight** per mesh
- **不支持** 后处理特效链（CompositorEffects）

---

## 4. 粒子系统优化

### 4.1 粒子数量限制

| 平台 | 建议最大粒子数 | 说明 |
|------|--------------|------|
| 桌面 WebGL | 500-1000 | 可承受更多 |
| 移动端 WebGL | 100-200 | 严格限制 |
| 低端 Android WebView | 50-100 | 极度受限 |

### 4.2 使用 Sprite Sheet（Flipbook）

Godot 2D 粒子系统支持 flipbook 动画。将多个粒子帧放入单个纹理中可以：
- 减少纹理切换（降低 draw call）
- 提升缓存命中率

```gdscript
# 设置粒子 flipbook
var particles = $GPUParticles2D
var material = particles.process_material as ParticleProcessMaterial
# 在编辑器中设置：
# Texture → 选择 sprite sheet
# Anim Speed → 调整动画速度
# Anim Offset → 随机起始帧
```

### 4.3 粒子系统优化清单

- [ ] 使用 `GPUParticles2D` 而非 `CPUParticles2D`（GPU 粒子效率更高）
- [ ] 将粒子纹理合并到晶体图集中
- [ ] 对大量粒子使用 `Unshaded` 着色模式（Compatibility 渲染器中更快）
- [ ] 预加载粒子场景，避免首次触发生成
- [ ] 使用 `amount_ratio` 在低端设备上缩减粒子数量
- [ ] 设置合理的 `lifetime`（越短 = 同时存在的粒子越少）
- [ ] 使用 `visibility_rect` 限制粒子只在可见区域生成
- [ ] 关闭不需要的碰撞检测（粒子碰撞在 Compatibility 中不支持 SDF）

### 4.4 低端设备自适应

```gdscript
# 根据设备性能调整粒子数
func adjust_particles_for_performance():
    var is_mobile = OS.get_name() == "Web" and \
        JavaScriptBridge.eval("""
            /Mobi|Android|iPhone|iPad/i.test(navigator.userAgent)
        """)
    
    if is_mobile:
        $MatchParticles.amount = 30         # 匹配粒子
        $ComboParticles.amount = 20        # 连击粒子
        $MatchParticles.speed_scale = 0.7   # 降低速度
    else:
        $MatchParticles.amount = 80
        $ComboParticles.amount = 60
```

---

## 5. 对象池模式 —— 瓦片复用

### 5.1 为什么需要对象池

在 Godot 中，`add_child()` 和 `queue_free()` 是最昂贵的操作之一：
- 每帧添加/移除数百个节点会导致严重帧率下降
- GDScript 虽然没有 GC 停顿，但节点的创建/销毁涉及资源分配和场景树操作

对于 Match-3 游戏（典型 8×8 网格 = 64 个瓦片，频繁交换/消除/生成），对象池是必需的。

### 5.2 瓦片对象池实现

```gdscript
# TilePool.gd — 瓦片对象池
class_name TilePool
extends Node

var _pool: Array[Node2D] = []
var _tile_scene: PackedScene
var _default_parent: Node

func _init(scene: PackedScene, parent: Node, initial_size: int = 80):
    _tile_scene = scene
    _default_parent = parent
    for i in range(initial_size):
        var tile = _tile_scene.instantiate()
        tile.visible = false
        tile.process_mode = Node.PROCESS_MODE_DISABLED
        _pool.append(tile)

func acquire() -> Node2D:
    var tile: Node2D
    if _pool.is_empty():
        tile = _tile_scene.instantiate()
    else:
        tile = _pool.pop_back()
    
    tile.visible = true
    tile.process_mode = Node.PROCESS_MODE_INHERIT
    return tile

func release(tile: Node2D):
    tile.visible = false
    tile.process_mode = Node.PROCESS_MODE_DISABLED
    # 从场景树中移除（不 free）
    if tile.get_parent():
        tile.get_parent().remove_child(tile)
    _pool.append(tile)

func clear():
    for tile in _pool:
        tile.queue_free()
    _pool.clear()
```

### 5.3 网格管理器使用对象池

```gdscript
# GridManager.gd — 网格管理器（使用对象池）
class_name GridManager
extends Node2D

const GRID_WIDTH = 8
const GRID_HEIGHT = 8
const TILE_SIZE = 64

var _tile_pool: TilePool
var _grid: Array = []  # 二维数组存储瓦片引用

func _ready():
    _tile_pool = TilePool.new(
        preload("res://scenes/crystal_tile.tscn"),
        self,
        80  # 预分配 80 个（8×8 + 额外用于动画）
    )
    _initialize_grid()

func swap_tiles(pos1: Vector2i, pos2: Vector2i):
    # 交换逻辑 — 不需要创建/销毁节点
    var tile_a = _grid[pos1.x][pos1.y]
    var tile_b = _grid[pos2.x][pos2.y]
    
    # 用 Tween 动画交换，无需创建新节点
    var tween = create_tween().set_parallel()
    tween.tween_property(tile_a, "position", _grid_to_world(pos2), 0.2)
    tween.tween_property(tile_b, "position", _grid_to_world(pos1), 0.2)
    
    _grid[pos1.x][pos1.y] = tile_b
    _grid[pos2.x][pos2.y] = tile_a

func remove_matched(matched_positions: Array):
    for pos in matched_positions:
        var tile = _grid[pos.x][pos.y]
        # 播放消除动画后回收
        var tween = create_tween()
        tween.tween_property(tile, "scale", Vector2.ZERO, 0.15)
        tween.tween_callback(_recycle_tile.bind(tile, pos))
        _grid[pos.x][pos.y] = null

func _recycle_tile(tile: Node2D, pos: Vector2i):
    _tile_pool.release(tile)

func spawn_new_tile(pos: Vector2i, crystal_type: int):
    var tile = _tile_pool.acquire()
    add_child(tile)
    tile.setup(crystal_type)  # 设置晶体类型（修改纹理区域）
    tile.position = _grid_to_world(pos) + Vector2(0, -GRID_HEIGHT * TILE_SIZE)
    tile.scale = Vector2.ONE
    
    var tween = create_tween()
    tween.tween_property(tile, "position", _grid_to_world(pos), 0.3)\
         .set_ease(Tween.EASE_OUT).set_trans(Tween.TRANS_BOUNCE)
    
    _grid[pos.x][pos.y] = tile
```

### 5.4 对象池最佳实践

- [x] **预分配**：在 `_ready()` 中创建所有可能需要的瓦片（64 + 额外缓冲）
- [x] **禁用未使用对象的处理**：`process_mode = PROCESS_MODE_DISABLED`
- [x] **不 free，只 detach**：保持对象存活，只从场景树中移除
- [x] **重置状态**：获取时重新初始化所有可变属性
- [x] **使用 Tween 而非 `_process` 做动画**：Tween 自动管理生命周期

---

## 6. Texture Atlas 纹理图集

### 6.1 为什么需要纹理图集

在 WebGL 中，每次切换纹理都是一次 draw call 分割点。将所有晶体类型放入一个图集中意味着：
- 所有晶体可以用**一次 draw call** 绘制（如果使用相同的材质/shader）
- 显著减少 GPU 状态切换

### 6.2 图集布局设计

```
+-----------------------------+
| 红  | 蓝  | 绿  | 黄  | 紫  |  ← 64×64 每种
|-----+-----+-----+-----+-----|
| 橙  | 白  | 特效1|特效2|特效3|
|-----+-----+-----+-----+-----|
| UI按钮|UI框|FX帧1 |FX帧2 |FX帧3|
|-----+-----+-----+-----+-----|
| FX帧4 |FX帧5|FX帧6 |FX帧7 |FX帧8|
+-----------------------------+
  1024 × 1024 像素
```

### 6.3 Godot 中使用 AtlasTexture

```gdscript
# 从图集中创建各个晶体纹理（编辑器中操作更方便）
# 在编辑器中：
# 1. 导入完整图集图片
# 2. 右键图集 → 新建 AtlasTexture
# 3. 设置 Region 为各晶体的矩形区域
# 4. 将 AtlasTexture 分配给各晶体类型
```

**AtlasTexture 的 Draw Call 优势**：
使用同一源纹理的所有 `AtlasTexture` 共享同一个材质实例，Godot 的 2D 批处理系统会自动将它们合并到同一批次中。

### 6.4 纹理图集优化清单

- [ ] 所有晶体类型放入单个 1024×1024 图集
- [ ] UI 元素放入单独的 UI 图集
- [ ] 粒子动画帧放入图集（减少粒子系统纹理切换）
- [ ] 图集中各元素间距 2-4px（防止纹理采样溢出）
- [ ] 使用 2 的幂次方尺寸（2048×2048 等）
- [ ] 移动端确保图集尺寸 ≤ 4096×4096（WebGL 最低保证）
- [ ] 移动端确保任意方向 ≤ 4096（某些旧设备限制 2048）

### 6.5 Godot 2D 批处理条件

Godot 的 Compatibility 渲染器在以下条件下对 2D 精灵进行批处理：
1. 使用相同的纹理（或同一图集的 AtlasTexture）
2. 使用相同的材质
3. 相同的混合模式
4. 位于连续的渲染顺序中（没有其他材质插入）

**确保批处理最大化**：
- 将晶体渲染放在同一 CanvasItem 层级
- 避免在晶体之间插入不同纹理的节点
- UI 元素分层放置，不与游戏元素交错

---

## 7. 内存管理

### 7.1 纹理内存计算

| 纹理尺寸 | RGBA8 内存 | RGB8 内存 | RGBA4 内存 |
|----------|-----------|----------|-----------|
| 256×256 | 256 KB | 192 KB | 128 KB |
| 512×512 | 1 MB | 768 KB | 512 KB |
| 1024×1024 | 4 MB | 3 MB | 2 MB |
| 2048×2048 | 16 MB | 12 MB | 8 MB |
| 4096×4096 | 64 MB | 48 MB | 32 MB |

### 7.2 Match-3 内存预算

```
总 VRAM 预算（移动端 WebGL 安全值）：50 MB

预算分配：
├── 晶体图集 (1024×1024, RGBA8)      4 MB
├── UI 图集 (512×512, RGBA8)          1 MB
├── 粒子精灵表 (512×512, RGBA8)       1 MB
├── 背景 TileMap 纹理 (各 512×512)     4 MB (4 种背景)
├── Godot 引擎内部纹理                 10 MB
├── WASM 内存（运行时）                15 MB
├── 音频缓冲区                         5 MB
└── 安全余量                           10 MB
─────────────────────────────────────────
总计                                   50 MB
```

### 7.3 防止内存泄漏

```gdscript
# Godot 资源管理最佳实践

# 1. 使用 ResourceLoader 缓存而非重复加载
var crystal_texture = load("res://textures/crystals.png")  # Godot 自动缓存

# 2. 及时释放不再使用的场景引用
func _exit_tree():
    _tile_pool.clear()  # 释放池中对象

# 3. 避免循环引用（GDScript 使用引用计数）
# 在节点移除时断开信号连接
func _on_tile_removed(tile):
    tile.match_completed.disconnect(_on_match_completed)

# 4. 大型纹理使用 ResourceSaver 手动管理
# 或使用 weak_ref() 获取弱引用
var weak_tile = weakref(some_tile)
```

### 7.4 纹理尺寸限制（移动端）

- **WebGL 最低保证**：4096×4096
- **现实安全值**：2048×2048（覆盖 >99% 设备）
- **WebP 格式上限**：16,383×16,383（Lossy 模式内部格式）
- **动画精灵表**：确保任意方向 ≤ 4096，否则移动端显示为黑块

---

## 8. 帧率目标与渲染策略

### 8.1 帧率目标

| 平台 | 目标帧率 | 最低帧率 | 说明 |
|------|---------|---------|------|
| 桌面 WebGL | 60 FPS | 30 FPS | 16.67ms 帧预算 |
| 移动端 WebGL (高性能) | 60 FPS | 30 FPS | 部分高端设备可达 |
| 移动端 WebGL (中端) | 30 FPS | 24 FPS | 33.33ms 帧预算 |
| 移动端 WebGL (低端) | 30 FPS | 20 FPS | 50ms 帧预算 |

### 8.2 帧率控制实现

```gdscript
# 自适应帧率管理器
class_name FrameRateManager
extends Node

enum QualityLevel { LOW, MEDIUM, HIGH }

var _quality: QualityLevel = QualityLevel.HIGH
var _fps_samples: Array = []
var _sample_window: int = 30

func _ready():
    _detect_quality()

func _detect_quality():
    if OS.get_name() == "Web":
        var is_mobile = JavaScriptBridge.eval("""
            /Mobi|Android|iPhone|iPad/i.test(navigator.userAgent)
        """)
        if is_mobile:
            _quality = QualityLevel.LOW
            Engine.max_fps = 30
            _apply_low_settings()
        else:
            _quality = QualityLevel.HIGH
            Engine.max_fps = 60
    else:
        _quality = QualityLevel.HIGH
        Engine.max_fps = 60

func _apply_low_settings():
    # 降低渲染分辨率
    get_viewport().scaling_3d_scale = 0.7
    
    # 禁用 VSync（减少输入延迟）
    DisplayServer.window_set_vsync_mode(DisplayServer.VSYNC_DISABLED)

func _process(_delta):
    # 动态调整
    _fps_samples.append(Engine.get_frames_per_second())
    if _fps_samples.size() > _sample_window:
        _fps_samples.pop_front()
    
    var avg_fps = _fps_samples.reduce(func(a, b): return a + b) / _fps_samples.size()
    
    if avg_fps < 25 and Engine.max_fps > 30:
        Engine.max_fps = 30  # 降级到 30 FPS
    elif avg_fps < 20:
        Engine.max_fps = 20  # 紧急降级
```

### 8.3 30fps vs 60fps 决策

**对于 Match-3 游戏**：
- Match-3 是**回合制/离散操作**游戏，不需要高帧率的流畅动作
- 30 FPS 对玩家体验影响很小（不同于动作/射击游戏）
- **建议**：移动端 WebGL 默认 30 FPS，桌面端 60 FPS

**帧预算对比**：
| 帧率 | 帧预算 | 渲染预算 | 逻辑预算 |
|------|--------|---------|---------|
| 60 FPS | 16.67ms | ~8ms | ~8ms |
| 30 FPS | 33.33ms | ~15ms | ~18ms |

30 FPS 时每帧有双倍时间，可以大幅缓解性能压力。

### 8.4 渲染分辨率缩放

```gdscript
# 降低内部渲染分辨率（保持 CSS/Canvas 显示尺寸不变）
func _ready():
    # 移动端 WebGL：以 70% 分辨率渲染
    if _is_mobile_webgl():
        get_viewport().scaling_3d_scale = 0.7
        # 或者对于 2D：
        # get_window().content_scale_factor = 0.7
```

---

## 9. 移动端专项优化

### 9.1 Touch Latency（触摸延迟）

**问题**：浏览器默认对 touch 事件有 300ms 延迟（等待双击缩放判断）。

**解决方案**：

```html
<!-- 在自定义 HTML Shell 的 <head> 中添加 -->
<meta name="viewport" content="width=device-width, initial-scale=1.0, 
      maximum-scale=1.0, user-scalable=no, viewport-fit=cover">
```

```gdscript
# Godot 中确保使用 _input 处理触摸（不是 _unhandled_input）
func _input(event: InputEvent):
    if event is InputEventScreenTouch:
        if event.pressed:
            _on_touch_down(event.position)
        else:
            _on_touch_up(event.position)
    elif event is InputEventScreenDrag:
        _on_swipe(event.relative)

# 所有 UI 按钮设置 input_ray_pickable = true
# 确保触摸响应路径最短
```

### 9.2 电池消耗优化

| 因素 | 消耗比例 | 优化方法 |
|------|---------|---------|
| CPU 使用率 | 高 | 降低帧率目标，减少 `_process` 计算 |
| GPU 渲染 | 高 | 降低分辨率，减少粒子，简化 shader |
| 网络请求 | 中 | 批量请求，减少轮询 |
| 屏幕亮度 | — | 用户控制（但游戏应提供暗色主题） |
| 振动反馈 | 低-中 | 谨慎使用，提供关闭选项 |

**电池友好设计**：
```gdscript
# 在应用进入后台时暂停大部分处理
func _notification(what):
    if what == NOTIFICATION_WM_WINDOW_FOCUS_OUT:
        # Web 标签页不可见时
        Engine.max_fps = 5  # 极低帧率
        AudioServer.set_bus_mute(0, true)  # 静音
    elif what == NOTIFICATION_WM_WINDOW_FOCUS_IN:
        Engine.max_fps = _target_fps
        AudioServer.set_bus_mute(0, false)
```

### 9.3 热控节流（Thermal Throttling）

移动设备过热时，SoC 会降低 CPU/GPU 频率，导致帧率突然下降。

**缓解策略**：
- 保持 CPU/GPU 负载稳定（避免突发高负载）
- 使用固定帧率（不要无上限运行）
- 降低渲染分辨率 = 减少 GPU 发热
- 移动端的 30 FPS 目标显著降低持续发热

### 9.4 Safari 特别注意事项

- Safari (iOS/macOS) WebGL 存在额外的已知问题
- iOS Safari 有严格的内存限制，可能因 "using significant memory" 重新加载页面
- Safari 中 `bufferSubData()` 调用有额外开销（Uniform Buffer 优化需特别注意）
- 推荐在 iOS 上引导用户使用 **添加到主屏幕** (PWA 模式)

### 9.5 PWA 导出配置

```gdscript
# 导出设置 → Web → Progressive Web App
# Enable = true
# 这允许：
# 1. 离线缓存（Service Worker）
# 2. 添加到主屏幕
# 3. 全屏体验（无浏览器地址栏）
# 4. 自动处理 COOP/COEP 头（多线程导出时）
```

---

## 10. Godot 4.x Compatibility 渲染器

### 10.1 Compatibility 渲染器特性总览

| 特性 | 支持情况 |
|------|---------|
| **2D 渲染** | 完整支持 |
| **核心 3D 渲染** | 支持 |
| **阴影** | 最多 8 个方向光 / 8 个点光源 per mesh |
| **全局光照** | 仅 LightmapGI 渲染（烘焙需要 RenderingDevice） |
| **SSAO** | 支持 |
| **Glow / 调整** | 支持 |
| **自定义全屏 Quad 后处理** | 支持 |
| **MSAA 3D** | 支持 |
| **MSAA 2D** | **不支持** |
| **SSAA** | 支持 |
| **Compute Shaders** | **不支持** |
| **粒子轨迹 (Trails)** | **不支持** |
| **粒子 SDF 碰撞** | **不支持** |
| **Decals** | **不支持** |
| **VRS** | **不支持** |
| **HDR 2D Viewport** | **不支持** |

### 10.2 Compatibility 渲染器性能优势

来自 Godot 4.1 渲染优先级文章：
- Compatibility 渲染器专为低端/移动设备设计
- 在低端设备上运行比 Godot 3.x GLES3 渲染器更高效
- 基础渲染开销低（但多物体缩放成本高）

### 10.3 避免的特性（WebGL 不支持）

- [ ] Compute Shaders
- [ ] Vulkan/Metal/D3D12 特定功能
- [ ] Forward+ 特有的后处理链
- [ ] VoxelGI / SDFGI / SSIL
- [ ] 体积雾
- [ ] 次表面散射

### 10.4 项目设置优化

```ini
# project.godot 中推荐的 Web/Mobile 设置

[rendering]
# 渲染器
renderer/rendering_method = "gl_compatibility"

# 纹理
textures/canvas_textures/default_texture_filter = 0  # Nearest (像素风格)
# textures/canvas_textures/default_texture_filter = 3  # Linear (平滑风格)

# 2D 批处理
rendering/2d/batching = true
rendering/2d/batching_synchronous = false  # 异步批处理

# 阴影（2D Match-3 不需要）
rendering/shadows/enabled = false

# 反射
rendering/reflections/screen_space_reflections/enabled = false

# SSAO（如果不需要环境光遮蔽）
rendering/environment/ssao/enabled = false

# Glow（仅在需要时启用）
rendering/environment/glow/enabled = true
rendering/environment/glow/upscale_mode = 0  # 低质量（更快）

[display]
window/stretch/mode = "canvas_items"  # 2D 推荐
window/stretch/aspect = "expand"

[audio]
general/default_playback_type.web = 0  # Sample 模式
# general/output_latency.web = 80       # 如果使用 Stream 模式

[application]
# 禁用不需要的功能以减少构建大小
config/use_hidden_directory = false
```

---

## 11. 资源压缩策略

### 11.1 纹理压缩格式

| 格式 | 桌面 | Android | iOS | WebGL 支持 |
|------|------|---------|-----|-----------|
| **S3TC (DXT1/5)** | ✅ | ❌ | ❌ | WEBGL_compressed_texture_s3tc |
| **ETC2** | ❌ | ✅ | ❌ | WEBGL_compressed_texture_etc |
| **PVRTC** | ❌ | ❌ | ✅ | WEBGL_compressed_texture_pvrtc |
| **ASTC** | ❌ | ✅(新) | ✅(新) | WEBGL_compressed_texture_astc |
| **Basis Universal** | ✅ | ✅ | ✅ | 跨平台通用方案 |

**推荐方案**：

对于 WebGL 导出——使用 **Basis Universal** 压缩：
- 单一文件支持所有 GPU 压缩格式
- 文件大小接近 JPEG
- 在加载时高效转码为目标格式

```gdscript
# Godot 中的 VRAM 压缩设置
# 导入图像 → Import 选项卡
# Compress → VRAM Compressed
# 勾选 "For Desktop" 和 "For Mobile"（WebGL 会选择合适的格式）
```

或使用 Lossy (WebP) 作为通用方案：
- 质量设为 75-85%
- 兼容性最好
- 文件大小合理

### 11.2 音频压缩

| 格式 | 推荐用途 | 比特率建议 | Web 支持 |
|------|---------|-----------|---------|
| **OGG Vorbis** | BGM / SFX | 64-128kbps (BGM), 32-64kbps (SFX) | ✅ |
| **MP3** | BGM | 96-128kbps | ✅ |
| **WAV** | 短 SFX | — | ✅ (但体积大) |

**Match-3 音频策略**：

```gdscript
# BGM: OGG Vorbis, 80kbps, Mono, 44100Hz
# SFX: OGG Vorbis, 48kbps, Mono, 22050Hz
# 短 SFX (<1s): WAV (便于循环和无缝播放)

# 在 Godot 导入设置中：
# Import → Audio → 
#   Mode: Vorbis
#   Quality: 0.5 (SFX) / 0.7 (BGM)
```

**音频优化清单**：
- [ ] BGM 使用 Streaming 模式（不全部加载到内存）
- [ ] SFX 使用 Sample 模式（Web 平台低延迟）
- [ ] 所有音频使用 Mono（Match-3 不需要立体声）
- [ ] 降低采样率（22050Hz 对 SFX 足够）
- [ ] 使用全局 AudioManager 限制同时播放数（最多 8 个）
- [ ] 不使用时释放音频资源

### 11.3 图片导入优化

```gdscript
# .import 文件示例（手动配置或通过编辑器 Import 选项卡）
# crystals.png.import

[remap]
importer="texture"
type="CompressedTexture2D"

[params]
compress/mode=3           # 3 = Lossy (WebP)
compress/lossy_quality=0.8  # 80% 质量
compress/hdr_compression=1
compress/normal_map=0
compress/channel_pack=0
mipmaps/generate=false     # 2D Match-3 不需要 mipmap
detect_3d/compress_to=0
```

---

## 12. 渐进式加载策略

### 12.1 启动加载流程

```
时间线:
0s    → 浏览器加载 HTML + JS (378KB)
0.5s  → 开始下载 WASM (5MB Brotli / 8MB Gzip)
1s    → WASM 编译 (浏览器后台线程)
1.5s  → 显示启动画面 (boot splash)
2s    → 开始下载 .pck 资源包
3s    → 加载核心资源 (图集 + 首屏 UI + 音频注册)
3.5s  → 进入主菜单
─────────────────────────────
目标总启动时间: < 4s (4G 网络)
```

### 12.2 资源加载优先级

```gdscript
# LoadingManager.gd — 渐进式加载管理器
class_name LoadingManager
extends Node

signal loading_progress(percent: float, stage: String)
signal loading_complete

var _stages = [
    {"name": "引擎初始化", "weight": 0.05},
    {"name": "核心纹理图集", "weight": 0.30},
    {"name": "UI 资源", "weight": 0.15},
    {"name": "音频资源", "weight": 0.20},
    {"name": "特效资源", "weight": 0.15},
    {"name": "关卡数据", "weight": 0.10},
    {"name": "完成", "weight": 0.05},
]

func start_loading():
    var progress = 0.0
    for stage in _stages:
        loading_progress.emit(progress, stage.name)
        await _load_stage(stage.name)
        progress += stage.weight

func _load_stage(stage_name: String):
    match stage_name:
        "核心纹理图集":
            # 批量预加载（Godot 自动缓存）
            ResourceLoader.load_threaded_request("res://textures/crystal_atlas.png")
            ResourceLoader.load_threaded_request("res://textures/ui_atlas.png")
            # 等待加载完成
            while ResourceLoader.load_threaded_get_status(
                "res://textures/crystal_atlas.png"
            ) != ResourceLoader.THREAD_LOAD_LOADED:
                await get_tree().process_frame
        
        "音频资源":
            # 预注册音频样本
            AudioServer.register_stream_as_sample(
                preload("res://audio/sfx_match.ogg")
            )
            AudioServer.register_stream_as_sample(
                preload("res://audio/sfx_swap.ogg")
            )
            # BGM 使用流式加载
            ResourceLoader.load_threaded_request("res://audio/bgm.ogg")
```

### 12.3 启动画面自定义

```html
<!-- 自定义 HTML Shell 中的启动画面 -->
<div id="godot-loading">
    <div class="splash">
        <img src="splash.png" alt="Loading..." />
        <progress id="progress-bar" value="0" max="100"></progress>
        <p id="progress-text">Loading...</p>
    </div>
</div>

<style>
#godot-loading {
    position: fixed;
    top: 0; left: 0;
    width: 100%; height: 100%;
    display: flex;
    align-items: center;
    justify-content: center;
    background: #1a1a2e;
    z-index: 9999;
}
#progress-bar {
    width: 80%;
    max-width: 300px;
    height: 8px;
    border-radius: 4px;
}
</style>
```

### 12.4 关卡按需加载

```gdscript
# 当玩家选择关卡时才加载对应背景和关卡数据
func load_level(level_id: int):
    # 释放上一关卡的资源
    if _current_level_data:
        _current_level_data.unreference()  # 减少引用计数
    
    # 按需加载
    var level_path = "res://levels/level_%d.tres" % level_id
    _current_level_data = ResourceLoader.load(level_path)
    
    # 加载关卡背景纹理
    var bg_path = "res://backgrounds/bg_level_%d.png" % level_id
    ResourceLoader.load_threaded_request(bg_path)
```

### 12.5 加载优化清单

- [ ] 使用 Brotli 服务端预压缩 (.wasm + .pck)
- [ ] 配置 `application/wasm` MIME 类型
- [ ] 启动画面显示真实加载进度
- [ ] 核心资源优先加载（图集 > UI > 音频 > 特效 > 关卡数据）
- [ ] 使用 `ResourceLoader.load_threaded_*` 异步加载
- [ ] 注册 Service Worker 实现二次加载即开（PWA）
- [ ] 编译去功能的自定义导出模板（缩小 WASM）
- [ ] 音频使用 Sample 模式避免初始化的 AudioContext 问题
- [ ] 在首帧用户交互后启动音频（浏览器自动播放策略）

---

## 附录 A：性能优化总清单

### 开发阶段

- [ ] 在编辑器中切换为 Compatibility 渲染器进行开发
- [ ] 使用 Web 一键部署进行定期测试（而非仅桌面测试）
- [ ] 在 Chrome DevTools Performance 面板中分析帧时间
- [ ] 使用 `--disable-gpu-vsync --disable-frame-rate-limit` 启动 Chrome 进行无上限测试
- [ ] 在真实移动设备上测试（非模拟器）
- [ ] 监控 WebGL 错误（F12 开发者控制台）

### 资源优化

- [ ] 所有晶体纹理合并为单一图集
- [ ] 图片使用 Lossy (WebP) 压缩，质量 75-85%
- [ ] 纹理尺寸使用 2 的幂次方
- [ ] 移动端纹理 ≤ 2048×2048
- [ ] BGM 使用 OGG Vorbis 80kbps Mono
- [ ] SFX 使用 OGG Vorbis 48kbps Mono
- [ ] 短 SFX 预注册为 Sample
- [ ] 使用 ResourceLoader 缓存而非重复加载

### 代码优化

- [ ] 实现瓦片对象池（预分配 80 个）
- [ ] 使用 Tween 做动画而非 `_process` 中的每帧计算
- [ ] 避免在 `_process` 中 `add_child()` / `queue_free()`
- [ ] 禁用不可见对象的 `_process` 回调
- [ ] 使用信号而非轮询检测状态变化
- [ ] 将大型数组操作移出 `_process`

### 渲染优化

- [ ] 目标 Draw Call < 100（移动端 WebGL）
- [ ] 保持相同纹理的精灵连续渲染
- [ ] 粒子系统使用 Unshaded 着色模式
- [ ] 移动端粒子数 ≤ 200
- [ ] 禁用不需要的阴影/SSAO/反射
- [ ] 使用 `Engine.max_fps` 限制帧率

### 导出配置

- [ ] 使用单线程导出（兼容性最好）
- [ ] 启用 PWA（离线缓存 + 添加到主屏幕）
- [ ] 编译自定义导出模板（去功能缩减 WASM）
- [ ] 配置 Brotli 服务端压缩
- [ ] 关闭 "Export with Debug"
- [ ] 设置 `application/wasm` MIME 类型

---

## 附录 B：关键数据参考

| 指标 | 桌面 WebGL 目标 | 移动端 WebGL 目标 |
|------|----------------|-------------------|
| 帧率 | 60 FPS | 30 FPS |
| Draw Call | < 500 | < 100 |
| 纹理内存 | < 100 MB | < 50 MB |
| 粒子总数 | < 500 | < 200 |
| WASM 大小 (压缩后) | < 8 MB | < 5 MB |
| .pck 大小 | < 20 MB | < 15 MB |
| 启动时间 (4G) | < 3s | < 5s |
| 内存占用 | < 200 MB | < 100 MB |

---

## 参考来源

1. MDN WebGL Best Practices — https://developer.mozilla.org/en-US/docs/Web/API/WebGL_API/WebGL_best_practices
2. Godot 4.x 导出为 Web — https://docs.godotengine.org/en/latest/tutorials/export/exporting_for_web.html
3. Godot 渲染器对比 — https://docs.godotengine.org/en/stable/tutorials/rendering/renderers.html
4. Godot 4.1 渲染优先级 — https://godotengine.org/article/rendering-priorities-4-1/
5. Godot 4.3 Web 导出进展 — https://godotengine.org/article/progress-report-web-export-in-4-3/
6. WebGL 2.0 跨平台问题 — https://webgl2fundamentals.org/webgl/lessons/webgl-cross-platform-issues.html
7. Wonderland Engine WebGL 性能指南 — https://wonderlandengine.com/about/webgl-performance/
8. Jacob Filipp — Godot .pck 缩小指南 — https://jacobfilipp.com/godot/
9. amann.dev — Godot Web 构建大小优化 — https://amann.dev/blog/2025/godot_web_size/
10. Mobile Game Optimization Guide — Red Apple Technologies (Medium, 2026)
11. ARM — Godot 在 ARM GPU 上的 3D 场景优化 — https://developer.arm.com/community/arm-community-blogs/b/mobile-graphics-and-gaming-blog/posts/optimizing-3d-scenes-in-godot-on-arm-gpus-part-2
