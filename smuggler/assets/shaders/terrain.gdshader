// Vertex-coloured terrain / roads with a soft locked-region desaturation.
// W67 look-dev: surface detail from WORLD position — zero geometry, zero extra draws, every tier. The vertex alpha is a
// material code (TerrainBuilder.CODE_*): tile tops carry their region's biome (index 0..7 → dune ripples, meadow
// tufts, coastal grass, industrial gravel, red-mesa strata, snow sparkle, tropical grass, neon pavement), cliff sides
// get rock strata, roads get asphalt grain + patches + worn tyre lanes, shoulders gravel. Alpha 1 = plain colour (the
// HQ pads, the store icon render).
shader_type spatial;
render_mode diffuse_lambert, specular_disabled;

uniform float desaturate : hint_range(0.0, 1.0) = 0.0;
uniform float brightness = 1.0;

varying vec3 wpos;

vec3 to_linear(vec3 c) {
	return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(0.04045, c));
}

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

float vnoise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	float a = hash(i);
	float b = hash(i + vec2(1.0, 0.0));
	float c = hash(i + vec2(0.0, 1.0));
	float d = hash(i + vec2(1.0, 1.0));
	return mix(mix(a, b, f.x), mix(c, d, f.x), f.y);
}

void vertex() {
	wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

float biome_shade(int b, vec2 p) {
	float large = vnoise(p * 0.08);           // ~12 m patches
	float mid = vnoise(p * 0.45 + 17.0);      // ~2 m tufts
	// 0.25 m speckle, rounded (a soft dot inside each cell, never a hard square)
	vec2 fc = fract(p * 4.0) - 0.5;
	float fine = hash(floor(p * 4.0)) * (1.0 - smoothstep(0.18, 0.42, length(fc)));
	if (b == 0) {           // Dust Valley: wind ripples across dunes + pebbles
		float rip = sin(p.x * 2.1 + p.y * 0.9 + large * 6.0) * 0.5 + 0.5;
		return 0.86 + 0.10 * rip + 0.12 * large - 0.09 * step(0.90, fine);
	} else if (b == 5) {    // Frostland: wind-packed snow + sparkle
		return 0.90 + 0.08 * large + 0.04 * mid + 0.10 * step(0.965, fine);
	} else if (b == 7) {    // Neon State: paving slabs (2 m) with dark joints
		vec2 g = abs(fract(p * 0.5) - 0.5);
		float joint = smoothstep(0.47, 0.49, max(g.x, g.y));
		return 0.92 + 0.12 * hash(floor(p * 0.5)) - 0.30 * joint;
	} else if (b == 3) {    // Iron Federation: gravel + oil stains
		return 0.84 + 0.12 * mid + 0.10 * fine - 0.14 * smoothstep(0.72, 0.9, large);
	} else if (b == 4) {    // Red Frontier: strata bands + dry cracks
		float band = sin(p.x * 0.6 - p.y * 0.35 + large * 3.0) * 0.5 + 0.5;
		return 0.82 + 0.14 * band + 0.10 * mid - 0.08 * step(0.9, fine);
	} else if (b == 1) {    // Green Republic: farm parcels (16 m) with crop rows along the parcel's axis + hedges
		vec2 cell = floor(p / 16.0);
		float parcel = hash(cell);
		vec2 q = parcel > 0.5 ? p : p.yx;
		float rows = abs(fract(q.x * 0.9) - 0.5) * 2.0;
		vec2 e = abs(fract(p / 16.0) - 0.5);
		float hedge = smoothstep(0.455, 0.48, max(e.x, e.y));
		float field = step(0.45, parcel);
		return mix(0.76 + 0.30 * large + 0.12 * mid, 0.74 + 0.20 * smoothstep(0.2, 0.8, rows) + 0.16 * (parcel - 0.5) + 0.10 * large, field) - 0.22 * hedge;
	}
	// Free Coast / Golden Isles: meadow patches, darker tufts, flower specks
	return 0.74 + 0.32 * large + 0.14 * mid - 0.10 * step(0.88, fine) + 0.12 * step(0.975, fine);
}

void fragment() {
	vec3 c = COLOR.rgb;
	float code = COLOR.a;
	vec2 p = wpos.xz;
	float shade = 1.0;
	if (code < 0.5) {                          // tile top: biome index = code * 16
		shade = biome_shade(int(floor(code * 16.0)), p);
	} else if (code < 0.6) {                   // cliff side: horizontal strata + vertical streaks
		float y = wpos.y;
		float strata = sin(y * 7.0 + vnoise(p * 0.6) * 3.0) * 0.5 + 0.5;
		shade = 0.82 + 0.16 * strata + 0.08 * vnoise(vec2(p.x + p.y, y) * vec2(1.5, 0.4));
	} else if (code < 0.7) {                   // shoulder: gravel
		shade = 0.86 + 0.14 * hash(floor(p * 5.0)) + 0.06 * vnoise(p * 0.7);
	} else if (code < 0.8) {                   // asphalt: grain + repair patches + worn lanes (lighter blotches)
		float grain = hash(floor(p * 6.0));
		float patch = step(0.80, vnoise(p * 0.18 + 5.0));
		shade = 0.92 + 0.10 * grain + 0.10 * vnoise(p * 0.9) - 0.12 * patch;
	} else if (code < 0.9) {                   // markings: worn paint
		shade = 0.80 + 0.20 * vnoise(p * 3.0);
	}
	c *= shade;
	float g = dot(c, vec3(0.299, 0.587, 0.114));
	c = mix(c, vec3(g) * vec3(0.78, 0.82, 0.9), desaturate) * brightness;
	ALBEDO = c;
	ROUGHNESS = 1.0;
}
