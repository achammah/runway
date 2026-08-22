# Lane briefs
One file per DAG lane, versioned so every agent launch is reproducible.
Common contract for ALL lanes: read unity/CHECKLIST.md (your lane's items +
the award bar) and unity/COMPILE-RISKS.md (conventions; EXTEND it for every
uncertain API). Ship ONLY new files + `partial class` extensions — never edit
shared files; wrap your feature in a `RUNWAY_FX_<NAME>` define kill-switch;
expose one static Apply/Install entry point; verify with the tool
`bash tools/unity_compile.sh` (must stay clean) plus your lane's own
screenshot/film evidence saved to the scratchpad; no git; never touch game/
or game/.env; report files+lines, evidence paths, ledger additions, and the
exact one-line hookups you need me to make at integration.
