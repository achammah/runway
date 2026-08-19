#!/usr/bin/env python3
"""RUNWAY! — the background taxonomy.

The owner's brief: "think of 500+ situations a startup can be in as a background
with a clear taxonomy. For example, the founder can go back to his parent's or
girlfriend, they can do something in a basement of an office, in an old hangar."

The places ARE the story. A founder's arc is told by where they end up sleeping,
who they have to sit across from, and what room they are in when it goes wrong. So
the library is not five office types with lighting variants — it is the whole map of
places a startup drags you through.

HOW IT IS USED AT RUNTIME. The DM never names a file. It emits structured facets
(family, place, time, condition, framing) and `resolve()` finds the best match,
dropping facets in a fixed priority order until something exists. That keeps the
model from hallucinating a filename and guarantees a scene always resolves.

  python3 tools/backgrounds_taxonomy.py            # print the plan and the counts
  python3 tools/backgrounds_taxonomy.py --json     # emit the manifest
  python3 tools/backgrounds_taxonomy.py --prompts  # emit one generation prompt per entry
"""
import json, sys, itertools

TIMES = ["day", "night", "small_hours"]
CONDITIONS = ["thriving", "steady", "in_the_red"]
FRAMINGS = ["wide", "medium"]

# TIER decides how many variants a place earns.
#   core      the rooms the player lives in            -> every time x condition x framing
#   secondary places visited repeatedly                -> 2 times x 2 conditions, wide only
#   episodic  places visited once or twice in a run    -> 1-2 variants
CORE, SECONDARY, EPISODIC = "core", "secondary", "episodic"

