using System;
using TMPro;
using UnityEngine;
using Runway.App;

namespace Runway.Game
{
    /// <summary>
    /// THE WEEK ARRIVES AS INK, NOT AS TEXT — the reading beat's letters, animated.
    ///
    /// `ReadingBeat` already writes its paragraphs in rather than printing them: it
    /// walks `maxVisibleCharacters` up over a few seconds, which is Godot's
    /// `visible_ratio` tween, ported. But a character that simply becomes visible
    /// POPS. A hand does not pop. A letter lands: it comes down the last two pixels
    /// onto the rule and darkens as it touches.
    ///
    /// So this layer takes the reveal off the clock and puts it on a pen:
    ///  - 40 characters a second, the pace of a hand writing something it means.
    ///  - Every character falls the last 2px onto its line and inks up as it lands,
    ///    over 0.14s, so five or six letters are always in the air at once.
    ///  - The words the week is JUDGED in get one 6% settle when they land, once.
    ///  - The die the week turned on is stamped into the sentence beside its number.
    ///  - One click lands everything, exactly as the beat's own skip already does.
    ///
    /// It is a VERTEX layer, not a second text object: the mesh TMP already built is
    /// re-written in place from a cached copy, so nothing is allocated per frame and
    /// the reveal costs one text regeneration a frame, which the beat pays already.
    ///
    /// KILL-SWITCH: `RUNWAY_FX_TEXT=0` in the environment (or in .env/keys.env) and
    /// `Apply` returns the caller's own timing having touched nothing at all — the
    /// beat then reads exactly as it does today.
    ///
    /// THE HOOKUP (one call, in `ReadingBeat.Reveal`, replacing the two lines that
    /// start the write-in — see the lane report for the exact text):
    ///
    ///     float secs = ReadingBeatText.Apply(b, Mathf.Clamp(body.Length / 95f, 0.3f, 6.5f));
    ///     StartCoroutine(WriteIn(b, secs));
    /// </summary>
    public static class ReadingBeatText
    {
        // ── the hand ───────────────────────────────────────────────────────────
        /// Characters a second. Slower than the beat's own 95: at 95 the page is
        /// TYPED, at 40 it is written, and the difference is the whole effect.
        public const float Cps = 40f;
        /// The original's own floor and ceiling on a block's write-in, kept: a long
        /// paragraph must never outlive the reading window the beat paces it into.
        public const float MinSecs = 0.3f;
        public const float MaxSecs = 6.5f;
        /// One character's landing: the fall and the ink, together.
        public const float SettleSecs = 0.14f;
        /// How far above its line a character starts. Two pixels reads as weight;
        /// four reads as a bug.
        public const float SettleDrop = 2f;
        /// The verdict's one-time punch, and how long it takes to come back down.
        public const float VerdictScale = 1.06f;
        public const float VerdictSecs = 0.22f;
        /// A jump bigger than this is not writing, it is a SKIP: the reader clicked,
        /// or the block was landed whole. Everything already past lands settled.
        public const int LeapChars = 8;

        // ── the die chit ───────────────────────────────────────────────────────
        /// The settled die is the LAST cell of its roll sheet (8x5 of 512, frame 39),
        /// and inside that cell the drawing sits in the same box on all twenty sheets:
        /// measured x 121..388, y 99..410, so this box is that box plus a pixel.
        internal const int DieSheetCols = 8;
        internal const int DieSheetCell = 512;
        internal const int DieSheetLastFrame = 39;
        internal const float DieCropX = 120f;
        internal const float DieCropY = 96f;
        internal const float DieCropW = 272f;
        internal const float DieCropH = 320f;
        /// Chit height as a share of the body size, and how far it sits under the
        /// baseline so it rests ON the rule rather than floating over it.
        internal const float DieHeightEm = 1.02f;
        internal const float DieSitEm = 0.08f;
        /// Clear space either side of the chit, so it reads as a stamp in the line
        /// rather than as a letter crowded by its neighbours.
        internal const float DieRoomEm = 0.55f;
        /// The gap the chit is dropped into is opened with non-breaking spaces, so a
        /// line can never wrap through the middle of "14 [die]".
        internal const char GapChar = ' ';
        internal const int GapMaxChars = 14;

