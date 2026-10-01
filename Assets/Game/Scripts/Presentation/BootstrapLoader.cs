using UnityEngine;
using UnityEngine.SceneManagement;

namespace BorrowedHex.Presentation
{
    /// <summary>
    /// Lives alone in Bootstrap.unity (build index 0). Its only job is to hand off to the
    /// Arena scene, keeping the first-loaded scene tiny so the Web build shows a frame early.
    /// </summary>
    public sealed class BootstrapLoader : MonoBehaviour
    {
        public string arenaSceneName = "Arena";

        void Start()
        {
            Application.targetFrameRate = 60;
            SceneManager.LoadScene(arenaSceneName, LoadSceneMode.Single);
        }
    }
}
