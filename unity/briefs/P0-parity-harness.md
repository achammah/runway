# P0 — Parity harness (the twin camera)
Checklist: B1 (feeds B2-B26). BUILD: NEW `Editor/UnityShots.cs` (or runtime
harness keyed by env RUNWAY_USHOTS=<dir>): reproduce EVERY state the Godot
harnesses photograph (read game/tests/new_screens_shot.gd, select_shot,
binder_shot, howto_shot, birth_shot, traits_shot for the exact states +
fixture data — same companies, same offers, same slots) and save PNGs with
IDENTICAL filenames to <dir>. Runtime screenshots via
ScreenCapture.CaptureScreenshotAsTexture after WaitForEndOfFrame.
VERIFY: run it, list the shots, confirm none is black/empty (pixel-variance
check). 100% = one command produces the full twin set for my side-by-side.