        /// <summary>
        /// THE WORDS THE WEEK IS JUDGED IN.
        ///
        /// The four capitals are the band table transcribed from `main.gd` — the
        /// authored vocabulary of the verdict. The four sentences are what the beat
        /// actually says today: `TurnRunner` turns the band into plain words and
        /// never prints the capitals, so emphasising only the capitals would be an
        /// effect nobody would ever see. Both sets are here; whichever the sentence
        /// carries is the one that settles.
        ///
        /// Matching is case-SENSITIVE and boundary-anchored, longest first, so
        /// "It lands beautifully." never matches as "It lands." and "BRILLIANT"
        /// never matches inside a longer word.
        /// </summary>
        public static readonly string[] Verdicts =
        {
            "It half-lands: something gives.",
            "It lands beautifully.",
            "It goes wrong.",
            "IT BACKFIRES",
            "MIXED RESULT",
            "BRILLIANT",
            "It lands.",
            "IT LANDS",
        };

        // ── the switch ─────────────────────────────────────────────────────────

        static int _on = -1;

        /// `RUNWAY_FX_TEXT=0` turns the whole lane off. Absent or anything else is on.
        public static bool Enabled
        {
            get
            {
                if (_on < 0) _on = Env.Get("RUNWAY_FX_TEXT", "1").Trim() == "0" ? 0 : 1;
                return _on == 1;
            }
        }

        /// Re-read the switch (the keys screen reloads the environment under the game).
        public static void Reread() { _on = -1; }

        // ── the entry point ────────────────────────────────────────────────────

        /// Put the hand on this label. Returns how long the caller's own reveal
        /// should take so the two agree: the ink pace when the lane is on, and the
        /// caller's `fallbackSecs` untouched when it is off.
        ///
        /// SAFE TO CALL TWICE on the same label, and safe on a label that is already
        /// half-revealed: the reveal is restarted from the top either way.
        public static float Apply(TMP_Text t, float fallbackSecs)
        {
            if (t == null || !Enabled) return fallbackSecs;
            try
            {
                BeatInkSettle fx = t.GetComponent<BeatInkSettle>();
                if (fx == null) fx = t.gameObject.AddComponent<BeatInkSettle>();
                fx.Install(t);
                return Pace(t.text != null ? t.text.Length : 0);
            }
            catch (Exception e)
            {
                // A settling letter is never worth a broken beat.
                Debug.LogWarning("RUNWAY! beat text fx skipped: " + e.Message);
                return fallbackSecs;
            }
        }

        public static float Apply(TMP_Text t)
        {
            return Apply(t, Pace(t != null && t.text != null ? t.text.Length : 0));
        }

        /// How long `n` characters take at the hand's pace, inside the beat's own
        /// floor and ceiling.
        public static float Pace(int n)
        {
            return Mathf.Clamp(n / Cps, MinSecs, MaxSecs);
        }

        // ── the verdict scan ───────────────────────────────────────────────────

        /// The longest verdict that starts at `at`, boundary-anchored, or 0.
        /// `Read` hands back one character of the laid-out text at a time so the scan
        /// runs over what TMP actually parsed rather than over the raw string.
        public static int VerdictAt(Func<int, char> read, int count, int at)
        {
            if (read == null || at < 0 || at >= count) return 0;
            if (at > 0 && IsWordish(read(at - 1))) return 0;
            int best = 0;
            for (int v = 0; v < Verdicts.Length; v++)
            {
                string w = Verdicts[v];
                int n = w.Length;
                if (n <= best || at + n > count) continue;
                bool hit = true;
                for (int i = 0; i < n; i++)
                {
                    if (read(at + i) != w[i]) { hit = false; break; }
                }
                if (!hit) continue;
                if (at + n < count && IsWordish(read(at + n))) continue;
                best = n;
            }
            return best;
        }

