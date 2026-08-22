# PERF — the numbers twin
Checklist: E1-E5 groundwork. BUILD: NEW `App/UnityPerf.cs` keyed by
RUNWAY_UPERF=<dir>: per screen (title, draft p1, birth loop, howto, curtain
shut, dice mid-roll, garage) settle 3s then record 3s averages: frame ms
(Time.unscaledDeltaTime), FPS, Profiler.GetTotalAllocatedMemoryLong, GC
collections, Canvas.willRenderCanvases count as the rebuild proxy; write a
markdown table to <dir>/unity_perf.md. Also a 10-minute scripted hitch hunt
(max frame ms, count >50ms) as mode RUNWAY_UPERF_SOAK=1.
VERIFY: run both, publish the tables. 100% = numbers ready for the E6
side-by-side against Godot's measured set (draft 382-620ms, settle 297ms,
12-25 redraws/s).
