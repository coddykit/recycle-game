extends "res://addons/coddy_ui/tests/font_audit.gd"

func setup_theme() -> void:
	load("res://scripts/ui/ui_kit.gd").theme()

func locales() -> Array:
	return ["en", "tr", "de", "es", "fr", "pt", "ru", "ja", "ko", "zh"]

func banner_path() -> String:
	return "res://scripts/monetization/ads/CoddyBanner.gd"
