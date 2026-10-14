using UnityEngine;
using TMPro;

namespace ArtWar
{
    /// <summary>
    /// A fixed label in both languages. Edit the two texts in the Inspector; the label shows the one for the chosen
    /// language when it appears.
    /// </summary>
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class LocalizedText : MonoBehaviour
    {
        [TextArea] public string vietnamese;
        [TextArea] public string english;

        void Awake() => Apply();

        public void Set(string vi, string en)
        {
            vietnamese = vi;
            english = en;
            Apply();
        }

        public void Apply() => GetComponent<TMP_Text>().text = Loc.T(vietnamese, english);
    }
}
