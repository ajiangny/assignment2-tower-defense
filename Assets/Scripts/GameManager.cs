using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace TowerDefense
{
    /// <summary>Tracks game state. An enemy finishing the path ends the game; R restarts.</summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public bool IsGameOver { get; private set; }
        public int Kills { get; private set; }

        private GUIStyle labelStyle;

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
            if (IsGameOver && keyboard != null && keyboard.rKey.wasPressedThisFrame)
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void RegisterKill() => Kills++;

        public void GameOver()
        {
            if (IsGameOver) return;
            IsGameOver = true;
            Time.timeScale = 0f;
        }

        // Minimal placeholder UI for the skeleton.
        private void OnGUI()
        {
            if (labelStyle == null)
            {
                labelStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 36 };
                labelStyle.normal.textColor = Color.white;
            }

            GUI.Label(new Rect(16, 8, 300, 40), $"Destroyed: {Kills}");

            if (IsGameOver)
            {
                GUI.Label(new Rect(0, 0, Screen.width, Screen.height),
                    $"GAME OVER\nEnemies destroyed: {Kills}\nPress R to restart", labelStyle);
            }
        }
    }
}
