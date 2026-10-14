using UnityEngine;

namespace ArtWar
{
    public enum Language { Vietnamese, English }

    /// <summary>
    /// The game's two languages. Every piece of text on screen is written as a pair, <c>Loc.T("tiếng Việt", "English")</c>,
    /// and shows in the chosen language. The choice is saved; the UI is built in it when a scene loads.
    /// </summary>
    public static class Loc
    {
        const string Key = "artwar.language";
        static bool loaded;
        static Language current;

        public static Language Current
        {
            get
            {
                if (!loaded)
                {
                    current = (Language)PlayerPrefs.GetInt(Key, (int)Language.English);
                    loaded = true;
                }
                return current;
            }
            set
            {
                current = value;
                loaded = true;
                PlayerPrefs.SetInt(Key, (int)value);
                PlayerPrefs.Save();
            }
        }

        public static bool English => Current == Language.English;

        /// <summary>The text in the current language.</summary>
        public static string T(string vi, string en) => English ? en : vi;
    }
}
