using UnityEngine;

namespace KeystoneKapers
{
    /// <summary>
    /// Parâmetros centrais do jogo (velocidades, tempos, alturas, pontuação, vidas).
    /// Edite o asset em Assets/Config/GameConfig.asset.
    /// </summary>
    [CreateAssetMenu(menuName = "Keystone Kapers/Game Config", fileName = "GameConfig")]
    public class GameConfig : ScriptableObject
    {
        [Header("Policial - Movimento")]
        public float runSpeed = 8f;
        public float airControl = 1f;
        public float gravity = 30f;
        [Tooltip("Altura máxima do salto em metros.")]
        public float jumpHeight = 1.6f;
        public float maxFallSpeed = 25f;
        public float turnSpeed = 1080f;

        [Header("Policial - Collider")]
        public float standHeight = 1.7f;
        public float crouchHeight = 1.0f;
        public float capsuleRadius = 0.3f;

        [Header("Policial - Impacto")]
        [Tooltip("Duração da proteção após um impacto (s).")]
        public float invulnerabilityTime = 1.5f;
        [Tooltip("Tempo em que o policial fica atordoado após bater num obstáculo (s).")]
        public float stunTime = 0.35f;
        public float blinkInterval = 0.08f;

        [Header("Transporte")]
        public float escalatorSpeed = 3.2f;
        [Tooltip("Velocidade do fugitivo na escada rolante (limitada à do policial).")]
        public float fugitiveEscalatorSpeed = 3.0f;
        public float elevatorTravelSpeed = 2.2f;
        public float elevatorDoorTime = 0.6f;
        public float elevatorWaitTime = 2.6f;
        public float elevatorEnterDistance = 1.1f;
        [Tooltip("Andar em que a cabine começa cada rodada, com as portas fechadas; depois ela desce até o térreo.")]
        public int elevatorStartFloor = 1;
        [Tooltip("Tempo parado no andar inicial antes de descer (s): define a janela para o policial pegar o elevador subindo.")]
        public float elevatorStartDelay = 1f;

        [Header("Fugitivo")]
        [Tooltip("Velocidade de corrida do fugitivo (m/s), fixa em todas as rodadas.")]
        public float fugitiveSpeed = 4f;
        public float captureDistance = 0.9f;
        [Tooltip("Velocidade máxima do fugitivo como fração da corrida do policial (o fugitivo é sempre mais lento).")]
        [Range(0.3f, 0.95f)] public float fugitiveMaxSpeedRatio = 0.8f;
        [Tooltip("Distância em que o fugitivo reage ao policial no mesmo andar.")]
        public float fugitiveReactDistance = 12f;
        [Tooltip("Tempo mínimo entre inversões de direção (s).")]
        public float fugitiveMinCommitTime = 1.2f;
        [Tooltip("Se o policial estiver no andar de cima a esta distância da saída da escada, o fugitivo não sobe.")]
        public float fugitiveAvoidExitDistance = 6f;

        [Header("Partida")]
        [Tooltip("Tempo mínimo da rodada (s).")]
        public float roundTime = 50f;
        [Tooltip("Aumenta o cronômetro quando necessário para que o fugitivo consiga chegar à saída do terraço antes do tempo acabar.")]
        public bool autoAdjustRoundTime = true;
        [Tooltip("Folga (s) além do tempo de fuga estimado do fugitivo.")]
        public float escapeTimeMargin = 6f;
        [Tooltip("Policial ativo + reservas. Reservas iniciais:")]
        public int startingReserveLives = 3;
        public int maxReserveLives = 3;
        public int extraLifeEvery = 10000;
        public float obstacleTimePenalty = 9f;
        public int collectiblePoints = 50;
        public float lowTimeWarning = 10f;
        public float roundEndDelay = 2.5f;
        public float lifeLostDelay = 2.5f;

        [Header("Pontuação (tempo restante x multiplicador)")]
        public int scoreMultiplierRounds1to8 = 100;
        public int scoreMultiplierRounds9to16 = 200;
        public int scoreMultiplierRounds17plus = 300;

        [Header("Câmera")]
        public float cameraOrthoSize = 3.8f;
        public float cameraHeightOffset = 2.0f;
        public float cameraSmoothX = 0.12f;
        public float cameraSmoothY = 0.25f;

        public int GetScoreMultiplier(int round)
        {
            if (round <= 8) return scoreMultiplierRounds1to8;
            if (round <= 16) return scoreMultiplierRounds9to16;
            return scoreMultiplierRounds17plus;
        }

        public float JumpVelocity => Mathf.Sqrt(2f * gravity * jumpHeight);

        /// <summary>Velocidade máxima permitida ao fugitivo: sempre abaixo da corrida do policial.</summary>
        public float MaxFugitiveSpeed => runSpeed * Mathf.Min(fugitiveMaxSpeedRatio, 0.95f);
        /// <summary>Velocidade do fugitivo na escada, nunca acima da do policial.</summary>
        public float FugitiveEscalatorSpeed => Mathf.Min(fugitiveEscalatorSpeed, escalatorSpeed);

        void OnValidate()
        {
            fugitiveMaxSpeedRatio = Mathf.Clamp(fugitiveMaxSpeedRatio, 0.3f, 0.95f);
            fugitiveEscalatorSpeed = Mathf.Min(fugitiveEscalatorSpeed, escalatorSpeed);
        }
    }
}
