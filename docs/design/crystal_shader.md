# 水晶着色器设计

> 定义几何水晶视觉效果, 使用程序化 shader 实现折射、菲涅尔、高光和色散效果。

## 关联文档

| 方向 | 文档 |
|------|------|
| ← 研究 | [research/performance.md](../research/performance.md) — WebGL Shader 优化、精度、移动端降级 |
| ↔ 同级 | [board_system.md](board_system.md) — Tile 节点的 ShaderMaterial 配置 |
| → 任务 | [Task 05](../task/05_crystal_shaders.md) — 3 个 .gdshader 文件实现 |
| → 任务 | [Task 06](../task/06_tile_and_pool.md) — Tile 集成 shader |

---

---

## 目录

1. [视觉效果目标](#1-视觉效果目标)
2. [Shader 架构](#2-shader-架构)
3. [crystal.gdshader 实现](#3-crystalgdshader-实现)
4. [水晶类型参数化](#4-水晶类型参数化)
5. [性能考量](#5-性能考量)

---

## 1. 视觉效果目标

### 1.1 几何水晶风格 (Bejeweled-like)

```
视觉效果清单:
✅ 多切面几何形状 (程序化法线模拟)
✅ 菲涅尔边缘光 (边缘比中心更亮)
✅ 内部折射/色散 (模拟光线在水晶内部偏折)
✅ 流动高光 (随时间移动的 specular highlight)
✅ 柔和辉光 (水晶自身发光)
✅ 选中时增强发光
✅ 特殊水晶有不同的视觉特征 (炸弹=脉冲, 彩虹=渐变)
```

### 1.2 视觉参考

- 多边形几何切面 → 使用程序化法线扰动
- 宝石边缘的亮线 → 菲涅尔效应 (1 - abs(dot(N, V)))
- 内部彩虹色散 → 根据法线偏移采样不同颜色
- 高光随水晶旋转 → 时间驱动的 specular 位置

---

## 2. Shader 架构

### 2.1 渲染管线

```
Compatibility Renderer (WebGL 2.0)
    │
    ▼
CanvasItem Shader (crystal.gdshader)
    │
    ├── Vertex: 传递 UV + 时间 uniform
    └── Fragment:
         ├── 程序化法线 (基于 UV 的几何切面模拟)
         ├── 菲涅尔计算
         ├── 漫反射 + 高光
         ├── 色散效果
         ├── 辉光
         └── 调制颜色 (每种水晶颜色)
```

### 2.2 关键限制 (Compatibility 渲染器)

| 限制 | 影响 |
|------|------|
| 不支持 Compute Shader | 无法预处理 |
| 不支持 `hint_depth_texture` | 无深度信息 |
| 不支持 `discard` 高效实现 | 使用 alpha blending |
| WebGL 2.0 → ESSL 3.00 | 需要声明精度 |

---

## 3. crystal.gdshader 实现

```glsl
// crystal.gdshader
// Godot 4 Compatibility Renderer
// 几何水晶效果 — 程序化切面 + 菲涅尔 + 色散

shader_type canvas_item;

// ---- 从精灵纹理采样 ----
uniform sampler2D crystal_texture : source_color;

// ---- 水晶颜色 (由脚本设置) ----
uniform vec4 crystal_color : source_color = vec4(1.0, 1.0, 1.0, 1.0);

// ---- 效果参数 ----
uniform float time_sec = 0.0;
uniform float glow_amount : hint_range(0.0, 1.0) = 0.3;
uniform float shimmer_speed : hint_range(0.0, 2.0) = 0.5;
uniform float facet_intensity : hint_range(0.0, 1.0) = 0.4;
uniform float fresnel_power : hint_range(1.0, 5.0) = 3.0;
uniform float dispersion_amount : hint_range(0.0, 0.5) = 0.15;
uniform vec2 light_dir = vec2(0.5, 0.5);

// ---- 选中状态 (由脚本设置) ----
uniform bool is_selected = false;

// ---- 特殊水晶: 脉冲 (炸弹) / 渐变 (彩虹) ----
uniform bool is_special = false;
uniform int special_type = -1; // 0=BOMB, 1=RAINBOW, 2=CROSS


// 程序化法线 — 模拟多边形切面
vec3 get_faceted_normal(vec2 uv) {
    // 将 UV 空间划分为菱形切面
    float scale = 6.0;
    vec2 grid_uv = uv * scale;
    vec2 grid_id = floor(grid_uv);
    vec2 grid_fract = fract(grid_uv);
    
    // 每个切面有随机法线偏移
    float hash = fract(sin(dot(grid_id, vec2(12.9898, 78.233))) * 43758.5453);
    
    // 切面法线 (在 (0,0,1) 周围波动)
    float nx = (hash - 0.5) * facet_intensity * 0.6;
    float ny = (fract(sin(hash * 6.28)) - 0.5) * facet_intensity * 0.6;
    float nz = sqrt(max(0.0, 1.0 - nx * nx - ny * ny));
    
    return normalize(vec3(nx, ny, nz));
}


// 菲涅尔效果
float fresnel(vec3 N, vec3 V, float power) {
    return pow(1.0 - abs(dot(N, V)), power);
}


void fragment() {
    // 采样纹理
    vec4 tex_color = texture(crystal_texture, UV);
    
    // 程序化法线
    vec3 N = get_faceted_normal(UV);
    
    // 假设视线方向 (2D 中固定)
    vec3 V = vec3(0.0, 0.0, 1.0);
    
    // ---- 菲涅尔边缘光 ----
    float fresnel_val = fresnel(N, V, fresnel_power);
    
    // ---- 漫反射光照 ----
    vec3 L = normalize(vec3(light_dir, 0.8));
    float diffuse = max(dot(N, L), 0.0) * 0.6 + 0.4; // 环境光保底
    
    // ---- 高光 ----
    float shimmer = sin(UV.x * 10.0 + UV.y * 8.0 + time_sec * shimmer_speed) * 0.5 + 0.5;
    float specular = pow(max(dot(reflect(-L, N), V), 0.0), 32.0);
    specular += shimmer * 0.15;
    
    // ---- 色散 (内部折射模拟) ----
    float dispersion = (fresnel_val - 0.5) * dispersion_amount;
    float r_disp = dispersion * 1.0;
    float g_disp = dispersion * 0.5;
    float b_disp = dispersion * -0.5;
    
    // ---- 组合颜色 ----
    vec3 base_color;
    if (special_type == 1) {
        // 彩虹水晶: 旋转 HSV
        float hue = fract(UV.x + UV.y + time_sec * 0.3);
        base_color = vec3(
            0.5 + 0.5 * cos(6.28318 * (hue + vec3(0.0, 0.333, 0.667)))
        );
    } else {
        base_color = crystal_color.rgb;
    }
    
    // 应用光照
    vec3 lit_color = base_color * diffuse;
    lit_color += specular * vec3(0.8, 0.9, 1.0) * 0.5;  // 冷色高光
    
    // 边缘光
    vec3 edge_glow = base_color * fresnel_val * 0.4;
    lit_color += edge_glow;
    
    // 色散 (边缘分离 RGB)
    lit_color.r += r_disp;
    lit_color.g += g_disp;
    lit_color.b += b_disp;
    
    // 辉光
    float glow = glow_amount;
    if (is_selected) {
        glow = 0.7;  // 选中时增强辉光
    }
    if (special_type == 0) {
        // 炸弹水晶: 脉冲发光
        float pulse = sin(time_sec * 3.0) * 0.3 + 0.7;
        glow *= pulse;
    }
    vec3 ambient_glow = base_color * glow * 0.25;
    lit_color += ambient_glow;
    
    // 纹理细节
    lit_color *= tex_color.rgb;
    
    // 最终颜色
    COLOR = vec4(lit_color, tex_color.a * crystal_color.a);
}
```

---

## 4. 水晶类型参数化

### 4.1 ShaderMaterial 配置表

在 GDScript 中为每种水晶类型配置不同的 ShaderMaterial:

```gdscript
# scripts/game/tile.gd

const CRYSTAL_COLORS: Dictionary = {
    CrystalType.RED:    Color("#ff4466"),  # 红宝石
    CrystalType.BLUE:   Color("#4488ff"),  # 蓝宝石
    CrystalType.GREEN:  Color("#44dd66"),  # 翡翠
    CrystalType.YELLOW: Color("#ffcc33"),  # 黄玉
    CrystalType.PURPLE: Color("#cc44ff"),  # 紫水晶
}

func _set_crystal_material():
    var material := crystal_sprite.material as ShaderMaterial
    if material == null:
        material = ShaderMaterial.new()
        material.shader = preload("res://assets/shaders/crystal.gdshader")
        crystal_sprite.material = material
    
    var color := CRYSTAL_COLORS.get(tile_data.crystal_type, Color.WHITE)
    material.set_shader_parameter("crystal_color", color)
    material.set_shader_parameter("is_special", tile_data.is_special())
    material.set_shader_parameter("special_type", tile_data.special_type)
    material.set_shader_parameter("is_selected", visual_state == TileVisualState.SELECTED)
```

### 4.2 Shader 参数默认值

| 参数 | 默认值 | 说明 |
|------|--------|------|
| `crystal_color` | Color(1,1,1) | 每种水晶的主色调 |
| `glow_amount` | 0.3 | 基础辉光强度 |
| `shimmer_speed` | 0.5 | 高光闪烁速度 |
| `facet_intensity` | 0.4 | 切面强度 (0=平滑, 1=强烈切面) |
| `fresnel_power` | 3.0 | 菲涅尔指数 (边缘光扩散程度) |
| `dispersion_amount` | 0.15 | 色散强度 |
| `light_dir` | (0.5, 0.5) | 光源方向 (从左上角) |

---

## 5. 性能考量

### 5.1 优化点

1. **提前返回**: 对于完全透明的像素, 不进行光照计算 (纹理 alpha = 0 → 跳过)
2. **避免 `discard`**: Compatibility 渲染器对 discard 不友好, 使用 alpha blending
3. **减少纹理采样**: 只采样一次 `crystal_texture`, 其余计算为程序化
4. **精度声明**: 使用 `mediump` 作为默认片段着色器精度 (移动端友好)
5. **简化特殊类型判定**: 将 `special_type` 放在 uniform, 避免分支

### 5.2 移动端降级

对于低端设备, 可提供一个简化版 shader:

```glsl
// crystal_simple.gdshader — 低端设备降级版
shader_type canvas_item;

uniform sampler2D crystal_texture : source_color;
uniform vec4 crystal_color : source_color = vec4(1.0);
uniform float time_sec = 0.0;

void fragment() {
    vec4 tex = texture(crystal_texture, UV);
    // 仅做简单的高光闪烁 + 颜色调制
    float shimmer = sin(UV.x * 6.0 + time_sec) * 0.15 + 0.85;
    COLOR = vec4(tex.rgb * crystal_color.rgb * shimmer, tex.a);
}
```

### 5.3 Shader 切换

```gdscript
# 在 TileManager 中根据设备性能选择 shader
func _get_shader() -> Shader:
    if GameData.particle_quality >= 1:
        return preload("res://assets/shaders/crystal.gdshader")
    else:
        return preload("res://assets/shaders/crystal_simple.gdshader")
```

---

## 附录: 背景 Shader (可选)

```glsl
// background.gdshader — 流动暗色背景
shader_type canvas_item;

uniform float time_sec = 0.0;
uniform vec4 color_top : source_color = vec4(0.1, 0.08, 0.2, 1.0);
uniform vec4 color_bottom : source_color = vec4(0.05, 0.04, 0.1, 1.0);

void fragment() {
    // 垂直渐变
    vec3 bg = mix(color_bottom.rgb, color_top.rgb, UV.y);
    
    // 细微的流动波纹
    float wave = sin(UV.x * 3.0 + time_sec * 0.3) * sin(UV.y * 4.0 + time_sec * 0.2) * 0.02;
    
    COLOR = vec4(bg + wave, 1.0);
}
```