# THE DESCRIPTION MUST NAME ONLY THE PLACE AND ITS OBJECTS — NEVER A PERSON OR AN
# ACTION. A pilot generated a creature on the sofa despite "EMPTY OF PEOPLE",
# because the text said "a laptop balanced on a knee": a knee implies a body and
# outweighs the instruction. 17 of 73 entries had this. Rooms must arrive empty,
# because any occupant here DOUBLES against the composited cast.
# family -> [(place, tier, what the room is, the objects that must be in it)]
PLACES = {
 # where you sleep when it is going badly. The retreat ladder IS the failure curve.
 "home_retreat": [
   ("parents_livingroom", SECONDARY, "a suburban living room with a floral sofa and a laptop left open on the cushion", "sofa, side table, family photos, laptop"),
   ("childhood_bedroom", SECONDARY, "a small bedroom kept exactly as it was at seventeen, posters still up", "single bed, desk, posters, trophy shelf"),
   ("partner_flat", SECONDARY, "a tidy flat with houseplants and a laptop open on the kitchen table", "kitchen table, plants, tidy shelves"),
   ("friends_couch", EPISODIC, "a friend's cluttered lounge with a rolled sleeping bag by the couch", "couch, sleeping bag, games console"),
   ("own_flat_empty", SECONDARY, "a flat with the furniture sold, a mattress on the floor and a monitor on a box", "mattress, boxes, single monitor"),
   ("car_backseat", EPISODIC, "the back seat of a car at night, laptop glow, fast food wrappers", "car seats, laptop, wrappers"),
   ("sublet_room", EPISODIC, "a bare rented room with a desk against the window", "desk, bare bed, suitcase"),
 ],
 # where you build before anyone takes you seriously
 "scrappy_workspace": [
   ("garage", CORE, "a suburban garage converted into a workshop", "workbench, pegboard, whiteboard, crate, garage door"),
   ("basement_office", CORE, "a windowless office basement with pipes overhead and strip lighting", "pipes, strip lights, folding tables, damp patch"),
   ("old_hangar", CORE, "a vast disused aircraft hangar with a tiny desk island in the middle", "hangar doors, girders, lone desk cluster, forklift"),
   ("storage_unit", SECONDARY, "a rented storage unit with a roller door, a desk and a camping lamp among the shelving", "roller door, shelving, stacked boxes, camping lamp"),
   ("back_of_shop", SECONDARY, "the stockroom behind a small shop, a desk wedged between the stock shelves", "stock shelves, till boxes, back door"),
   ("church_hall", EPISODIC, "a rented church hall with stacked chairs and a trestle table", "stacked chairs, trestle table, high windows"),
   ("university_lab", SECONDARY, "a corner of a university lab with borrowed equipment", "lab bench, equipment, cable trays"),
   ("shipping_container", EPISODIC, "a shipping container fitted out as an office", "container walls, small window, heater"),
   ("barn", EPISODIC, "a converted barn with beams and space heaters", "beams, hay bales, space heater, long table"),
   ("attic", SECONDARY, "a low attic room with a skylight and a sloping ceiling", "skylight, sloping ceiling, low desk"),
   ("houseboat", EPISODIC, "a narrowboat cabin turned into a workspace", "cabin windows, compact desk, ropes"),
 ],
 # the legitimate ladder
 "legit_workspace": [
   ("coworking_hotdesk", CORE, "a bright coworking floor of hot desks and beanbags", "hot desks, beanbags, kanban board, coffee bar"),
   ("coworking_phonebooth", SECONDARY, "a glass phone booth in a coworking space", "glass booth, stool, small shelf"),
   ("small_office", CORE, "a first proper office: a handful of desks and a window", "desks, window, small whiteboard, plant"),
   ("open_floor", CORE, "an open plan floor of desk rows", "desk rows, monitors, breakout corner"),
   ("glass_boardroom", SECONDARY, "a glass walled boardroom with a long table", "long table, screen, glass walls"),
   ("hq_atrium", CORE, "a company atrium with a staircase and a logo wall", "staircase, logo wall, plants, reception"),
   ("hq_skyline", CORE, "a top floor office with a city skyline through floor to ceiling glass", "glass wall, skyline, long table, awards shelf"),
   ("server_corner", EPISODIC, "a corner rack of servers with cable spaghetti", "server rack, cables, blinking lights"),
 ],
 # where money is given and taken away
 "money": [
   ("vc_office", CORE, "a venture fund's meeting room, expensive and cold", "long table, art, water carafe, city view"),
   ("vc_lobby", SECONDARY, "a fund's waiting lobby with hard chairs", "reception desk, hard chairs, magazines"),
   ("angel_kitchen", SECONDARY, "an angel investor's kitchen table, informal and dangerous", "kitchen table, fruit bowl, laptop"),
   ("bank_branch", EPISODIC, "a high street bank branch with a queue barrier", "counter, queue barrier, posters"),
   ("pitch_stage", CORE, "a pitch stage with a mic stand and a huge screen", "stage, mic stand, screen, front row"),
   ("demo_day", CORE, "a demo day hall, rows of seats, a bright stage", "rows of chairs, stage, banner"),
   ("family_office", EPISODIC, "a discreet family office with panelled walls", "panelled walls, antique desk"),
   ("penthouse_party", EPISODIC, "a penthouse with floor to ceiling glass, a bar and abandoned glasses", "floor to ceiling glass, bar, abandoned glasses"),
   ("video_call_wall", SECONDARY, "a desk with a large monitor filling the frame, its screen a grid of empty video-call tiles", "monitor, empty call tiles, desk edge"),
 ],
 # where revenue actually lives
 "customer": [
   ("trade_show_booth", CORE, "a trade show booth on a hall floor, aisles empty", "booth, banner, leaflet table, carpet aisle"),
   ("client_warehouse", SECONDARY, "a client's warehouse with racking and a forklift", "racking, pallets, forklift, clipboard"),
   ("retail_floor", SECONDARY, "a shop floor with a till and shelves", "till, shelves, shopping baskets"),
   ("hospital_ward", EPISODIC, "a hospital corridor with a trolley, ward doors and a notice board", "trolley, ward doors, notice board"),
   ("factory_line", SECONDARY, "a factory line with conveyor and safety markings", "conveyor, safety lines, control panel"),
   ("farm_yard", EPISODIC, "a muddy farmyard with a barn and a pickup", "barn, pickup, mud, fence"),
   ("restaurant_kitchen", EPISODIC, "a restaurant kitchen with a steel pass, counters and a rail of order tickets", "pass, steel counters, ticket rail"),
   ("construction_site", EPISODIC, "a construction site with scaffolding, a site hut and stacked materials", "scaffolding, site hut, stacked materials"),
   ("school_classroom", EPISODIC, "a classroom where the software is being piloted", "desks, whiteboard, posters"),
 ],
 # the boring rooms that end companies
 "institutional": [
   ("lawyer_office", SECONDARY, "a lawyer's office lined with case files", "shelves of files, heavy desk, chairs"),
   ("accountant_office", SECONDARY, "an accountant's cramped office of ring binders", "ring binders, calculator, small desk"),
   ("courtroom", EPISODIC, "a small empty courtroom with benches and a raised judge's bench", "benches, raised bench, flag"),
   ("patent_office", EPISODIC, "a patent office counter with numbered tickets", "counter, ticket machine, notices"),
   ("immigration_office", EPISODIC, "an immigration waiting room of plastic chairs", "plastic chairs, number screen, forms"),
   ("tax_office", EPISODIC, "a tax office interview room, bare and grey", "bare table, two chairs, filing cabinet"),
 ],
 # the grind between places
 "transit": [
   ("airport_gate", SECONDARY, "an airport gate at an unsociable hour", "gate seating, departure screen, window"),
   ("plane_cabin", EPISODIC, "an economy cabin with a laptop on a tray table", "tray table, seat backs, window"),
   ("train_carriage", SECONDARY, "a train carriage table seat with a laptop", "table seat, window, luggage rack"),
   ("rental_car", EPISODIC, "a rental car parked outside a client's building", "dashboard, windscreen, coffee cup"),
   ("hotel_room", SECONDARY, "a chain hotel room used as an office", "bed, desk, blackout curtain, kettle"),
   ("motel_room", EPISODIC, "a cheap motel room with a flickering sign outside", "twin bed, neon through curtain"),
   ("conference_hotel_lobby", SECONDARY, "a conference hotel lobby with low chairs, a sponsor banner and a lanyard table", "lobby chairs, sponsor banner, lanyard table"),
 ],
 # where the company follows you
 "social": [
   ("launch_party", SECONDARY, "a room set for a launch party: banner, drinks table, unopened bottles", "banner, drinks table, unopened bottles"),
   ("industry_mixer", EPISODIC, "an industry mixer room with high tables, a bar and a sheet of unclaimed name badges", "high tables, name badges, bar"),
   ("wedding_reception", EPISODIC, "a wedding reception room with round tables, string lights and an empty dance floor", "round tables, string lights, dance floor"),
   ("funeral", EPISODIC, "a quiet chapel with pews and flowers at the front", "pews, flowers, order of service"),
   ("family_dinner", SECONDARY, "a family dining table laid for dinner, dishes served and chairs pushed back", "dining table, dishes, pushed-back chairs"),
   ("school_reunion", EPISODIC, "a school reunion in a hired hall", "hall, name tags, old photos"),
 ],
 # the cost the founder pays
 "body_mind": [
   ("clinic_waiting", SECONDARY, "a clinic waiting room with a ticket display", "waiting chairs, ticket display, posters"),
   ("therapist_office", SECONDARY, "a therapist's room with two armchairs and a plant", "two armchairs, plant, tissues, clock"),
   ("hospital_bed", EPISODIC, "a hospital bed with a laptop that should not be there", "bed, drip stand, curtain, laptop"),
   ("gym_2am", EPISODIC, "an empty 24 hour gym at two in the morning", "treadmills, mirrors, vending machine"),
   ("pharmacy", EPISODIC, "a late night pharmacy counter", "counter, shelves, harsh light"),
 ],
 # how it stops
 "endings": [
   ("nasdaq_bell", CORE, "an exchange balcony with the bell and the ticker wall, the floor below empty", "balcony, bell, ticker wall, railing"),
   ("signing_room", SECONDARY, "an acquisition signing room with pens and a thick contract", "long table, contract, pens, water"),
   ("empty_office_cleared", CORE, "an office being cleared: boxes, dust rectangles where desks were", "boxes, dust marks, stripped cables"),
   ("returned_laptops", SECONDARY, "a storage room of returned laptops in labelled boxes", "shelves, labelled boxes, laptops"),
   ("liquidation_auction", EPISODIC, "a hall of office furniture carrying lot stickers, chairs stacked in rows", "lot stickers, stacked chairs, rostrum"),
   ("press_conference", EPISODIC, "a press conference room: microphones on a lectern, a branded backdrop, rows of empty chairs", "lectern, microphones, backdrop, empty chairs"),
 ],
}

