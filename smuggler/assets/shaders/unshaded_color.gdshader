// Flat glowing colour for markers, beacons and route lines.
shader_type spatial;
render_mode unshaded, cull_disabled;

uniform vec4 color : source_color = vec4(1.0, 0.8, 0.2, 1.0);
uniform float pulse = 0.0;

void fragment() {
	float p = 1.0 + pulse * 0.25 * sin(TIME * 6.0);
	ALBEDO = color.rgb * p;
}
