extends RefCounted
## coddy_anim · ParkingLot — a park pocket / bay row with REAL slot reservation: every slot holds at most one vehicle;
## a vehicle that finds the lot full waits in a FIFO queue (and physically queues on the road before `gate`, kept apart
## by the following distance). release() hands the slot to the queue head on its next request.

var road_node := ""             ## graph node the lot is reached from
var gate := Vector3.ZERO        ## stop line / entrance
var slots: Array = []           ## Vector3 per slot
var aisle: Array = []           ## Vector3 per slot: the aisle point in front of it (path gate -> aisle -> slot)
var occupant := PackedInt32Array()
var queue: Array = []           ## vehicle ids waiting, FIFO


func setup(node_id: String, gate_p: Vector3, slot_pts: Array, aisle_pts: Array = []) -> void:
	road_node = node_id
	gate = gate_p
	slots = slot_pts
	aisle = aisle_pts if aisle_pts.size() == slot_pts.size() else slot_pts.duplicate()
	occupant.resize(slots.size())
	occupant.fill(-1)
	queue.clear()


## A row of n slots beside the gate: aisle along `dir` from the gate, slots `side_m` to its right, `pitch` apart.
func setup_row(node_id: String, gate_p: Vector3, dir: Vector3, n: int, pitch: float = 3.0, side_m: float = 3.0) -> void:
	var right := Vector3(-dir.z, 0.0, dir.x).normalized()
	var s: Array = []
	var a: Array = []
	for k in n:
		var ap := gate_p + dir.normalized() * (pitch * float(k + 1))
		a.append(ap)
		s.append(ap + right * side_m)
	setup(node_id, gate_p, s, a)


func slot_of(id: int) -> int:
	return occupant.find(id)


## The vehicle's slot, reserving the first free one when it is the queue head; -1 = wait (it is now in the queue).
func request(id: int) -> int:
	var have := slot_of(id)
	if have >= 0:
		return have
	if not queue.has(id):
		queue.append(id)
	if queue[0] != id:
		return -1
	var free := occupant.find(-1)
	if free < 0:
		return -1
	occupant[free] = id
	queue.pop_front()
	return free


func release(id: int) -> void:
	var k := slot_of(id)
	if k >= 0:
		occupant[k] = -1
	queue.erase(id)


func occupied() -> int:
	var n := 0
	for o in occupant:
		if o >= 0:
			n += 1
	return n
