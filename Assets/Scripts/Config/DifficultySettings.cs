using System;
using System.Collections.Generic;
using UnityEngine;

namespace KeystoneKapers
{
    public enum ObstacleType { Cart, Ball, Radio, Plane }

    [Serializable]
    public class RoundDifficulty
    {
        public string label = "Rodada";
        [Header("Tipos permitidos")]
        public bool carts = true;
        public bool balls;
        public bool planes;
        [Tooltip("Rádios parados por andar.")]
        public int radiosPerFloor;

        [Header("Ondas")]
        [Tooltip("Obstáculos por sequência (onda).")]
        public int obstaclesPerWave = 1;
        [Tooltip("Intervalo entre ondas (s).")]
        public float waveInterval = 5f;
        [Tooltip("Distância entre obstáculos da mesma onda (m).")]
        public float waveSpacing = 3.5f;
        [Tooltip("Distância entre carrinhos da mesma onda (m): curta, para o policial pular todos num único pulo.")]
        public float cartWaveSpacing = 1.5f;

        [Header("Velocidades")]
        public float cartSpeed = 3f;
        public float ballSpeed = 2.6f;
        public float planeSpeed = 4f;
        [Tooltip("Altura do quique da bola (base da bola, em m).")]
        public float ballBounceHeight = 0.35f;
        public float ballBouncePeriod = 1.1f;

        [Header("Coletáveis")]
        public int collectibles = 2;

        public List<ObstacleType> AllowedMoving()
        {
            var l = new List<ObstacleType>();
            if (carts) l.Add(ObstacleType.Cart);
            if (balls) l.Add(ObstacleType.Ball);
            if (planes) l.Add(ObstacleType.Plane);
            return l;
        }
    }

    /// <summary>
    /// Tabela de dificuldade por rodada. A última entrada é usada para rodadas além da lista,
    /// com escalonamento suave definido pelos campos "beyond".
    /// </summary>
    [CreateAssetMenu(menuName = "Keystone Kapers/Difficulty Settings", fileName = "DifficultySettings")]
    public class DifficultySettings : ScriptableObject
    {
        public List<RoundDifficulty> rounds = new List<RoundDifficulty>();

        [Header("Escalonamento após a última entrada (por rodada extra)")]
        public float beyondSpeedIncrease = 0.15f;
        public float beyondIntervalDecrease = 0.15f;
        public float minWaveInterval = 2f;
        public float maxObstacleSpeed = 9f;

        public RoundDifficulty Get(int round)
        {
            if (rounds == null || rounds.Count == 0) return new RoundDifficulty();
            int idx = Mathf.Clamp(round - 1, 0, rounds.Count - 1);
            var src = rounds[idx];
            int extra = Mathf.Max(0, round - rounds.Count);
            if (extra == 0) return src;
            var r = (RoundDifficulty)src.MemberwiseCloneSafe();
            r.cartSpeed = Mathf.Min(maxObstacleSpeed, r.cartSpeed + beyondSpeedIncrease * extra);
            r.planeSpeed = Mathf.Min(maxObstacleSpeed, r.planeSpeed + beyondSpeedIncrease * extra);
            r.ballSpeed = Mathf.Min(maxObstacleSpeed, r.ballSpeed + beyondSpeedIncrease * extra);
            r.waveInterval = Mathf.Max(minWaveInterval, r.waveInterval - beyondIntervalDecrease * extra);
            return r;
        }
    }

    static class RoundDifficultyExt
    {
        public static object MemberwiseCloneSafe(this RoundDifficulty d) => JsonUtility.FromJson<RoundDifficulty>(JsonUtility.ToJson(d));
    }
}
