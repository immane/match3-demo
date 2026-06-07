shader_type canvas_item;

uniform vec4 crystal_color : source_color = vec4(1.0, 1.0, 1.0, 1.0);
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

vec3 get_faceted_normal(vec2 uv) {
    float scale = 6.0;
    vec2 grid_uv = uv * scale;
    vec2 grid_id = floor(grid_uv);
    float hash = fract(sin(dot(grid_id, vec2(12.9898, 78.233))) * 43758.5453);
    float nx = (hash - 0.5) * facet_intensity * 0.6;
    float ny = (fract(sin(hash * 6.28)) - 0.5) * facet_intensity * 0.6;
    float nz = sqrt(max(0.0, 1.0 - nx * nx - ny * ny));
    return normalize(vec3(nx, ny, nz));
}

float fresnel(vec3 N, vec3 V, float power) {
    return pow(1.0 - abs(dot(N, V)), power);
}

void fragment() {
    vec3 N = get_faceted_normal(UV);
    vec3 V = vec3(0.0, 0.0, 1.0);
    float fresnel_val = fresnel(N, V, fresnel_power);
    vec3 L = normalize(vec3(light_dir, 0.8));
    float diffuse = max(dot(N, L), 0.0) * 0.6 + 0.4;

    float shimmer = sin(UV.x * 10.0 + UV.y * 8.0 + time_sec * shimmer_speed) * 0.5 + 0.5;
    float specular = pow(max(dot(reflect(-L, N), V), 0.0), 32.0);
    specular += shimmer * 0.15;

    float dispersion = (fresnel_val - 0.5) * dispersion_amount;

    vec3 base_color;
    if (special_type == 1) {
        float hue = fract(UV.x + UV.y + time_sec * 0.3);
        base_color = vec3(
            0.5 + 0.5 * cos(6.28318 * (hue + vec3(0.0, 0.333, 0.667)))
        );
    } else {
        base_color = crystal_color.rgb;
    }

    vec3 lit_color = base_color * diffuse;
    lit_color += specular * vec3(0.8, 0.9, 1.0) * 0.5;
    vec3 edge_glow = base_color * fresnel_val * 0.4;
    lit_color += edge_glow;

    lit_color.r += dispersion * 1.0;
    lit_color.g += dispersion * 0.5;
    lit_color.b += dispersion * -0.5;

    float glow = glow_amount;
    if (is_selected) {
        glow = 0.7;
    }
    if (special_type == 0) {
        float pulse = sin(time_sec * 3.0) * 0.3 + 0.7;
        glow *= pulse;
    }
    vec3 ambient_glow = base_color * glow * 0.25;
    lit_color += ambient_glow;

    COLOR = vec4(lit_color, crystal_color.a);
}
