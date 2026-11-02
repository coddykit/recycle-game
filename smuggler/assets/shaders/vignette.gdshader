shader_type canvas_item;
// W67 look-dev (Kaiten recipe step 4, copied from train-baron): a screen-space vignette under the HUD (CanvasLayer −1, blend_mul). 2D only:
// no internal 3D buffer, so it never trips the Android GLES3 MSAA leak guard (coddy_perf guard_msaa). Every tier.
render_mode blend_mul, unshaded;
uniform float strength = 0.28;
uniform vec3 tint = vec3(1.0, 0.94, 0.86);

void fragment() {
	vec2 uv = SCREEN_UV - vec2(0.5);
	uv.x *= SCREEN_PIXEL_SIZE.y / SCREEN_PIXEL_SIZE.x;   // round on any aspect
	float d = length(uv) / 0.75;
	float v = 1.0 - strength * smoothstep(0.45, 1.05, d);
	COLOR = vec4(mix(vec3(1.0), tint, smoothstep(0.35, 1.0, d)) * v, 1.0);
}
