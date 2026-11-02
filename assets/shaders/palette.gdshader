// Shared material for every Blender asset. All models are UV-mapped onto an
// 8x8 palette texture (tools/palette.py): one material, tiny texture,
// mobile friendly. Row 7 glows; cell (7,4) is replaced by a paint colour.
shader_type spatial;
render_mode diffuse_lambert, specular_disabled;

uniform sampler2D palette_tex : source_color, filter_nearest;
uniform vec4 paint_color : source_color = vec4(0.88, 0.35, 0.28, 1.0);
uniform float desaturate : hint_range(0.0, 1.0) = 0.0;
uniform float brightness = 1.0;
uniform vec4 flash_color : source_color = vec4(1.0);
uniform float flash : hint_range(0.0, 1.0) = 0.0;
uniform float emission_strength = 0.9;

varying vec3 inst_color;

vec3 to_linear(vec3 c) {
	return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(0.04045, c));
}

void vertex() {
	// MultiMesh instance colour tints the paint cell
	inst_color = COLOR.rgb;
}

void fragment() {
	vec2 cell = floor(UV * 8.0);
	vec3 c = texture(palette_tex, UV).rgb;
	if (cell.x > 6.5 && cell.y > 3.5 && cell.y < 4.5) {
		c = paint_color.rgb * inst_color;
	}
	float g = dot(c, vec3(0.299, 0.587, 0.114));
	c = mix(c, vec3(g) * vec3(0.80, 0.84, 0.92), desaturate);
	c *= brightness;
	c = mix(c, flash_color.rgb, flash);
	ALBEDO = c;
	ROUGHNESS = 0.9;
	if (cell.y > 6.5 && desaturate < 0.5) {
		EMISSION = c * emission_strength;
	}
}
