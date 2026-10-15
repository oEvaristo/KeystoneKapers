using UnityEngine;

namespace KeystoneKapers
{
    /// <summary>
    /// Câmera lateral ortográfica em blocos de tela, como no original: o corredor é dividido em blocos
    /// (LevelLayout.blockCount) e a câmera mostra um bloco inteiro; quando o policial passa da borda,
    /// a câmera troca para o bloco vizinho. A altura (Y) acompanha o andar com uma transição curta; Z é fixo.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraFollow : MonoBehaviour
    {
        public PoliceController target;
        public GameConfig config;
        [Tooltip("Distância fixa da câmera ao plano de jogo.")]
        public float distance = 20f;
        [Tooltip("Inclinação para dar leitura 2.5D.")]
        public float pitch = 8f;
        [Tooltip("Tamanho ortográfico mínimo (em telas muito largas o bloco cabe com sobra).")]
        public float minOrthoSize = 3.2f;
        [Tooltip("Duração da troca de bloco em segundos (0 = corte instantâneo como no original).")]
        public float blockSwitchTime = 0f;

        Camera cam;
        float vy, vx;
        int currentBlock = -1;

        LevelLayout Layout => LevelLayout.Instance;
        public int CurrentBlock => currentBlock;

        void Awake()
        {
            cam = GetComponent<Camera>();
            cam.orthographic = true;
        }

        void FitBlock()
        {
            // A largura visível é exatamente um bloco do corredor.
            float size = Layout.BlockWidth / (2f * Mathf.Max(0.1f, cam.aspect));
            cam.orthographicSize = Mathf.Max(minOrthoSize, size);
        }

        float DesiredY()
        {
            var p = target.transform.position;
            float y = target.IsTransporting ? p.y : Layout.FloorY(target.CurrentFloor);
            y += config.cameraHeightOffset;
            float minY = Layout.FloorY(0) + config.cameraHeightOffset;
            float maxY = Layout.FloorY(Layout.TerraceIndex) + config.cameraHeightOffset;
            return Mathf.Clamp(y, minY, maxY);
        }

        void Place(float x, float y)
        {
            var rot = Quaternion.Euler(pitch, 0f, 0f);
            transform.rotation = rot;
            transform.position = new Vector3(x, y, Layout.playZ) - rot * Vector3.forward * distance;
        }

        Vector3 FocusNow() => transform.position + Quaternion.Euler(pitch, 0f, 0f) * Vector3.forward * distance;

        public void SnapToTarget()
        {
            if (!target || !Layout) return;
            if (!cam) cam = GetComponent<Camera>();
            FitBlock();
            currentBlock = Layout.BlockIndex(target.transform.position.x);
            Place(Layout.BlockCenter(currentBlock), DesiredY());
            vx = vy = 0f;
        }

        void LateUpdate()
        {
            if (!target || !Layout || Time.timeScale <= 0f) return;
            FitBlock();
            currentBlock = Layout.BlockIndex(target.transform.position.x);
            float dt = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
            var focus = FocusNow();

            float targetX = Layout.BlockCenter(currentBlock);
            float x = blockSwitchTime <= 0f ? targetX : Mathf.SmoothDamp(focus.x, targetX, ref vx, blockSwitchTime, Mathf.Infinity, dt);

            float y = Mathf.SmoothDamp(focus.y, DesiredY(), ref vy, config.cameraSmoothY, Mathf.Infinity, dt);
            // Mantém o jogador sempre visível durante a transição vertical.
            float halfH = cam.orthographicSize * 0.85f;
            float py = target.transform.position.y + 0.9f;
            y = Mathf.Clamp(y, py - halfH, py + halfH);
            Place(x, y);
        }
    }
}
