shader_type canvas_item;

uniform vec4 crystal_color : source_color = vec4(1.0, 1.0, 1.0, 1.0);
uniform float time_sec = 0.0;

void fragment() {
    float shimmer = sin(UV.x * 6.0 + time_sec) * 0.15 + 0.85;
    COLOR = vec4(crystal_color.rgb * shimmer, crystal_color.a);
}