        static bool IsWordish(char c)
        {
            return char.IsLetterOrDigit(c) || c == '\'' || c == '-';
        }

        // ── the die in the sentence ────────────────────────────────────────────

        /// "The die came up 14." — the number the cup settled on, and where the chit
        /// goes: past the number AND past the full stop that closes its clause, so the
        /// die is stamped after the statement instead of splitting it from its point.
        /// Returns 0 when this paragraph is not the judgement.
        public static int FindDie(string s, out int after)
        {
            after = -1;
            if (string.IsNullOrEmpty(s)) return 0;
            const string lead = "die came up ";
            int at = IndexOfIgnoreCase(s, lead);
            if (at < 0) return 0;
            int i = at + lead.Length;
            int n = 0, digits = 0;
            while (i < s.Length && s[i] >= '0' && s[i] <= '9' && digits < 3)
            {
                n = n * 10 + (s[i] - '0');
                i++; digits++;
            }
            if (digits == 0 || n < 1 || n > 20) return 0;
            if (i < s.Length && (s[i] == '.' || s[i] == ',' || s[i] == '!')) i++;
            if (i < s.Length && s[i] == ' ') i++;
            after = i;
            return n;
        }

        static int IndexOfIgnoreCase(string s, string needle)
        {
            int last = s.Length - needle.Length;
            for (int i = 0; i <= last; i++)
            {
                bool hit = true;
                for (int k = 0; k < needle.Length; k++)
                {
                    if (char.ToLowerInvariant(s[i + k]) != needle[k]) { hit = false; break; }
                }
                if (hit) return i;
            }
            return -1;
        }

        /// The art file for a settled roll, or "" when that sheet did not ship.
        public static string DieSheet(int roll)
        {
            if (roll < 1 || roll > 20) return "";
            string rel = string.Format("dice/roll_{0:00}.png", roll);
            return RunwayPaths.ArtExists(rel) ? rel : "";
        }

        /// The last frame's die, cropped out of its sheet, as a uvRect. Unity's UV
        /// origin is BOTTOM-left and the sheets are laid out top-left, so the row is
        /// flipped here — the same flip `SheetLoop.CoverRect` makes, and nowhere else.
        public static Rect DieUv(float texW, float texH)
        {
            if (texW < 1f || texH < 1f) return new Rect(0f, 0f, 1f, 1f);
            float sx = (DieSheetLastFrame % DieSheetCols) * DieSheetCell + DieCropX;
            float syTop = (DieSheetLastFrame / DieSheetCols) * DieSheetCell + DieCropY;
            return new Rect(sx / texW, 1f - (syTop + DieCropH) / texH,
                            DieCropW / texW, DieCropH / texH);
        }

        /// Hand the sheet over however this process can get it. In the game that is
        /// always the session cache — the cup played this very sheet a second ago, so
        /// it is already in hand. Without a `Boot` there is no coroutine pump to fetch
        /// with, so the file is read straight off disk; that path only ever runs in an
        /// editor harness, where a blocking read costs nobody a frame.
        public static void DieTexture(string rel, Action<Texture2D> cb)
        {
            if (cb == null) return;
            if (string.IsNullOrEmpty(rel)) { cb(null); return; }
            if (ArtCache.Known(rel)) { cb(ArtCache.Peek(rel)); return; }
            if (Boot.Instance != null) { ArtCache.Load(rel, cb); return; }
            cb(ReadPng(RunwayPaths.Art(rel)));
        }

        static Texture2D ReadPng(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            try
            {
                byte[] bytes = System.IO.File.ReadAllBytes(path);
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!tex.LoadImage(bytes)) return null;
                tex.wrapMode = TextureWrapMode.Clamp;
                return tex;
            }
            catch (Exception) { return null; }
        }
    }
}
