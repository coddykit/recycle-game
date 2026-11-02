class_name Fmt
## Number / time formatting helpers used across the UI.


static func money(v: float, short := true) -> String:
	var neg := v < 0
	var a: float = absf(v)
	var s: String
	if short and a >= 1_000_000_000.0:
		s = "%.2fB" % (a / 1_000_000_000.0)
	elif short and a >= 1_000_000.0:
		s = "%.2fM" % (a / 1_000_000.0)
	elif short and a >= 100_000.0:
		s = "%.0fK" % (a / 1000.0)
	elif short and a >= 10_000.0:
		s = "%.1fK" % (a / 1000.0)
	else:
		s = commas(int(round(a)))
	return ("-$" if neg else "$") + s


static func commas(n: int) -> String:
	var s := str(absi(n))
	var out := ""
	var count := 0
	for i in range(s.length() - 1, -1, -1):
		out = s[i] + out
		count += 1
		if count % 3 == 0 and i > 0:
			out = "," + out
	return ("-" if n < 0 else "") + out


static func time(sec: float) -> String:
	var s := int(ceil(maxf(sec, 0.0)))
	if s < 60:
		return "%ds" % s
	if s < 3600:
		return "%dm %02ds" % [s / 60, s % 60]
	return "%dh %02dm" % [s / 3600, (s % 3600) / 60]


static func pct(v: float) -> String:
	return "%d%%" % int(round(v))


static func risk_label(risk: float) -> String:
	if risk < 15:
		return TranslationServer.translate("Low")
	if risk < 35:
		return TranslationServer.translate("Medium")
	if risk < 60:
		return TranslationServer.translate("High")
	return TranslationServer.translate("Extreme")


static func risk_color(risk: float) -> Color:
	if risk < 15:
		return Color("3ddc84")
	if risk < 35:
		return Color("ffc93c")
	if risk < 60:
		return Color("ff8a3c")
	return Color("ff4d5e")


static func heat_label(h: float) -> String:
	if h <= 20:
		return TranslationServer.translate("Cold")
	if h <= 50:
		return TranslationServer.translate("Warm")
	if h <= 75:
		return TranslationServer.translate("Hot")
	return TranslationServer.translate("Critical")


static func heat_color(h: float) -> Color:
	if h <= 20:
		return Color("5fd8e8")
	if h <= 50:
		return Color("ffc93c")
	if h <= 75:
		return Color("ff8a3c")
	return Color("ff3b4e")


## Rounds a reward to a "nice" human number.
static func nice(v: float) -> int:
	if v < 100:
		return int(round(v / 5.0) * 5)
	if v < 1000:
		return int(round(v / 10.0) * 10)
	if v < 10000:
		return int(round(v / 50.0) * 50)
	if v < 100000:
		return int(round(v / 100.0) * 100)
	return int(round(v / 1000.0) * 1000)
