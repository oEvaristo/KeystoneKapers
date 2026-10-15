using UnityEngine;
using UnityEngine.UI;

namespace KeystoneKapers
{
    /// <summary>HUD (pontuação, cronômetro, rodada, reservas) e telas de início, pausa e Game Over.</summary>
    public class UIManager : MonoBehaviour
    {
        public GameManager game;
        public GameConfig config;

        [Header("HUD")]
        public GameObject hudRoot;
        public Text scoreText;
        public Text timerText;
        public Text roundText;
        public Text reservesLabel;
        public Image[] reserveIcons;
        public Text messageText;
        public Text penaltyText;
        public Color timerNormal = Color.white;
        public Color timerWarning = new Color(1f, 0.25f, 0.2f);

        [Header("Telas")]
        public GameObject titlePanel;
        public Button playButton;
        public GameObject pausePanel;
        public Button continueButton;
        public Button restartButton;
        public GameObject gameOverPanel;
        public Text gameOverScoreText;
        public Button playAgainButton;

        float messageTimer, penaltyTimer;

        void Awake()
        {
            if (playButton) playButton.onClick.AddListener(() => game.StartNewGame());
            if (continueButton) continueButton.onClick.AddListener(() => game.Resume());
            if (restartButton) restartButton.onClick.AddListener(() => game.Restart());
            if (playAgainButton) playAgainButton.onClick.AddListener(() => game.StartNewGame());
            if (messageText) messageText.text = "";
            if (penaltyText) penaltyText.text = "";
        }

        public void OnStateChanged(GameState s)
        {
            if (titlePanel) titlePanel.SetActive(s == GameState.Title);
            if (pausePanel) pausePanel.SetActive(s == GameState.Paused);
            if (gameOverPanel) gameOverPanel.SetActive(s == GameState.GameOver);
            if (hudRoot) hudRoot.SetActive(s != GameState.Title);
            if (s == GameState.GameOver && gameOverScoreText) gameOverScoreText.text = "PONTUAÇÃO FINAL\n" + game.Score.ToString("N0");
            if (s == GameState.GameOver || s == GameState.Title) { messageText.text = ""; penaltyText.text = ""; }

            var sel = s == GameState.Title ? playButton : s == GameState.Paused ? continueButton : s == GameState.GameOver ? playAgainButton : null;
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es) es.SetSelectedGameObject(sel ? sel.gameObject : null);
        }

        public void ShowMessage(string msg, float duration)
        {
            if (!messageText) return;
            messageText.text = msg;
            messageTimer = duration;
        }

        public void ShowPenalty(string msg)
        {
            if (!penaltyText) return;
            penaltyText.text = msg;
            penaltyTimer = 1.2f;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (messageTimer > 0f) { messageTimer -= dt; if (messageTimer <= 0f) messageText.text = ""; }
            if (penaltyTimer > 0f)
            {
                penaltyTimer -= dt;
                if (penaltyText) penaltyText.rectTransform.anchoredPosition = new Vector2(0f, 60f + (1.2f - penaltyTimer) * 40f);
                if (penaltyTimer <= 0f) penaltyText.text = "";
            }

            if (!game) return;
            if (scoreText) scoreText.text = "PONTOS  " + game.Score.ToString("000000");
            if (roundText) roundText.text = "RODADA  " + game.Round;
            if (timerText)
            {
                float t = game.TimeLeft;
                timerText.text = Mathf.CeilToInt(t).ToString("00");
                bool warn = t <= config.lowTimeWarning && game.State == GameState.Playing;
                if (warn)
                {
                    float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 12f);
                    timerText.color = Color.Lerp(timerWarning, Color.white, pulse * 0.35f);
                    timerText.rectTransform.localScale = Vector3.one * (1.1f + 0.15f * pulse);
                }
                else
                {
                    timerText.color = t <= config.lowTimeWarning ? timerWarning : timerNormal;
                    timerText.rectTransform.localScale = Vector3.one;
                }
            }
            if (reserveIcons != null)
                for (int i = 0; i < reserveIcons.Length; i++)
                    if (reserveIcons[i]) reserveIcons[i].enabled = i < game.Reserves;
        }
    }
}
