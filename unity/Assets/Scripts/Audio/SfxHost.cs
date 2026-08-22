using UnityEngine;

namespace Runway.Audio
{
    /// <summary>
    /// The cue pool's body — one GameObject that carries the seven AudioSources
    /// <see cref="Sfx"/> plays through and pumps the one coroutine per cue that reads
    /// it off disk.
    ///
    /// It has no Update, no state and no public surface: everything about the sound
    /// lives in `Sfx`, and this exists because a coroutine needs a MonoBehaviour and
    /// an AudioSource needs a GameObject. It is created by `Sfx.Install()`, marked
    /// DontDestroyOnLoad in play mode so a cue outlives the screen that fired it, and
    /// `HideFlags.DontSave` so it never dirties a scene.
    ///
    /// It is its own file because this project's rule is one MonoBehaviour per file,
    /// named after the file — Unity will not attach a component whose class name does
    /// not match its file name.
    /// </summary>
    public sealed class SfxHost : MonoBehaviour
    {
    }
}
