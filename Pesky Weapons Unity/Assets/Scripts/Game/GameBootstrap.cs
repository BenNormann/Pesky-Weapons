using UnityEngine;
using UnityEngine.SceneManagement;

namespace Pesky.Game
{
    /// <summary>
    /// The only thing in Boot.unity (build index 0): it loads the gameplay scene. Everything else about the
    /// game lives in Zone1, so the bootstrap stays the one place a launcher or a lobby would hook into.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameBootstrap : MonoBehaviour
    {
        [Tooltip("Scene to load. Must be in the build settings.")]
        [SerializeField] string sceneName = "Zone1";
        [Tooltip("Load it automatically on Start. Off = something else calls Load().")]
        [SerializeField] bool loadOnStart = true;

        public string SceneName { get { return sceneName; } }

        void Start()
        {
            if (loadOnStart) Load();
        }

        public void Load()
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogError("GameBootstrap has no scene name.", this);
                return;
            }
            if (SceneManager.GetActiveScene().name == sceneName) return;
            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        }
    }
}
