# Task 05: 水晶着色器

## 关联

| 方向 | 文档 |
|------|------|
| ↖ 设计 | [design/crystal_shader.md](../design/crystal_shader.md) — crystal.gdshader 完整效果规格和 GLSL 代码 |

## 状态
- [x] 已完成

## 依赖
- 无 (完全独立, 可与其他所有 Phase 1 任务并行)

## 产出文件
```
assets/shaders/crystal.gdshader
assets/shaders/crystal_simple.gdshader
assets/shaders/background.gdshader
```

## 实现要求

### crystal.gdshader — 几何水晶效果 (高质量)

参考 `docs/design/crystal_shader.md` §3:

```glsl
shader_type canvas_item;

uniform sampler2D crystal_texture : source_color;
uniform vec4 crystal_color : source_color = vec4(1.0);
uniform float time_sec = 0.0;
uniform float glow_amount : hint_range(0.0, 1.0) = 0.3;
uniform float shimmer_speed : hint_range(0.0, 2.0) = 0.5;
uniform float facet_intensity : hint_range(0.0, 1.0) = 0.4;
uniform float fresnel_power : hint_range(1.0, 5.0) = 3.0;
uniform float dispersion_amount : hint_range(0.0, 0.5) = 0.15;
uniform vec2 light_dir = vec2(0.5, 0.5);
uniform bool is_selected = false;
uniform bool is_special = false;
uniform int special_type = -1;
```

必须包含的效果:
1. **程序化切面法线**: `get_faceted_normal(uv)` — 使用 `floor/fract` 将 UV 切分为菱形网格, 每个切面有随机法线偏移
2. **菲涅尔边缘光**: `fresnel(N, V, power)` — `pow(1.0 - abs(dot(N, V)), power)`
3. **漫反射光照**: `max(dot(N, L), 0.0) * 0.6 + 0.4` (环境光保底)
4. **流动高光**: 使用 `sin(UV*10 + time)` 调制 specular
5. **色散**: 边缘分离 RGB 通道 `(r += dispersion, g += 0.5*dispersion, b -= 0.5*dispersion)`
6. **选中增强**: `is_selected` 时 glow 提升到 0.7
7. **彩虹水晶** (special_type==1): 使用 `cos` 在 RGB 通道制造旋转色相
8. **炸弹脉冲** (special_type==0): `sin(time*3.0)*0.3+0.7` 调制 glow

**重要**: 不要使用 `discard` 语句, 使用 alpha blending。声明片段着色器精度: `precision mediump float;`

### crystal_simple.gdshader — 移动端降级版

```glsl
shader_type canvas_item;

uniform sampler2D crystal_texture : source_color;
uniform vec4 crystal_color : source_color = vec4(1.0);
uniform float time_sec = 0.0;
```

简化效果: 仅高光闪烁 + 颜色调制, 无切面/菲涅尔/色散。`shimmer = sin(UV*6 + time)*0.15 + 0.85`。

### background.gdshader — 流动暗色背景

```glsl
shader_type canvas_item;

uniform float time_sec = 0.0;
uniform vec4 color_top : source_color = vec4(0.1, 0.08, 0.2, 1.0);
uniform vec4 color_bottom : source_color = vec4(0.05, 0.04, 0.1, 1.0);
```

效果: 垂直渐变 + 细微正弦波纹 `sin(UV.x*3 + time*0.3) * sin(UV.y*4 + time*0.2) * 0.02`

## 验收标准
- 三个 .gdshader 文件创建, Godot 4 能正确加载
- crystal.gdshader 包含所有 7 种效果 (切面/菲涅尔/漫反射/高光/色散/特殊水晶/选中)
- 使用 `shader_type canvas_item;` (Compatibility 渲染器)
- 不使用 `discard`
- crystal_simple.gdshader 为简化版, 只含高光+颜色
- background.gdshader 为渐变+波纹

## 注意
- Compatibility 渲染器用 GLSL ES 3.00 语法
- `precision mediump float;` 放在 fragment 函数之前
- uniform 使用正确的 `hint_range`, `source_color` 等 hint
