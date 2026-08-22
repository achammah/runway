#if !RUNWAY_FX_USHOTS_OFF
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Runway.App
{
    /// <summary>
    /// THE HARNESS'S FINGER — how the twin camera presses the buttons the shipped
    /// screens keep to themselves.
    ///
    /// The Godot harnesses drive the screens through their OWN private members:
    /// `t._show_menu()`, `ht._btn.pressed.emit()`, `d._show_trait_tip("credibility")`,
    /// `b.set("_tab", i)`. GDScript lets a harness do that; C# does not, and the
    /// parity lane may not edit a shipped file to add a seam. So the same calls are
    /// made by reflection — the identical entry points, reached the only way a new
    /// file can reach them.
    ///
    /// NOTHING HERE IS SILENT. A member that has been renamed logs a POKE MISS with
    /// the type and the member, and the run's summary lists every one, because a
    /// harness that quietly photographs the wrong state is worse than one that fails.
    /// </summary>
    public static class UnityShotsPoke
    {
        const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static
                                 | BindingFlags.Public | BindingFlags.NonPublic
                                 | BindingFlags.DeclaredOnly;

        /// Every member this run could not reach, in the order it was asked for.
        public static readonly List<string> Misses = new List<string>();

        // ── methods ────────────────────────────────────────────────────────────

        /// Call a method by name, public or private, matching on argument count.
        public static object Call(object target, string method, params object[] args)
        {
            if (target == null) return Miss(method, "the target is null");
            int argc = args != null ? args.Length : 0;
            MethodInfo m = FindMethod(target.GetType(), method, argc);
            if (m == null)
                return Miss(method, target.GetType().Name + " has no " + method
                                    + "(" + argc + " args)");
            try
            {
                return m.Invoke(m.IsStatic ? null : target, args);
            }
            catch (TargetInvocationException e)
            {
                return Miss(method, "threw "
                    + (e.InnerException != null ? e.InnerException.Message : e.Message));
            }
            catch (Exception e)
            {
                return Miss(method, "threw " + e.Message);
            }
        }

        static MethodInfo FindMethod(Type t, string name, int argc)
        {
            while (t != null)
            {
                MethodInfo[] all = t.GetMethods(Any);
                for (int i = 0; i < all.Length; i++)
                    if (all[i].Name == name && all[i].GetParameters().Length == argc)
                        return all[i];
                t = t.BaseType;
            }
            return null;
        }

        // ── fields ─────────────────────────────────────────────────────────────

        /// Write a field by name, public or private (the twin of `b.set("_tab", i)`).
        public static bool SetField(object target, string field, object value)
        {
            if (target == null) { Miss(field, "the target is null"); return false; }
            FieldInfo f = FindField(target.GetType(), field);
            if (f == null)
            {
                Miss(field, target.GetType().Name + " has no field " + field);
                return false;
            }
            try
            {
                f.SetValue(f.IsStatic ? null : target, value);
                return true;
            }
            catch (Exception e)
            {
                Miss(field, "would not take " + value + " (" + e.Message + ")");
                return false;
            }
        }

        public static object GetField(object target, string field)
        {
            if (target == null) return Miss(field, "the target is null");
            FieldInfo f = FindField(target.GetType(), field);
            if (f == null) return Miss(field, target.GetType().Name + " has no field " + field);
            try { return f.GetValue(f.IsStatic ? null : target); }
            catch (Exception e) { return Miss(field, "would not read (" + e.Message + ")"); }
        }

        static FieldInfo FindField(Type t, string name)
        {
            while (t != null)
            {
                FieldInfo f = t.GetField(name, Any);
                if (f != null) return f;
                t = t.BaseType;
            }
            return null;
        }

        // ── the bookkeeping ────────────────────────────────────────────────────

        static object Miss(string member, string why)
        {
            string line = member + " — " + why;
            Misses.Add(line);
            Debug.LogError("USHOTS POKE MISS: " + line);
            return null;
        }
    }
}
#endif
