using System.Collections;
using UnityEngine;

namespace KeystoneKapers
{
    public enum GameState { Title, Playing, Paused, RoundWon, LifeLost, GameOver }

    /// <summary>
    /// Estado da partida: cronômetro, pontuação, vidas de reserva, captura, perda de vida, pausa e Game Over.
    /// Estados explícitos impedem que captura, perda de vida e pausa se sobreponham.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public GameConfig config;
        public DifficultySettings difficulty;
        public PoliceController police;
        public FugitiveAI fugitive;
        public ObstacleSpawner spawner;
        public UIManager ui;
        public CameraFollow cameraFollow;

        [Header("Pontos de início")]
        public Transform policeSpawn;
        public Transform fugitiveSpawn;
        public float fugitiveStartFacing = -1f;

        public GameState State { get; private set; } = GameState.Title;
        public int Score { get; private set; }
        public int Round { get; private set; } = 1;
        public int Reserves { get; private set; }
        public float TimeLeft { get; private set; }
        /// <summary>Tempo total da rodada atual (ajustado ao percurso de fuga do fugitivo).</summary>
        public float RoundTimeLimit { get; private set; }
        /// <summary>Tempo estimado para o fugitivo ir do início até a saída do terraço sem ser incomodado.</summary>
        public float EscapeTimeEstimate { get; private set; }
        public RoundDifficulty CurrentDifficulty { get; private set; }

        int nextExtraLife;
        GameState stateBeforePause;

        void Awake()
        {
            Instance = this;
            Time.timeScale = 1f;
        }

        void Start()
        {
            fugitive.OnEscaped = () => LoseLife("O FUGITIVO ESCAPOU!");
            PrepareAttempt();
            police.SetControlEnabled(false);
            fugitive.Stop(FugitiveAI.Mode.Idle);
            spawner.SetRunning(false);
            SetState(GameState.Title);
        }

        void SetState(GameState s)
        {
            State = s;
            if (ui) ui.OnStateChanged(s);
        }

        // ---------- Fluxo ----------
        public void StartNewGame()
        {
            StopAllCoroutines();
            Time.timeScale = 1f;
            Score = 0;
            Round = 1;
            Reserves = config.startingReserveLives;
            nextExtraLife = config.extraLifeEvery;
            BeginAttempt();
        }

        void PrepareAttempt()
        {
            CurrentDifficulty = difficulty.Get(Round);
            var layout = LevelLayout.Instance;
            if (layout.elevator) layout.elevator.ResetElevator();
            police.ResetTo(policeSpawn.position, 1f);
            int ff = layout.GetFloorIndex(fugitiveSpawn.position.y);
            fugitive.ResetTo(ff, fugitiveSpawn.position.x, fugitiveStartFacing);
            // Velocidade fixa em todas as rodadas, sempre abaixo da do policial (travada em FugitiveAI).
            fugitive.Speed = config.fugitiveSpeed;
            EscapeTimeEstimate = EstimateEscapeTime(fugitive.Speed);
            RoundTimeLimit = config.autoAdjustRoundTime
                ? Mathf.Max(config.roundTime, Mathf.Ceil(EscapeTimeEstimate + config.escapeTimeMargin))
                : config.roundTime;
            TimeLeft = RoundTimeLimit;
            if (cameraFollow) cameraFollow.SnapToTarget();
        }

        void BeginAttempt()
        {
            PrepareAttempt();
            spawner.BeginAttempt(Round, CurrentDifficulty);
            police.SetControlEnabled(true);
            fugitive.StartRunning();
            SetState(GameState.Playing);
            if (ui) ui.ShowMessage("RODADA " + Round, 1.5f);
        }

        void Update()
        {
            var input = GameInput.Instance;
            if (State == GameState.Playing && input && input.PausePressed) { Pause(); return; }
            if (State == GameState.Paused && input && input.PausePressed) { Resume(); return; }
            if (State != GameState.Playing) return;

            TimeLeft -= Time.deltaTime;
            if (TimeLeft <= 0f) { TimeLeft = 0f; LoseLife("TEMPO ESGOTADO!"); return; }

            CheckObstacles();
            if (State != GameState.Playing) return;
            CheckCollectibles();
            CheckCapture();
        }

        void CheckObstacles()
        {
            if (!police.CanBeHit) return;
            var pb = police.HitBounds;
            foreach (var o in spawner.Active)
            {
                if (!o || !o.Active || o.Floor != police.CurrentFloor) continue;
                if (!o.Overlaps(pb)) continue;
                if (o.type == ObstacleType.Plane)
                {
                    police.ApplyHit(0f);
                    LoseLife("ATINGIDO PELO AVIÃO!");
                }
                else
                {
                    TimeLeft = Mathf.Max(0f, TimeLeft - config.obstacleTimePenalty);
                    police.ApplyHit();
                    if (ui) ui.ShowPenalty("-" + config.obstacleTimePenalty.ToString("0") + "s");
                }
                return;
            }
        }

