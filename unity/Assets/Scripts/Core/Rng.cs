using System;
using System.Globalization;
using System.Text;

namespace Runway.Core
{
    /// <summary>
    /// THE SALTED STREAMS — the C# twin of SimEngine._rng / WorldGen.build.
    ///
    /// Godot seeds a RandomNumberGenerator with hash(str(seed) + ":" + str(week)
    /// + ":" + str(salt)). Godot's hash() and PCG32 are engine internals we cannot
    /// reproduce byte-for-byte outside Godot, so this port keeps the STRUCTURE
    /// exactly — one independent stream per (seed, week, salt), built from the very
    /// same key strings — and swaps the two primitives for portable ones:
    ///   key string -> 64-bit FNV-1a  ->  xorshift64* state
    ///
    /// Consequence, stated plainly: a given (seed, week, salt) draws DIFFERENT
    /// numbers here than it does in Godot. Every determinism property the game
    /// relies on still holds — same state in, same numbers out, every run, on
    /// every machine, and each subsystem stays statistically independent of the
    /// others. Two builds of the same run will diverge in flavour, not in law.
    /// </summary>
    public sealed class Rng
    {
        private ulong _s;

        public Rng(ulong seed)
        {
            // xorshift dies on a zero state; the golden ratio constant is the standard escape.
            _s = seed == 0UL ? 0x9E3779B97F4A7C15UL : seed;
        }

        /// <summary>Stable 64-bit FNV-1a over the UTF-8 bytes of the key.</summary>
        public static ulong Fnv1a64(string key)
        {
            const ulong offset = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            ulong h = offset;
            byte[] bytes = Encoding.UTF8.GetBytes(key ?? string.Empty);
            for (int i = 0; i < bytes.Length; i++)
            {
                h ^= bytes[i];
                h *= prime;
            }
            return h;
        }

        public static Rng FromKey(string key)
        {
            return new Rng(Fnv1a64(key));
        }

        /// <summary>The engine's per-subsystem stream: "seed:week:salt", exactly as GDScript builds it.</summary>
        public static Rng Salted(long simSeed, int week, int salt)
        {
            return FromKey(simSeed.ToString(CultureInfo.InvariantCulture) + ":"
                + week.ToString(CultureInfo.InvariantCulture) + ":"
                + salt.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>The world bible's one-shot stream: "seed:world".</summary>
        public static Rng World(long simSeed)
        {
            return FromKey(simSeed.ToString(CultureInfo.InvariantCulture) + ":world");
        }

        private ulong Next()
        {
            ulong x = _s;
            x ^= x >> 12;
            x ^= x << 25;
            x ^= x >> 27;
            _s = x;
            return x * 2685821657736338717UL;
        }

        /// <summary>Godot's randi(): a random 32-bit unsigned integer.</summary>
        public uint Randi()
        {
            return (uint)(Next() >> 32);
        }

        /// <summary>Godot's randf(): a double in [0, 1).</summary>
        public double Randf()
        {
            // 53 significant bits, the widest a double holds exactly.
            return (Next() >> 11) * (1.0 / 9007199254740992.0);
        }

        /// <summary>Godot's randf_range(from, to).</summary>
        public double RandfRange(double from, double to)
        {
            return from + (to - from) * Randf();
        }

        /// <summary>Godot's randi_range(from, to) — INCLUSIVE at both ends.</summary>
        public int RandiRange(int from, int to)
        {
            if (to < from)
            {
                int swap = from;
                from = to;
                to = swap;
            }
            ulong span = (ulong)((long)to - (long)from + 1L);
            return (int)((long)from + (long)(Randi() % (uint)span));
        }
    }

    /// <summary>
    /// The GDScript math/string primitives the engine leans on, ported with their
    /// Godot semantics (which are NOT the .NET defaults — round() goes away from
    /// zero, int() truncates toward zero, snapped() floors at the half step).
    /// </summary>
    public static class Gd
    {
        public static double Clampf(double v, double lo, double hi)
        {
            return v < lo ? lo : (v > hi ? hi : v);
        }

        public static int Clampi(int v, int lo, int hi)
        {
            return v < lo ? lo : (v > hi ? hi : v);
        }

        public static double Minf(double a, double b) { return a < b ? a : b; }
        public static double Maxf(double a, double b) { return a > b ? a : b; }
        public static int Mini(int a, int b) { return a < b ? a : b; }
        public static int Maxi(int a, int b) { return a > b ? a : b; }
        public static int Absi(int a) { return a < 0 ? -a : a; }
        public static double Absf(double a) { return a < 0.0 ? -a : a; }

        /// <summary>Godot round(): halfway cases go AWAY from zero (.NET rounds to even).</summary>
        public static double Round(double v)
        {
            return Math.Round(v, MidpointRounding.AwayFromZero);
        }

        /// <summary>int(round(x)) — the engine's most common conversion.</summary>
        public static int RoundToInt(double v)
        {
            return (int)Round(v);
        }

        /// <summary>Godot int(x) on a float: truncation toward zero.</summary>
        public static int ToInt(double v)
        {
            return (int)v;
        }

        /// <summary>Godot snappedf(value, step) = floor(value / step + 0.5) * step.</summary>
        public static double Snappedf(double v, double step)
        {
            if (step == 0.0)
            {
                return v;
            }
            return Math.Floor(v / step + 0.5) * step;
        }

        /// <summary>Godot String.left(n): the first n characters (or the whole string).</summary>
        public static string Left(string s, int n)
        {
            if (string.IsNullOrEmpty(s) || n <= 0)
            {
                return string.Empty;
            }
            return s.Length <= n ? s : s.Substring(0, n);
        }

        /// <summary>
        /// Godot String.capitalize(): underscores become spaces, camelCase gains a
        /// space, everything lowercases, then each word's first letter goes up.
        /// </summary>
        public static string Capitalize(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return string.Empty;
            }
            var spaced = new StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '_')
                {
                    spaced.Append(' ');
                    continue;
                }
                if (i > 0 && char.IsUpper(c) && !char.IsUpper(s[i - 1]) && s[i - 1] != ' ')
                {
                    spaced.Append(' ');
                }
                spaced.Append(char.ToLowerInvariant(c));
            }
            var outp = new StringBuilder();
            bool atWordStart = true;
            string flat = spaced.ToString();
            for (int i = 0; i < flat.Length; i++)
            {
                char c = flat[i];
                if (c == ' ')
                {
                    atWordStart = true;
                    outp.Append(c);
                    continue;
                }
                outp.Append(atWordStart ? char.ToUpperInvariant(c) : c);
                atWordStart = false;
            }
            return outp.ToString();
        }

        /// <summary>Invariant "%.Nf" so a French locale can never move a decimal point.</summary>
        public static string F(double v, int decimals)
        {
            return v.ToString("F" + decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        }
    }
}