def variants(tier):
    if tier == CORE:
        return list(itertools.product(TIMES, CONDITIONS, FRAMINGS))
    if tier == SECONDARY:
        return list(itertools.product(TIMES[:2], CONDITIONS, FRAMINGS[:1]))
    return [(TIMES[0], "steady", "wide"), (TIMES[1], "in_the_red", "wide"),
            (TIMES[1], "thriving", "wide")]

def build():
    out = []
    for family, places in PLACES.items():
        for place, tier, desc, objects in places:
            for time, cond, fram in variants(tier):
                out.append({
                    "id": f"{family}/{place}/{time}_{cond}_{fram}",
                    "family": family, "place": place, "tier": tier,
                    "time": time, "condition": cond, "framing": fram,
                    "description": desc, "objects": objects,
                })
    return out

## Runtime selection. The DM sends facets, never a filename. Facets are dropped in
## this order until something matches, so a scene ALWAYS resolves.
DROP_ORDER = ["framing", "time", "condition", "place", "family"]

def resolve(entries, want):
    have = dict(want)
    for drop in [None] + DROP_ORDER:
        if drop:
            have.pop(drop, None)
        hits = [e for e in entries if all(e.get(k) == v for k, v in have.items())]
        if hits:
            return hits[0]["id"], (drop or "exact")
    return entries[0]["id"], "fallback"