        void CheckCollectibles()
        {
            if (police.IsTransporting) return;
            var pb = police.HitBounds;
            foreach (var c in spawner.Collectibles)
            {
                if (!c || !c.Active || c.Floor != police.CurrentFloor) continue;
                if (!c.Overlaps(pb)) continue;
                c.Collect();
                GameAudio.PlayItemPickup();
                AddScore(config.collectiblePoints);
                if (ui) ui.ShowPenalty("+" + config.collectiblePoints);
            }
        }

        void CheckCapture()
        {
            if (police.IsTransporting || fugitive.IsTransporting) return;
            if (fugitive.CurrentMode != FugitiveAI.Mode.Running) return;
            if (police.CurrentFloor != fugitive.CurrentFloor) return;
            Vector3 a = police.transform.position, b = fugitive.transform.position;
            if (Mathf.Abs(a.y - b.y) > 2f) return;
            if (Mathf.Abs(a.x - b.x) > config.captureDistance) return;
            RoundWon();
        }

        void RoundWon()
        {
            if (State != GameState.Playing) return;
            int secs = Mathf.CeilToInt(TimeLeft);
            int points = secs * config.GetScoreMultiplier(Round);
            SetState(GameState.RoundWon);
            FreezeActors();
            fugitive.Stop(FugitiveAI.Mode.Captured);
            AddScore(points);
            if (ui) ui.ShowMessage("CAPTURADO!\n+" + points + " pontos", config.roundEndDelay);
            StartCoroutine(NextRoundAfterDelay());
        }

        IEnumerator NextRoundAfterDelay()
        {
            yield return new WaitForSeconds(config.roundEndDelay);
            Round++;
            BeginAttempt();
        }

        public void LoseLife(string reason)
        {
            if (State != GameState.Playing) return;
            SetState(GameState.LifeLost);
            FreezeActors();
            if (ui) ui.ShowMessage(reason, config.lifeLostDelay);
            StartCoroutine(AfterLifeLost());
        }

        IEnumerator AfterLifeLost()
        {
            yield return new WaitForSeconds(config.lifeLostDelay);
            if (Reserves > 0)
            {
                Reserves--;
                BeginAttempt();
            }
            else
            {
                spawner.ClearAll();
                SetState(GameState.GameOver);
            }
        }

        void FreezeActors()
        {
            police.SetControlEnabled(false);
            if (fugitive.CurrentMode == FugitiveAI.Mode.Running || fugitive.CurrentMode == FugitiveAI.Mode.Riding)
                fugitive.Stop(FugitiveAI.Mode.Idle);
            spawner.SetRunning(false);
        }

        void AddScore(int pts)
        {
            Score += pts;
            while (Score >= nextExtraLife)
            {
                nextExtraLife += config.extraLifeEvery;
                if (Reserves < config.maxReserveLives)
                {
                    Reserves++;
                    if (ui) ui.ShowPenalty("VIDA EXTRA!");
                }
            }
        }

        // ---------- Pausa ----------
        public void Pause()
        {
            if (State != GameState.Playing) return;
            stateBeforePause = State;
            Time.timeScale = 0f;
            SetState(GameState.Paused);
        }

        public void Resume()
        {
            if (State != GameState.Paused) return;
            Time.timeScale = 1f;
            SetState(stateBeforePause);
        }

        public void Restart()
        {
            Time.timeScale = 1f;
            StartNewGame();
        }

        /// <summary>
        /// Percorre a rota do fugitivo (corredores + escadas de subida até o terraço + saída de fuga)
        /// usando as posições reais do cenário.
        /// </summary>
        public float EstimateEscapeTime(float runSpeed)
        {
            var L = LevelLayout.Instance;
            if (!L || runSpeed <= 0f) return 0f;
            float esc = config.FugitiveEscalatorSpeed;
            int floor = L.GetFloorIndex(fugitiveSpawn.position.y);
            float x = fugitiveSpawn.position.x;
            float t = 0f;
            int guard = 0;
            while (floor < L.TerraceIndex && guard++ < 10)
            {
                var up = L.GetUp(floor, true);
                if (!up) break;
                t += Mathf.Abs(up.EntryPosition.x - x) / runSpeed;
                t += up.PathLength / esc;
                x = up.ExitPosition.x;
                floor = up.toFloor;
            }
            float escapeX = L.escapePoint ? L.escapePoint.position.x : L.floors[L.TerraceIndex].minX;
            t += Mathf.Abs(escapeX - x) / runSpeed;
            return t;
        }

        // ---------- Utilidades de teste ----------
        public void DebugSetTime(float t) => TimeLeft = t;
        public void DebugSetRound(int r) { Round = Mathf.Max(1, r); }
    }
}
