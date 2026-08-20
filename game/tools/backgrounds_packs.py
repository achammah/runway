#!/usr/bin/env python3
"""Expansion packs — the road from 516 scenes to 1000+.

The owner: "look at different milestones in the potential life of any startup...
think of MANY crazy or weird scenarios to have truly ANY potential range of scenes.
You can very well be in the 1000+ scenes."

Same contract as backgrounds_taxonomy.py: every description names ONLY the place and
its objects — NEVER a person or an action (a knee summons a body; 17 of 73 base
places had this bug). Every room arrives EMPTY; the cast is assembled on slots.

    python3 tools/backgrounds_packs.py           # counts
    python3 tools/backgrounds_packs.py --json    # manifest for the generator
    python3 tools/backgrounds_packs.py --prompts # generation prompts
"""
import sys, json, itertools

CORE, SECONDARY, EPISODIC = "core", "secondary", "episodic"

PACKS = {
 # ── THE YC PACK — the accelerator is a world of its own ──────────────────────
 "yc": [
  ("yc_interview_room", SECONDARY, "a bare interview room with a long table, five empty chairs on one side and one on the other, a wall clock", "long table, chairs, wall clock, water bottles"),
  ("yc_lobby", EPISODIC, "an accelerator lobby with an orange logo wall, badge printer and rows of waiting chairs", "logo wall, badge printer, waiting chairs"),
  ("yc_group_office_hours", SECONDARY, "a small glass room with a round table, six chairs and a wall-mounted screen showing a blank graph grid", "round table, chairs, screen, sticky wall"),
  ("yc_batch_dinner", SECONDARY, "a big hall with long canteen tables set with paper plates, a stage with a single stool and mic stand", "canteen tables, paper plates, stage, mic stand"),
  ("yc_demo_backstage", EPISODIC, "a curtained backstage corridor with equipment cases, a monitor showing a countdown, taped cables", "curtains, flight cases, countdown monitor, taped cables"),
  ("yc_whiteboard_room", SECONDARY, "a tiny room whose every wall is whiteboard, one small table, marker trays", "whiteboard walls, small table, marker trays"),
  ("yc_alumni_party", EPISODIC, "a rooftop terrace strung with lights, a drinks table, a skyline at dusk", "string lights, drinks table, skyline"),
 ],
 # ── PRESS & SCANDAL — fame arrives, then the other kind ─────────────────────
 "press": [
  ("tv_studio_desk", SECONDARY, "a television studio with a curved interview desk, two chairs, camera rigs and hot lights", "interview desk, chairs, cameras, teleprompter"),
  ("podcast_booth", SECONDARY, "a small padded booth with two mics on arms, headphones on hooks, a neon on-air sign", "mic arms, headphones, on-air sign, foam walls"),
  ("congressional_hearing", EPISODIC, "a wood-panelled hearing chamber, a raised dais of empty seats, one small witness table with a single mic", "dais, witness table, single mic, name placard"),
  ("courthouse_steps", EPISODIC, "wide stone courthouse steps with a cluster of empty mic stands bolted together at the bottom", "stone steps, mic-stand cluster, columns"),
  ("crisis_war_room", EPISODIC, "an office at night with a wall of printed headlines taped up, pizza boxes, a whiteboard headed by a red line", "headline wall, pizza boxes, whiteboard, phones"),
  ("home_office_3am", SECONDARY, "a dark bedroom desk lit only by a monitor, a phone face-up and glowing, an untouched bed behind", "monitor glow, glowing phone, untouched bed"),
  ("magazine_cover_shoot", EPISODIC, "a photo studio with a white cyclorama, a stool, umbrella lights and a fan on a stand", "cyclorama, stool, umbrella lights, fan"),
 ],
 # ── CRISIS — the weeks that end companies ───────────────────────────────────
 "crisis": [
  ("burning_office", EPISODIC, "an office doorway with smoke rolling along the ceiling, a fire extinguisher on the wall, papers scattered mid-air", "smoke, extinguisher, scattered papers, exit sign"),
  ("flooded_garage", EPISODIC, "a garage shin-deep in water, a power strip hanging just above the surface, floating pizza boxes", "standing water, hanging power strip, floating boxes"),
  ("police_raid_office", EPISODIC, "an office with evidence boxes stacked by the door, yellow tape across a desk, drawers pulled open", "evidence boxes, yellow tape, open drawers"),
  ("repo_day", EPISODIC, "an office being emptied: a hand truck by the door, pale rectangles on the wall where frames were, one chair left", "hand truck, wall shadows, one chair"),
  ("pawn_shop", EPISODIC, "a pawn shop counter with barred glass, shelves of electronics with price tags, a bell on the counter", "barred counter, tagged electronics, bell"),
  ("storage_auction", EPISODIC, "a roller-door storage unit half-open onto a crowd barrier, lot numbers stickered on furniture", "roller door, crowd barrier, lot stickers"),
  ("eviction_door", EPISODIC, "an office front door with a taped notice, a cardboard box of desk things beside it, keys in the lock", "taped notice, box of desk things, keys"),
  ("er_waiting", EPISODIC, "a hospital emergency waiting room at night, rows of bolted chairs, a triage window, a wall-mounted TV", "bolted chairs, triage window, wall TV"),
  ("server_meltdown", EPISODIC, "a server closet with one rack smoking faintly, a melted cable bundle, a big red switch", "smoking rack, melted cables, red switch"),
  ("jail_cell", EPISODIC, "a small holding cell with a bench, a barred door and a payphone on the wall outside", "bench, barred door, payphone"),
 ],
 # ── MILESTONES & EXCESS — the good weeks, and the too-good weeks ────────────
 "milestone": [
  ("first_office_keys", EPISODIC, "an empty just-leased office, keys on the bare floor in a shaft of light, one folding chair", "bare floor, keys, folding chair, radiator"),
  ("launch_cake", EPISODIC, "an office kitchen with a sheet cake iced with a rocket, paper cups, a hand-lettered banner", "sheet cake, paper cups, banner"),
  ("million_user_wall", EPISODIC, "an office wall painted with a giant 1,000,000 in wet paint, a ladder and paint tray below", "painted number, ladder, paint tray"),
  ("yacht_party", EPISODIC, "a yacht rear deck with a drinks table, white sofas, a flag snapping on a pole, open sea", "deck, drinks table, white sofas, flag"),
  ("casino_table", EPISODIC, "a casino blackjack table with chip stacks, green felt, a shoe of cards, low hanging lamp", "chip stacks, green felt, card shoe, lamp"),
  ("shenzhen_factory", SECONDARY, "an electronics factory floor with conveyor lines of small devices, anti-static mats, big red counters on the wall", "conveyors, device trays, wall counters"),
  ("customs_seizure", EPISODIC, "an airport customs back room with an opened crate of devices, inspection table, rubber stamps", "opened crate, inspection table, stamps"),
  ("wedding_altar", EPISODIC, "a wedding aisle and flowered altar seen from the back row, order-of-service sheets on chairs, one buzzing phone on a seat", "aisle, altar, chairs, phone on seat"),
  ("award_stage", EPISODIC, "an awards-show stage with a glowing trophy on a plinth, a mic stand, a giant screen with a spotlight", "trophy plinth, mic stand, giant screen"),
  ("wired_wall_street", EPISODIC, "a bank trading floor visited at dawn, rows of six-screen desks, ticker band on the wall", "six-screen desks, ticker band"),
 ],
 # ── THE GRIND, WEIRDER — places a founder genuinely ends up ─────────────────
 "weird": [
  ("laundromat_office", EPISODIC, "a laundromat at night with a laptop on a folding table between rows of machines", "washing machines, folding table, detergent shelf"),
  ("24h_diner", SECONDARY, "a diner booth at 2am, laminated menus, a coffee pot on the table, neon in the window", "booth, menus, coffee pot, neon"),
  ("parking_rooftop", EPISODIC, "the top deck of a parking garage at dusk, one car, a low wall over the city", "parking deck, one car, city view"),
  ("church_pew_thinking", EPISODIC, "an empty church interior, long pews, candle rack, light through a tall window", "pews, candle rack, tall window"),
  ("bathtub_office", EPISODIC, "a bathroom with a laptop on a board across the tub, a towel rail of drying clothes", "tub with board, laptop, towel rail"),
  ("ikea_showroom", EPISODIC, "a furniture showroom fake office with price tags on everything, a fake window with a printed view", "tagged furniture, fake window, arrows on floor"),
  ("blood_donation", EPISODIC, "a blood donation clinic with recline chairs, juice and biscuits table, pinned health posters", "recline chairs, juice table, posters"),
  ("night_bus", EPISODIC, "the top deck of a night bus, empty seats, city lights sliding past the windows, a bell strip", "bus seats, windows, bell strip"),
  ("mattress_hq", EPISODIC, "a studio apartment where a mattress leans on the wall to make room for two desks and a wall chart", "leaning mattress, two desks, wall chart"),
  ("gym_locker_pitch", EPISODIC, "a gym locker room with benches, lockers ajar, one towel and one laptop on the bench", "benches, lockers, laptop on bench"),
 ],
 # ── THE MONEY WORLD, DEEPER ─────────────────────────────────────────────────
 "money2": [
  ("term_sheet_room", SECONDARY, "a law-firm signing room, long table with two pens on blotters, a stack of tabbed documents", "blotters, pens, tabbed documents, carafe"),
  ("due_diligence_room", EPISODIC, "a windowless data room with banker boxes on shelves, one table, a numbered-folder wall", "banker boxes, numbered folders, table"),
  ("bank_vault_door", EPISODIC, "a bank corridor ending in an open round vault door, deposit boxes inside", "vault door, deposit boxes, corridor"),
  ("investor_garden_party", EPISODIC, "a manicured garden with a marquee, round tables in white cloth, a croquet set abandoned mid-game", "marquee, round tables, croquet set"),
  ("board_room_showdown", SECONDARY, "a boardroom with a long table set with name placards, one chair pulled far back from the rest", "placards, long table, pulled-back chair"),
  ("secondary_sale_cafe", EPISODIC, "a discreet hotel cafe corner, two armchairs, a low table with one folder on it", "armchairs, low table, single folder"),
 ],
}

