using System;
using System.Collections.Generic;
using UnityEngine;

namespace KeystoneKapers
{
    /// <summary>
    /// Descreve os andares, corredores e conexões da loja. Usado pela IA, geração de obstáculos, câmera e minimapa.
    /// </summary>
    public class LevelLayout : MonoBehaviour
    {
        public static LevelLayout Instance { get; private set; }

        [Serializable]
        public class FloorInfo
        {
            public string name;
            [Tooltip("Altura da superfície do piso.")]
            public float y;
            public float minX = -24f;
            public float maxX = 24f;
        }

        [Tooltip("Andares internos em ordem (0 = térreo). O último é o terraço.")]
        public FloorInfo[] floors;
        [Tooltip("Profundidade fixa do plano de jogo (Z).")]
        public float playZ = 0f;
        public Transform escapePoint;
        public FloorConnection[] connections;
        public ElevatorController elevator;
        [Tooltip("Meia-largura da zona livre na frente do elevador para geração.")]
        public float elevatorNoSpawnHalfWidth = 2.2f;
        public float connectionNoSpawnHalfWidth = 2.2f;

        [Header("Blocos de tela (como no original)")]
        [Tooltip("Quantidade de blocos de tela por andar: o central + 3 para cada lado.")]
        public int blockCount = 7;

        public float MinX { get { float m = float.MaxValue; foreach (var f in floors) m = Mathf.Min(m, f.minX); return m; } }
        public float MaxX { get { float m = float.MinValue; foreach (var f in floors) m = Mathf.Max(m, f.maxX); return m; } }
        public float BlockWidth => (MaxX - MinX) / Mathf.Max(1, blockCount);
        public int BlockIndex(float x) => Mathf.Clamp(Mathf.FloorToInt((x - MinX) / BlockWidth), 0, blockCount - 1);
        public float BlockMin(int i) => MinX + i * BlockWidth;
        public float BlockMax(int i) => MinX + (i + 1) * BlockWidth;
        public float BlockCenter(int i) => MinX + (i + 0.5f) * BlockWidth;

        public int TerraceIndex => floors.Length - 1;
        public int FloorCount => floors.Length;

        void Awake() { Instance = this; }

        public float FloorY(int i) => floors[Mathf.Clamp(i, 0, floors.Length - 1)].y;

        /// <summary>Índice do andar cuja superfície está logo abaixo de y.</summary>
        public int GetFloorIndex(float y)
        {
            int best = 0;
            for (int i = 0; i < floors.Length; i++)
                if (y >= floors[i].y - 0.6f) best = i;
            return best;
        }

        public FloorConnection GetUp(int floor, bool forFugitive)
        {
            foreach (var c in connections)
                if (c && c.fromFloor == floor && c.toFloor == floor + 1 && (forFugitive ? c.fugitiveCanUse : c.policeCanUse)) return c;
            return null;
        }

        public FloorConnection GetDown(int floor)
        {
            foreach (var c in connections)
                if (c && c.fromFloor == floor && c.toFloor == floor - 1 && c.fugitiveCanUse) return c;
            return null;
        }

        /// <summary>Zonas onde não se deve gerar obstáculos (entradas/saídas de escadas e elevador).</summary>
        public bool IsNoSpawnZone(int floor, float x)
        {
            if (elevator && floor <= elevator.TopFloor && Mathf.Abs(x - elevator.transform.position.x) < elevatorNoSpawnHalfWidth) return true;
            foreach (var c in connections)
            {
                if (!c) continue;
                if (c.fromFloor == floor && Mathf.Abs(x - c.EntryPosition.x) < connectionNoSpawnHalfWidth) return true;
                if (c.toFloor == floor && Mathf.Abs(x - c.ExitPosition.x) < connectionNoSpawnHalfWidth) return true;
            }
            return false;
        }

        public float ClampX(int floor, float x, float margin = 0.4f)
        {
            var f = floors[Mathf.Clamp(floor, 0, floors.Length - 1)];
            return Mathf.Clamp(x, f.minX + margin, f.maxX - margin);
        }

        public IEnumerable<FloorConnection> All => connections;
    }
}
