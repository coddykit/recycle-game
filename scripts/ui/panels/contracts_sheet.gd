extends Sheet
## Incoming contract offers + the accepted queue.

var _cards: Array = []
var _sig_count := -1


func _init() -> void:
	super._init()
	title = "Contracts"
	icon_name = "clipboard-list"
	height_frac = 0.64


func on_opened() -> void:
	ContractManager.offers_changed.connect(refresh)
	ContractManager.queue_changed.connect(refresh)
	VehicleManager.vehicle_changed.connect(_on_vehicle_changed)
	EconomyManager.reputation_changed.connect(func(_a, _b): refresh())


func _on_vehicle_changed(_v) -> void:
	refresh()


func build(b: VBoxContainer) -> void:
	_cards.clear()
	var max_q := int(Data.config.get("max_queue", 4))
	var offers_txt := tr("New offers  %d/%d") % [ContractManager.offers.size(), ProgressionManager.offer_slots()]
	var queue_txt := tr("Accepted  %d/%d") % [ContractManager.queue.size(), max_q]
	var sub := [offers_txt + "  ·  " + queue_txt, offers_txt]
	if ProgressionManager.auto_enabled("auto_accept"):
		sub = [offers_txt + "  ·  " + tr("AUTO ACCEPT ON"), offers_txt]
	set_sub(sub)
	# the paid refresh: a quiet outlined chore beside the close chip, its price on one line
	var refresh_b := UIKit.button(Fmt.money(ContractManager.refresh_cost()), "", "refresh", Vector2(0, 84), 22)
	refresh_b.theme_type_variation = "OutlineButton"
	refresh_b.name = "refresh_offers"
	# a price label must stay on one line: word-smart autowrap broke "$25" into one glyph per row
	refresh_b.autowrap_mode = TextServer.AUTOWRAP_OFF
	refresh_b.clip_text = false
	refresh_b.add_theme_constant_override("icon_max_width", 30)
	refresh_b.custom_minimum_size.x = maxf(refresh_b.custom_minimum_size.x, 140)
	refresh_b.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	refresh_b.disabled = not EconomyManager.can_afford(ContractManager.refresh_cost()) or not TutorialManager.allow_offer_spawning()
	refresh_b.pressed.connect(func():
		if ContractManager.paid_refresh():
			AudioManager.play("whoosh"))
	header_extra.add_child(refresh_b)
	if ContractManager.queue.size() > 0:
		var qh := UIKit.hbox(8)
		b.add_child(qh)
		var qs := section(tr("Accepted  %d/%d") % [ContractManager.queue.size(), max_q])
		qs.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		qh.add_child(qs)
		if ProgressionManager.auto_enabled("auto_assign") and ContractManager.ready_pairs().size() > 0:
			var da := Kit.action_pill(tr("Dispatch All"), func(): ContractManager.dispatch_all_ready(), true, "primary", "arrow-right", low())
			da.size_flags_vertical = Control.SIZE_SHRINK_END
			qh.add_child(da)
		for c in ContractManager.queue:
			_add_card(b, c, true)
	if ContractManager.queue.size() > 0 or ContractManager.offers.is_empty():
		b.add_child(section(offers_txt))
	if ContractManager.offers.is_empty():
		b.add_child(Kit.empty_card("clock", tr("New contracts are on their way..."), "", "", Callable(), low()))
	var first := true
	for c in ContractManager.offers:
		_add_card(b, c, false, first)
		first = false


func _add_card(b: VBoxContainer, c: Dictionary, queued: bool, first := false) -> void:
	var refs := ContractCard.build(c, queued, first,
			func(): _on_accept(c),
			func():
				AudioManager.play("whoosh")
				ContractManager.reject(c),
			func(): ContractManager.toggle_priority(c))
	b.add_child(refs.card)
	_cards.append(refs)
	if c.get("fresh", false):
		c.fresh = false
		UIKit.pop_in(refs.card, 0.05 * _cards.size())


func _on_accept(c: Dictionary) -> void:
	if not ContractManager.rep_ok(c):
		return
	var best := ContractManager.best_vehicle(c)
	if best.is_empty():
		# nobody free right now: park it in the queue
		if ContractManager.accept(c):
			AudioManager.play("accept")
			UIManager.toast("Queued - dispatch when a vehicle is free", UIKit.BLUE, "contract")
		else:
			UIManager.toast("Queue is full", UIKit.RED, "risk")
			AudioManager.play("error")
		return
	AudioManager.play("accept")
	AudioManager.voice("accept", 45.0)
	hud.open_panel("dispatch", c.id)


func live_update(_delta: float) -> void:
	for r in _cards:
		var bar: ProgressBar = r.bar
		if bar and is_instance_valid(bar):
			var lt: Array = Data.config.get("offer_lifetime", [70, 120])
			bar.value = clampf(float(r.contract.expires) / float(lt[1]), 0.0, 1.0)
