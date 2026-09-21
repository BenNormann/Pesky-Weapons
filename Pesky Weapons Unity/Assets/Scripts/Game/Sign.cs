using TMPro;
using UnityEngine;

namespace Pesky.Game
{
    /// <summary>
    ///     /// A grey-box tutorial sign: a board plus world-space TextMeshPro text that wraps and auto-sizes to
        /// fit inside the board. The material culls back faces and z-tests normally, so the text is invisible
    /// from behind and never draws through walls. Authored in the Inspector; nothing reads it at runtime.
    /// </summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public sealed class Sign : MonoBehaviour, ISceneId
    {
        [SerializeField] int id;
        [TextArea(1, 4)]
        [SerializeField] string text = "SIGN";
        [Tooltip("World-space TextMeshPro laid out inside the board with a margin: it wraps and auto-sizes.")]
        [SerializeField] TextMeshPro label;

        public int SceneId { get { return id; } }
        public string Text { get { return text; } }

        void OnEnable() { Apply(); }
        void OnValidate() { Apply(); }

        void Apply()
        {
            if (label != null && label.text != text) label.text = text;
        }
    }
}
