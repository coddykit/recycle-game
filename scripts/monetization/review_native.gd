## The OS's own rating sheet: Google Play In-App Review (Android) / StoreKit requestReview (iOS). Desktop = no-op.
## Android: called through Godot's JavaClassWrapper (no plugin AAR): the Play Core review library is a Gradle dependency
## added at export time by addons/coddy_firebase_gradle (`_get_android_dependencies`); the Task is polled, never listened to.
## iOS: StoreKit needs a native call Godot does not have — the game looks for a native singleton `CoddyReview` with
## `request_review()` (a small iOS plugin, not built yet: see games/smuggler/docs/qa/w5-2026-10-19/NOTES.md); without it the game
## does not ask on iOS. (Same design as corrupt-tycoon's ReviewNative.)
## The result is never known (the OS hides whether the sheet showed or a rating was given), so every call counts as an ask.
extends RefCounted

const IOS_SINGLETON := "CoddyReview"


static func available() -> bool:
	if OS.get_name() == "Android":
		return Engine.has_singleton("AndroidRuntime") and JavaClassWrapper != null
	if OS.get_name() == "iOS":
		return Engine.has_singleton(IOS_SINGLETON)
	return false


## Ask the OS to show its rating sheet. Returns true when the request was handed over (not that a sheet appeared).
static func request(tree: SceneTree) -> bool:
	if OS.get_name() == "iOS" and Engine.has_singleton(IOS_SINGLETON):
		Engine.get_singleton(IOS_SINGLETON).request_review()
		return true
	if OS.get_name() == "Android" and Engine.has_singleton("AndroidRuntime"):
		return await _android(tree)
	return false


static func _android(tree: SceneTree) -> bool:
	var rt = Engine.get_singleton("AndroidRuntime")
	var activity = rt.getActivity()
	var factory = JavaClassWrapper.wrap("com.google.android.play.core.review.ReviewManagerFactory")
	if activity == null or factory == null: # the Play review library is not in this build
		return false
	var manager = factory.create(activity)
	var flow = manager.requestReviewFlow()
	if not await _wait(tree, flow) or not flow.isSuccessful():
		return false
	var launched = manager.launchReviewFlow(activity, flow.getResult())
	await _wait(tree, launched)
	return true


## Poll a Play Core Task until it completes (a few seconds at most).
static func _wait(tree: SceneTree, task) -> bool:
	var t0 := Time.get_ticks_msec()
	while not task.isComplete():
		await tree.process_frame
		if Time.get_ticks_msec() - t0 > 20000:
			return false
	return true
