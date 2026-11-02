@tool
extends EditorPlugin
## The Godot 4.7 Android template (res://android/build, installed per build, not in git) has no google-services
## Gradle plugin, so Firebase cannot read google-services.json → FirebaseApp init fails. At every Android export
## this adds `id 'com.google.gms.google-services'` to the template's plugins {} block (idempotent).
## godotx_firebase copies google-services.json into res://android/build at the same moment.

var _export: EditorExportPlugin


func _enter_tree() -> void:
	_export = GradlePatch.new()
	add_export_plugin(_export)


func _exit_tree() -> void:
	remove_export_plugin(_export)


class GradlePatch extends EditorExportPlugin:
	const GRADLE := "res://android/build/build.gradle"
	const CONFIG := "res://google-services.json"
	const ANCHOR := "    id 'org.jetbrains.kotlin.android'\n"
	const LINE := "    id 'com.google.gms.google-services' version '4.4.4'\n"

	func _get_name() -> String:
		return "CoddyFirebaseGradle"

	func _supports_platform(platform: EditorExportPlatform) -> bool:
		return platform is EditorExportPlatformAndroid

	## Firebase Analytics (and AdMob) merge com.google.android.gms.permission.AD_ID into the manifest; Play refuses a
	## release whose manifest has it while the app's Advertising-ID declaration says "no" (Airport / Train 1.1.0 were
	## rejected for exactly that, 2026-10-19). Ads here are non-personalised, so the id is stripped unless the project
	## opts in with [coddy_ads] keep_ad_id=true — set it only together with "Advertising ID = Yes" in Play Console.
	func _get_android_manifest_element_contents(_platform: EditorExportPlatform, _debug: bool) -> String:
		if bool(ProjectSettings.get_setting("coddy_ads/keep_ad_id", false)):
			return ""
		var out := ""
		for perm in ["com.google.android.gms.permission.AD_ID", "android.permission.AD_ID", "android.permission.ACCESS_ADSERVICES_AD_ID",
				"android.permission.ACCESS_ADSERVICES_ATTRIBUTION"]:
			out += '<uses-permission android:name="%s" tools:node="remove" />\n' % perm
		return out

	## Google Play In-App Review (scripts/monetization/review_native.gd calls it through JavaClassWrapper): the Play review library.
	func _get_android_dependencies(_platform: EditorExportPlatform, _debug: bool) -> PackedStringArray:
		return PackedStringArray(["com.google.android.play:review:2.0.2"])

	func _export_begin(features: PackedStringArray, _is_debug: bool, _path: String, _flags: int) -> void:
		# No config → no plugin (the google-services task fails the build when the json is missing).
		if not features.has("android") or not FileAccess.file_exists(GRADLE) or not FileAccess.file_exists(CONFIG):
			return
		var text := FileAccess.get_file_as_string(GRADLE)
		if text.contains("com.google.gms.google-services"):
			return
		if not text.contains(ANCHOR):
			push_error("[CoddyFirebaseGradle] plugins {} anchor not found in " + GRADLE + " — Firebase will not init")
			return
		var f := FileAccess.open(GRADLE, FileAccess.WRITE)
		f.store_string(text.replace(ANCHOR, ANCHOR + LINE))
		f.close()
		print("[CoddyFirebaseGradle] google-services plugin applied to " + GRADLE)
