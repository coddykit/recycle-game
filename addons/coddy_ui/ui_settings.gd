extends RefCounted
## coddy_ui settings building blocks — the standard rows every game's Settings has, in the fleet look. The game passes
## translated text (the kit never calls tr()). Sections are an Overline + a Group of rows (UiKit.section / group).
##   var col := modal.columns(2)
##   UiSettings.section(col[0], tr("Sound"), [UiRow.toggle("music", tr("Music"), on, set_music), ...])
##   UiSettings.privacy(col[1], Ads, {"title": tr("Privacy"), "options": tr("Privacy options"), "policy": tr("Privacy policy")})
##
## PRIVACY (Patron 10-22, AdMob GDPR + US state messages live fleet-wide): the US message only opens from the in-app
## "Privacy options" entry. Standard: the row shows ONLY when Google UMP says privacyOptionsRequirementStatus == REQUIRED
## (`ads.privacy_options_required()`), a tap calls `ads.show_privacy_options()` (UMP showPrivacyOptionsForm); next to it
## the privacy policy link (PRIVACY_URL) is always shown. `ads` = the game's CoddyAds autoload (games/tools/ads/CoddyAds.gd
## has both methods); null → only the policy link. Families / kids games: same rows (the policy link is required there too).

const PRIVACY_URL := "https://www.coddykit.com/pages/privacy-policy"
const UiRow = preload("ui_row.gd")

static var opened_urls: Array = []     ## tests: links opened (OS.shell_open is skipped headless)


## Overline title + a group of rows, added to `parent`. Returns the group.
static func section(parent: Control, title: String, rows: Array) -> PanelContainer:
	var kit = load("res://addons/coddy_ui/ui_kit.gd")
	var head: Label = kit.section(title)
	head.name = "Section_" + title.left(24).validate_node_name()
	parent.add_child(head)
	var g: PanelContainer = kit.group()
	for r in rows:
		if r != null:
			kit.group_add(g, r)
	parent.add_child(g)
	return g


## The privacy block: "Privacy options" (only when UMP requires it) + "Privacy policy" (always).
## labels: title, options, policy (translated). Row names: PrivacyOptions, PrivacyPolicy.
static func privacy(parent: Control, ads: Object, labels: Dictionary, extra_rows: Array = []) -> PanelContainer:
	var rows: Array = extra_rows.duplicate()
	if privacy_options_visible(ads):
		var opt: Button = UiRow.nav("shield-check", str(labels.get("options", "Privacy options")), "", func() -> void: ads.call("show_privacy_options"))
		opt.name = "PrivacyOptions"
		rows.append(opt)
	var pol: Button = UiRow.nav("book-open", str(labels.get("policy", "Privacy policy")), "coddykit.com", open_policy)
	pol.name = "PrivacyPolicy"
	rows.append(pol)
	return section(parent, str(labels.get("title", "Privacy")), rows)


static func privacy_options_visible(ads: Object) -> bool:
	return ads != null and is_instance_valid(ads) and ads.has_method("privacy_options_required") and ads.has_method("show_privacy_options") and bool(ads.call("privacy_options_required"))


static func open_policy() -> void:
	opened_urls.append(PRIVACY_URL)
	if DisplayServer.get_name() != "headless":
		OS.shell_open(PRIVACY_URL)
