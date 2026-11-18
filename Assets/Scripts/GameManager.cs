using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace TowerDefense
{
    public enum GameResult { Playing, Lost, Won }

    /// <summary>Tracks the outcome. An enemy finishing the path ends the game; R restarts afterwards.</summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public GameResult Result { get; private set; } = GameResult.Playing;
        public bool IsFinished => Result != GameResult.Playing;
        public int Kills { get; private set; }
        public bool FastForward { get; private set; }

        private void Awake()
        {
            Instance = this;
            Time.timeScale = 1f;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (IsFinished && keyboard != null && keyboard.rKey.wasPressedThisFrame) Restart();
        }

        public void Restart() => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);

        public void RegisterKill() => Kills++;

        public void ToggleFastForward()
        {
            if (IsFinished) return;
            FastForward = !FastForward;
            Time.timeScale = FastForward ? 2f : 1f;
        }

        /// <summary>Display name of the zombie that reached the gate, for the end screen.</summary>
        public string LeakedBy { get; private set; }

        public void GameOver(string leakedBy = null)
        {
            if (!IsFinished) LeakedBy = leakedBy;
            Finish(GameResult.Lost);
        }

        public void Victory() => Finish(GameResult.Won);

        private void Finish(GameResult result)
        {
            if (IsFinished) return;
            Result = result;
            Time.timeScale = 0f;
        }
    }
}
