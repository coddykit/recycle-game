extends Sheet
var _clock := 0.0
func _init() -> void:
	super._init()
	title = "Daily logistics"
	icon_name = "gift"
	height_frac = 0.78
func on_opened() -> void:
	DailyManager.ensure_day()
	DailyManager.changed.connect(refresh)
	Monetization.changed.connect(refresh)
func build(b: VBoxContainer) -> void:
	# the old explainer note is the header's summary line (long → short)
	set_sub([tr("Three optional jobs. No missed-day penalty.") + "  ·  " + DailyManager.reset_text(), tr("Three optional jobs. No missed-day penalty."), DailyManager.reset_text()])
	if DailyManager.state.is_empty():
		b.add_child(Kit.empty_card("triangle-alert", tr("Could not save. Check available storage and try again."), "", "", Callable(), low()))
		return
	var day := int(DailyManager.state.day)
	for task in DailyManager.state.tasks:
		var amount := DailyManager.progress(task)
		var done: bool = amount >= float(task.target)
		var card := row("contract", DailyManager.description(task), ["%s / %s" % [Fmt.commas(int(amount)), Fmt.commas(int(task.target))]],
				{"selected": done and not task.claimed and day == DailyManager.utc_day()})
		card.name = "task_" + str(task.id)
		b.add_child(card)
		progress(card, minf(amount, float(task.target)), float(task.target))
		var claim := pill(card, tr("Claimed") if task.claimed else tr("Claim %s") % Fmt.money(float(task.reward)), func():
			DailyManager.claim(str(task.id), day)
			refresh(), not task.claimed and done and day == DailyManager.utc_day())
		claim.name = "claim_" + str(task.id)
		# opt-in rewarded ad: a claimed reward pays once more (ad-free: no video), within the day's reward budget — a quiet
		# outlined chore under the row (its long caption wraps between words instead of widening the pill)
		var extra := Monetization.daily_double_amount(str(task.id))
		if task.claimed and not task.get("doubled", false) and day == DailyManager.utc_day() and extra > 0.0 and Monetization.placement_ready("daily_double"):
			Monetization.note_offer("daily_double", extra)
			var more := "+" + Fmt.money(extra)
			var dbl := Kit.button(tr("Double %s") % more if Monetization.video_free() else tr("Watch ad · double %s") % more, "secondary", "",
					func(): Monetization.claim_placement("daily_double", {"task": str(task.id), "day": day}))
			dbl.theme_type_variation = "OutlineButton"
			dbl.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
			dbl.name = "double_" + str(task.id)
			Kit.list_card_body(card).add_child(dbl)


func live_update(delta: float) -> void:
	_clock += delta
	if _clock >= 5:
		_clock = 0
		DailyManager.ensure_day()
		refresh()
