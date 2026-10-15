using System.Collections.Generic;
using UnityEngine;

namespace KeystoneKapers
{
    /// <summary>
    /// Geração de obstáculos em ondas determinísticas, com pool de objetos (sem acúmulo).
    /// Obstáculos móveis pertencem ao bloco de tela do policial: nascem fora da tela na ponta oposta do bloco,
    /// atravessam vindo em direção a ele e são reciclados ao sair do bloco ou quando ele troca de bloco.
    /// Rádios e coletáveis são posicionados no início de cada tentativa.
    /// </summary>
    public class ObstacleSpawner : MonoBehaviour
    {
        public Obstacle cartPrefab, ballPrefab, radioPrefab, planePrefab;
        public Collectible[] collectiblePrefabs;
        public PoliceController police;
        public Camera gameCamera;
        [Tooltip("Distância mínima entre o ponto de geração e o policial.")]
        public float minSpawnDistance = 6f;
        [Tooltip("Distância além da borda do bloco visível onde os obstáculos móveis nascem.")]
        public float offscreenMargin = 1.5f;
        [Tooltip("Nos blocos das pontas, o obstáculo nasce dentro da parede lateral (canto do mapa), fora de vista.")]
        public float wallSpawnInset = 0.8f;
        [Tooltip("Rádios não ficam colados nas bordas dos blocos (o jogador precisa vê-los ao entrar no bloco).")]
        public float radioBlockEdgeMargin = 3f;
        public float firstWaveDelay = 2.5f;
        [Tooltip("Tempo após entrar em um bloco até a onda nascer na ponta oposta.")]
        public float blockEntryDelay = 0.5f;
        public int maxActive = 14;

        readonly Dictionary<ObstacleType, Queue<Obstacle>> pool = new Dictionary<ObstacleType, Queue<Obstacle>>();
        readonly List<Obstacle> active = new List<Obstacle>();
        readonly List<Collectible> collectibles = new List<Collectible>();

        RoundDifficulty diff;
        int round;
        float waveTimer;
        int waveIndex;
        // Onda em andamento
        int pendingInWave;
        float pendingTimer;
        ObstacleType waveType;
        float waveX, waveDir;
        int waveFloor;
        bool running;
        int currentBlockKey = -1;

        public IReadOnlyList<Obstacle> Active => active;
        public IReadOnlyList<Collectible> Collectibles => collectibles;

        LevelLayout Layout => LevelLayout.Instance;

        public void BeginAttempt(int roundNumber, RoundDifficulty d)
        {
            ClearAll();
            round = roundNumber;
            diff = d;
            waveTimer = firstWaveDelay;
            waveIndex = 0;
            pendingInWave = 0;
            PlaceRadios();
            PlaceCollectibles();
            currentBlockKey = police ? BlockKey() : -1;
            running = true;
        }

        public void SetRunning(bool on) => running = on;

        public void ClearAll()
        {
            for (int i = active.Count - 1; i >= 0; i--) Recycle(active[i]);
            active.Clear();
            foreach (var c in collectibles) if (c) c.Collect();
            pendingInWave = 0;
            running = false;
        }

        Obstacle Get(ObstacleType t)
        {
            if (!pool.TryGetValue(t, out var q)) pool[t] = q = new Queue<Obstacle>();
            Obstacle o = q.Count > 0 ? q.Dequeue() : null;
            if (!o)
            {
                var prefab = t == ObstacleType.Cart ? cartPrefab : t == ObstacleType.Ball ? ballPrefab : t == ObstacleType.Plane ? planePrefab : radioPrefab;
                o = Instantiate(prefab, transform);
                o.name = prefab.name;
            }
            active.Add(o);
            return o;
        }

        void Recycle(Obstacle o)
        {
            if (!o) return;
            o.Deactivate();
            active.Remove(o);
            if (!pool.TryGetValue(o.type, out var q)) pool[o.type] = q = new Queue<Obstacle>();
            if (!q.Contains(o)) q.Enqueue(o);
        }


