// Cheap stylised water: two colours, slow animated ripples. No transparency.
// W67 look-dev: shallows + a broken foam line around every island tile (box distance to the tile squares the world
// passes in `tiles`; one plane, no geometry, every tier) and a slow large-scale tint so the open sea is not one flat blue.
shader_type spatial;
render_mode diffuse_lambert, specular_schlick_ggx;

uniform vec4 deep_color : source_color = vec4(0.16, 0.48, 0.70, 1.0);
uniform vec4 light_color : source_color = vec4(0.36, 0.66, 0.84, 1.0);
uniform vec4 shallow_color : source_color = vec4(0.30, 0.76, 0.80, 1.0);
uniform vec4 foam_color : source_color = vec4(0.93, 0.97, 1.0, 1.0);
uniform vec2 tiles[8];
uniform int tile_count = 0;
uniform float tile_half = 20.0;

varying vec3 wpos;

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

float vnoise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}

void vertex() {
	wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	vec2 p = wpos.xz;
	float w = sin(p.x * 0.35 + TIME * 0.8) * sin(p.y * 0.3 - TIME * 0.6);
	float band = smoothstep(0.6, 0.95, w);
	float swell = vnoise(p * 0.012 + TIME * 0.01);
	vec3 col = mix(deep_color.rgb * (0.88 + 0.18 * swell), light_color.rgb, band * 0.35);
	float d = 1e5;
	for (int i = 0; i < 8; i++) {
		if (i >= tile_count) {
			break;
		}
		vec2 q = abs(p - tiles[i]) - vec2(tile_half);
		d = min(d, length(max(q, vec2(0.0))) + min(max(q.x, q.y), 0.0));
	}
	float shallow = 1.0 - smoothstep(0.0, 6.0, d);
	col = mix(col, shallow_color.rgb, shallow * 0.75);
	float breakup = vnoise(p * 1.3 + vec2(TIME * 0.35, -TIME * 0.22));
	float foam_w = 0.55 + 0.9 * breakup + 0.25 * sin(TIME * 1.4 + p.x * 0.4 + p.y * 0.3);
	float foam = (1.0 - smoothstep(foam_w * 0.6, foam_w, d)) * step(0.0, d);
	float ring = (1.0 - smoothstep(0.0, 0.5, abs(d - (1.5 + 0.35 * sin(TIME * 0.9))))) * step(0.55, breakup);
	col = mix(col, foam_color.rgb, clamp(foam * 0.9 + ring * 0.45, 0.0, 1.0));
	ALBEDO = col;
	ROUGHNESS = 0.35;
	SPECULAR = 0.4;
}
