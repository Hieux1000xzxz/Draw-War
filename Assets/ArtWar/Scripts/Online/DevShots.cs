#if DEVELOPMENT_BUILD && !UNITY_EDITOR
using UnityEngine;

namespace ArtWar
{
    /// <summary>
    /// Development builds only: "-shots C:\path\shot.png" saves a screenshot of the game every few seconds, for
    /// checking a second copy of the game (an online guest) while testing.
    /// </summary>
    public class DevShots : MonoBehaviour
    {
        string path;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Start()
        {
            var args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, "-shots");
            if (i < 0 || i + 1 >= args.Length) return;
            var go = new GameObject("Dev Shots");
            DontDestroyOnLoad(go);
            go.AddComponent<DevShots>().path = args[i + 1];
        }

        float next = 3f;

        void Update()
        {
            // real time: the menus stop game time
            if (Time.realtimeSinceStartup < next) return;
            next = Time.realtimeSinceStartup + 3f;
            Shoot();
        }

        void Shoot() => ScreenCapture.CaptureScreenshot(path);
    }
}
#endif
