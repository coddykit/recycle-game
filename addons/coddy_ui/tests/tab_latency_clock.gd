extends RefCounted
## #66: host load stretches milliseconds; acceptance is content ready within two frames.
## Headless Godot draws no frames. Its process-frame count is a content/layout proxy, never GPU proof.
static func begin() -> Vector2i:
	return Vector2i(Engine.get_frames_drawn(), Engine.get_process_frames())

static func elapsed(stamp: Vector2i) -> int:
	return Engine.get_process_frames() - stamp.y if DisplayServer.get_name() == "headless" else Engine.get_frames_drawn() - stamp.x

static func basis() -> String:
	return "headless-content" if DisplayServer.get_name() == "headless" else "drawn"