TIMES = ["day", "night", "small_hours"]
CONDITIONS = ["thriving", "steady", "in_the_red"]
FRAMINGS = ["wide", "medium"]

def variants(tier):
    if tier == CORE:
        return list(itertools.product(TIMES, CONDITIONS, FRAMINGS))
    if tier == SECONDARY:
        return list(itertools.product(TIMES[:2], CONDITIONS, FRAMINGS[:1]))
    return [(TIMES[0], "steady", "wide"), (TIMES[1], "in_the_red", "wide"),
            (TIMES[1], "thriving", "wide")]

def build():
    out = []
    for fam, places in PACKS.items():
        for place, tier, desc, objects in places:
            for time, cond, fram in variants(tier):
                out.append({"id": f"{fam}/{place}/{time}_{cond}_{fram}",
                    "family": fam, "place": place, "tier": tier, "time": time,
                    "condition": cond, "framing": fram,
                    "description": desc, "objects": objects})
    return out

if __name__ == "__main__":
    entries = build()
    if "--json" in sys.argv:
        print(json.dumps(entries, indent=1)); sys.exit()
    if "--prompts" in sys.argv:
        lit = {"day": "flat daylight", "night": "warm lamplight against dark windows",
               "small_hours": "a single cold light source, everything else in shadow"}
        cond = {"thriving": "well kept, a sense of money", "steady": "lived in, ordinary",
                "in_the_red": "fraying: bare shelves, unpaid notices, litter"}
        shot = {"wide": "a wide establishing shot", "medium": "a medium shot, closer"}
        for e in entries:
            print(f'{e["id"]}\t{shot[e["framing"]]} of {e["description"]}. It contains '
                  f'{e["objects"]}. Lit by {lit[e["time"]]}. The place is {cond[e["condition"]]}. '
                  f'COMPLETELY EMPTY OF PEOPLE.')
        sys.exit()
    n_places = sum(len(v) for v in PACKS.values())
    print(f"{'PACK':12s} PLACES  SCENES")
    tot = 0
    for f, places in PACKS.items():
        n = sum(len(variants(t)) for _, t, _, _ in places)
        tot += n
        print(f"  {f:10s} {len(places):5d} {n:7d}")
    print(f"\n  {'TOTAL':10s} {n_places:5d} {tot:7d}   (+516 base = {tot+516} scenes)")
