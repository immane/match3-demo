shader_type canvas_item;

uniform float time_sec = 0.0;
uniform vec4 color_top : source_color = vec4(0.1, 0.08, 0.2, 1.0);
uniform vec4 color_bottom : source_color = vec4(0.05, 0.04, 0.1, 1.0);

void fragment() {
    vec3 bg = mix(color_bottom.rgb, color_top.rgb, UV.y);
    float wave = sin(UV.x * 3.0 + time_sec * 0.3) * sin(UV.y * 4.0 + time_sec * 0.2) * 0.02;
    COLOR = vec4(bg + wave, 1.0);
}
