extends SceneTree
## Game wrapper supplies its real theme initializer. Checks the source kit theme used directly by house ads.
## Run with --audio-driver Dummy, an isolated user dir, and -s tests/test_house_fonts.gd.
const Kit = preload("../ui_kit.gd")
const Probe = preload("font_probe.gd")
var failures := 0
class LocalAds:
	extends Node
	func ui_lang() -> String: return TranslationServer.get_locale().substr(0, 2)
	func _pick_i18n(_m) -> String: return "Unsupported " + String.chr(0x10ffff)

func _initialize() -> void:
	_run.call_deferred()
func setup_theme() -> void:
	pass
func locales() -> Array:
	return ["en", "tr", "de", "es", "fr", "pt", "ru", "ja", "ko", "zh"]
func banner_path() -> String:
	return ""
func _run() -> void:
	var banner = load(banner_path()) if banner_path() != "" else null
	for locale in locales():
		TranslationServer.set_locale(locale)
		setup_theme()
		var t: Theme = Kit.theme(locale)
		var lang: String = str(locale).substr(0, 2)
		var caption: String = banner.install_text(lang) if banner != null else "Install"
		for kind in ["Button", "PrimaryButton", "Label", "Title"]:
			var font: Font = t.get_font("font", kind)
			var missing := Probe.missing(font, caption)
			if not missing.is_empty():
				failures += 1
				print("FAIL HOUSE_FONT ", locale, " ", kind, " ", missing)
		await _card_without_banner()
	print("HOUSE_FONTS: ", locales().size(), " locales, ", failures, " failures")
	Kit.tap_sound = Callable()
	quit(1 if failures else 0)

## Render the real card after removing its optional banner helper. A deliberately unsupported
## remote title must select the drawable English title in every locale, never render tofu.
func _card_without_banner() -> void:
	var path := banner_path().get_base_dir().path_join("CoddyInterstitial.gd") if banner_path() != "" else "res://src/services/CoddyInterstitial.gd"
	if not ResourceLoader.exists(path):
		failures += 1
		print("FAIL HOUSE_FONT missing card ", path)
		return
	var owner := LocalAds.new()
	root.add_child(owner)
	var card = load(path).new(owner, {"titleI18n": {"en": "Font audit"}}, [], null, false)
	root.add_child(card)
	for child in card.get_children(): child.free()
	card._banner_script = null
	card._build()
	if card.title_label.text != "Font audit" or not Probe.missing(card.title_label.get_theme_font("font"), card.title_label.text).is_empty():
		failures += 1
		print("FAIL HOUSE_FONT bannerless remote title ", TranslationServer.get_locale())
	card.queue_free()
	owner.queue_free()
	await process_frame