        void PlaceRadios()
        {
            if (diff.radiosPerFloor <= 0) return;
            for (int f = 0; f < Layout.FloorCount; f++)
            {
                var fl = Layout.floors[f];
                int n = diff.radiosPerFloor;
                for (int i = 0; i < n; i++)
                {
                    float u = (i + 0.5f + (f % 2 == 0 ? 0.15f : -0.15f)) / n;
                    float x = Mathf.Lerp(fl.minX + 5f, fl.maxX - 5f, u);
                    if (!FindFreeSpot(f, ref x, true)) continue;
                    var o = Get(ObstacleType.Radio);
                    o.Launch(f, x, 1f, diff);
                }
            }
        }

        void PlaceCollectibles()
        {
            if (collectiblePrefabs == null || collectiblePrefabs.Length == 0) return;
            int count = diff.collectibles;
            while (collectibles.Count < count)
            {
                var c = Instantiate(collectiblePrefabs[collectibles.Count % collectiblePrefabs.Length], transform);
                c.gameObject.SetActive(false);
                collectibles.Add(c);
            }
            for (int i = 0; i < count; i++)
            {
                int f = 1 + (i + round) % Mathf.Max(1, Layout.FloorCount - 1);
                var fl = Layout.floors[f];
                float x = Mathf.Lerp(fl.minX + 4f, fl.maxX - 4f, ((i * 0.37f + round * 0.23f) % 1f));
                if (!FindFreeSpot(f, ref x, true)) continue;
                collectibles[i].Place(f, new Vector3(x, fl.y + 1.0f, Layout.playZ));
            }
        }

        bool FindFreeSpot(int floor, ref float x, bool avoidPolice)
        {
            for (int k = 0; k < 8; k++)
            {
                float cx = Layout.ClampX(floor, x + (k % 2 == 0 ? 1 : -1) * k * 1.5f, 1f);
                bool nearPolice = avoidPolice && police && police.CurrentFloor == floor && Mathf.Abs(cx - police.transform.position.x) < minSpawnDistance;
                bool nearStatic = false;
                foreach (var o in active) if (o.Floor == floor && Mathf.Abs(o.transform.position.x - cx) < 2.5f) nearStatic = true;
                int b = Layout.BlockIndex(cx);
                bool nearBlockEdge = Mathf.Min(cx - Layout.BlockMin(b), Layout.BlockMax(b) - cx) < radioBlockEdgeMargin;
                if (!Layout.IsNoSpawnZone(floor, cx) && !nearPolice && !nearStatic && !nearBlockEdge) { x = cx; return true; }
            }
            return false;
        }

        int BlockKey() => police.CurrentFloor * 100 + Layout.BlockIndex(police.transform.position.x);

        /// <summary>
        /// Ao entrar em um bloco (ou andar), os obstáculos móveis do bloco anterior são removidos e a próxima onda
        /// nasce logo em seguida na ponta oposta do novo bloco, vindo em direção ao policial.
        /// </summary>
        void EnterBlock(int key)
        {
            currentBlockKey = key;
            for (int i = active.Count - 1; i >= 0; i--)
                if (active[i] && active[i].type != ObstacleType.Radio) Recycle(active[i]);
            pendingInWave = 0;
            waveTimer = blockEntryDelay;
        }

        void Update()
        {
            if (Time.deltaTime <= 0f) return;
            if (!running || diff == null || !police) { RecycleOutOfBounds(); return; }

            if (!police.IsTransporting)
            {
                int key = BlockKey();
                if (key != currentBlockKey) EnterBlock(key);
            }
            RecycleOutOfBounds();

            if (pendingInWave > 0)
            {
                pendingTimer -= Time.deltaTime;
                if (pendingTimer <= 0f)
                {
                    TrySpawnMoving(waveType, waveFloor, waveX, waveDir);
                    pendingInWave--;
                    float spacing = waveType == ObstacleType.Cart ? diff.cartWaveSpacing : diff.waveSpacing;
                    pendingTimer = spacing / Mathf.Max(0.5f, SpeedOf(waveType));
                }
                return;
            }

            waveTimer -= Time.deltaTime;
            if (waveTimer > 0f) return;

            var allowed = diff.AllowedMoving();
            if (allowed.Count == 0 || police.IsTransporting) { waveTimer = 0.5f; return; }

            waveType = allowed[waveIndex % allowed.Count];
            waveFloor = police.CurrentFloor;
            if (!FarEdgeSpawn(waveFloor, out waveX, out waveDir))
            {
                waveTimer = 0.5f;
                return;
            }
            waveIndex++;
            pendingInWave = Mathf.Max(1, diff.obstaclesPerWave);
            pendingTimer = 0f;
            waveTimer = diff.waveInterval;
        }