if __name__ == "__main__":
    entries = build()
    if "--json" in sys.argv:
        print(json.dumps(entries, indent=1)); sys.exit()
    if "--prompts" in sys.argv:
        for e in entries:
            light = {"day": "flat daylight", "night": "warm lamplight against dark windows",
                     "small_hours": "a single cold light source, everything else in shadow"}[e["time"]]
            cond = {"thriving": "well kept, full shelves, good kit, a sense of money",
                    "steady": "lived in, ordinary, neither rich nor desperate",
                    "in_the_red": "fraying: bare shelves, unpaid notices, a dead plant, litter"}[e["condition"]]
            shot = {"wide": "a wide establishing shot of the whole room",
                    "medium": "a medium shot, closer, one part of the room filling the frame"}[e["framing"]]
            print(f'{e["id"]}\t{shot} of {e["description"]}. It contains {e["objects"]}. '
                  f'Lit by {light}. The place is {cond}. EMPTY OF PEOPLE.')
        sys.exit()
    by_family, by_tier = {}, {}
    for e in entries:
        by_family[e["family"]] = by_family.get(e["family"], 0) + 1
        by_tier[e["tier"]] = by_tier.get(e["tier"], 0) + 1
    print(f"{'FAMILY':22s} PLACES  BACKGROUNDS")
    for f, places in PLACES.items():
        print(f"  {f:20s} {len(places):5d} {by_family[f]:12d}")
    print(f"\n{'':22s} {sum(len(p) for p in PLACES.values()):5d} {len(entries):12d}  TOTAL")
    print(f"\nby tier: " + ", ".join(f"{k}={v}" for k, v in by_tier.items()))
    print(f"\nvariants per tier: core={len(variants(CORE))}, secondary={len(variants(SECONDARY))}, episodic={len(variants(EPISODIC))}")
