using UnityEngine;

namespace Runway.App
{
    /// <summary>
    /// THE SEAM BETWEEN THE FLOW AND THE RUN. Boot owns the ORDER of main.gd — studio
    /// card, keys gate, title, how-to, draft, birth, book, garage — and owns none of
    /// the run itself: the seed, the state, the record, WorldGen, SimEngine and the
    /// saves all live behind this interface, implemented by the lane that owns
    /// Runway.Core and the garage.
    ///
    /// Register once at boot:
    ///     Boot.Instance.Driver = new RunDriver();
    /// or, before Boot exists:
    ///     Boot.PendingDriver = new RunDriver();
    ///
    /// Every method maps to a named beat in main.gd, quoted in its comment. Nothing
    /// here returns a Core type on purpose: the App and LLM layers never see one.
    /// </summary>
    public interface IRunDriver
    {
        /// SaveSystem.list_slots() — one row per slot for the title screen.
        SaveSlotInfo[] ListSlots();

        /// SaveSystem.active_slot = slot
        void SetActiveSlot(int slot);

        /// SaveSystem.clear_run() — NEW GAME on an occupied slot overwrites it.
        void ClearRun();

        /// SaveSystem.has_run() for the ACTIVE slot.
        bool HasSavedRun();

        /// _start_run's resume half: load the slot, rebuild rng, and be ready for the
        /// garage. Returns false when the save would not load, so the flow falls
        /// through to a fresh run exactly as main.gd does.
        bool ResumeSavedRun();

        /// _start_run's fresh half: seed (or the daily seed), new GameState, new
        /// RunRecord, clear the generated pool.
        void BeginFreshRun(bool daily);

        /// _after_draft, the engine half: archetype, funding, cofounders, cap table,
        /// bag, competence coverage, the traps, the record entry, WorldGen.build,
        /// generate_arcs. The screen half (birth, book, garage) stays with Boot.
        void ApplyDraft(object draftResult);

        /// The bag-page prefetch: generate_world for the pitch as typed so far.
        /// key is company_name + "|" + company_idea, so a re-entry with the same
        /// pitch does not pay twice.
        void PrefetchWorld(string companyName, string companyIdea,
                           string bizWhat, string bizWho);

        /// The birth screen's own guard: "the bag-page prefetch missed (edited name at
        /// the last second, or direct entry) — start it now". The driver holds the key,
        /// so a prefetch that already covers this pitch is not paid for twice.
        void EnsureWorldgen();

        /// True while the bible is still being written — the birth screen's 25s gate.
        bool WorldgenInFlight { get; }

        /// True once a bible has landed (empty means the deterministic skeleton stands).
        bool WorldgenLanded { get; }

        /// _finish_worldgen: apply_llm_world + seed_beliefs, then the founding prefetch.
        /// Returns the founding narration if it has ALREADY landed, else "" — the book
        /// opens on the founder's own entry when it can, and on its placeholder when
        /// it cannot.
        string FinishWorldgen();
        bool FoundingInFlight { get; }
        bool FoundingReady { get; }
        /// live narration if it landed; the engine's authored day one if not
        string AdoptAuthoredFounding();

        /// _cold_open: the curtain drops, day one is written, the beat reads it while
        /// the first image of THIS company renders behind it.
        void ColdOpen();

        /// The company name shown on the birth/book screens before the bible lands.
        string CompanyName { get; }
    }

    /// One row of SaveSystem.list_slots(), as the title screen needs it.
    public struct SaveSlotInfo
    {
        public int Slot;
        public bool Exists;
        public string Company;
        public string Founder;
        public int Week;
        public long Timestamp;   // unix seconds
    }

    /// <summary>
    /// The stand-in until the run lane lands: every beat logs and returns the answer
    /// that keeps the flow walkable (no saves, no bible, no day one). It is never
    /// silent — a flow running on this is a flow with no game behind it, and that has
    /// to be visible in the log rather than look like a working boot.
    /// </summary>
    public sealed class NullRunDriver : IRunDriver
    {
        int _slot = 1;

        public SaveSlotInfo[] ListSlots()
        {
            var rows = new SaveSlotInfo[SaveSlots.SlotCount];
            for (int i = 0; i < rows.Length; i++) rows[i] = SaveSlots.Read(i + 1);
            return rows;
        }

        public void SetActiveSlot(int slot) { _slot = slot; SaveSlots.ActiveSlot = slot; }

        public void ClearRun() { SaveSlots.Clear(_slot); }

        public bool HasSavedRun() { return SaveSlots.Read(_slot).Exists; }

        public bool ResumeSavedRun()
        {
            Debug.LogWarning("RUNWAY! no run driver registered — cannot resume slot " + _slot + ".");
            return false;
        }

        public void BeginFreshRun(bool daily)
        {
            Debug.LogWarning("RUNWAY! no run driver registered — BeginFreshRun(daily=" + daily
                             + ") did nothing. Register one with Boot.Instance.Driver.");
        }

        public void ApplyDraft(object draftResult)
        {
            Debug.LogWarning("RUNWAY! no run driver registered — the draft result was dropped.");
        }

        public void PrefetchWorld(string companyName, string companyIdea,
                                  string bizWhat, string bizWho)
        { }

        public void EnsureWorldgen() { }

        public bool WorldgenInFlight { get { return false; } }

        public bool WorldgenLanded { get { return false; } }

        public string FinishWorldgen() { return ""; }
        public bool FoundingInFlight { get { return false; } }
        public bool FoundingReady { get { return false; } }
        public string AdoptAuthoredFounding() { return ""; }

        public void ColdOpen() { }

        public string CompanyName { get { return ""; } }
    }
}