        float SpeedOf(ObstacleType t) => t == ObstacleType.Cart ? diff.cartSpeed : t == ObstacleType.Ball ? diff.ballSpeed : diff.planeSpeed;

        /// <summary>
        /// Ponto de nascimento fora da tela, na ponta do bloco atual mais distante do policial, para o obstáculo
        /// atravessar o bloco vindo em direção a ele. Nos blocos das pontas do mapa, nasce dentro da parede lateral.
        /// </summary>
        public bool FarEdgeSpawn(int floor, out float x, out float dir)
        {
            float px = police.transform.position.x;
            var fl = Layout.floors[floor];
            int b = Layout.BlockIndex(px);
            float lo = Layout.BlockMin(b), hi = Layout.BlockMax(b);
            float side = (px - lo) < (hi - px) ? 1f : -1f;
            x = (side > 0 ? hi : lo) + side * offscreenMargin;
            x = Mathf.Clamp(x, fl.minX - wallSpawnInset, fl.maxX + wallSpawnInset);
            for (int k = 0; k < 4 && Layout.IsNoSpawnZone(floor, x); k++)
                x = Mathf.Clamp(x + side * 1.5f, fl.minX - wallSpawnInset, fl.maxX + wallSpawnInset);
            dir = -side;
            if (Layout.IsNoSpawnZone(floor, x)) return false;
            return Mathf.Abs(x - px) >= minSpawnDistance;
        }

        /// <summary>Verdadeiro se x está fora da área visível do bloco do policial.</summary>
        public bool IsOffscreen(float x)
        {
            int b = Layout.BlockIndex(police.transform.position.x);
            return x < Layout.BlockMin(b) - 0.3f || x > Layout.BlockMax(b) + 0.3f;
        }

        void TrySpawnMoving(ObstacleType t, int floor, float x, float dir)
        {
            if (active.Count >= maxActive) return;
            // Ao trocar de bloco a onda é cancelada (EnterBlock); aqui só evita nascer perto do policial.
            if (police.CurrentFloor != floor || !IsOffscreen(x)) return;
            if (police.CurrentFloor == floor && Mathf.Abs(police.transform.position.x - x) < minSpawnDistance * 0.6f) return;
            var o = Get(t);
            o.Launch(floor, x, dir, diff);
        }

        void RecycleOutOfBounds()
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var o = active[i];
                if (!o || o.type == ObstacleType.Radio) continue;
                var fl = Layout.floors[o.Floor];
                float x = o.transform.position.x;
                // Recicla ao sair do mapa pelo lado para onde está indo.
                if ((o.Direction < 0f && x < fl.minX - wallSpawnInset - 0.5f) || (o.Direction > 0f && x > fl.maxX + wallSpawnInset + 0.5f)) { Recycle(o); continue; }
                // Também recicla quando já atravessou o bloco do policial e saiu pelo outro lado (não fica esperando no bloco vizinho).
                if (police && !police.IsTransporting && o.Floor == police.CurrentFloor)
                {
                    int b = Layout.BlockIndex(police.transform.position.x);
                    float m = offscreenMargin + 0.6f;
                    if ((o.Direction < 0f && x < Layout.BlockMin(b) - m) || (o.Direction > 0f && x > Layout.BlockMax(b) + m)) Recycle(o);
                }
            }
        }
    }
}
