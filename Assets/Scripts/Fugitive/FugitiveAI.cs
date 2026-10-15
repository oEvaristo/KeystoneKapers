using UnityEngine;

namespace KeystoneKapers
{
    /// <summary>
    /// IA determinística do fugitivo. Percorre os corredores, sobe pelas escadas rolantes rumo ao terraço,
    /// usa as escadas de descida exclusivas para fugir e inverte a direção quando o policial bloqueia o caminho.
    /// Não usa o elevador e mantém o Z fixo no corredor.
    /// </summary>
    public class FugitiveAI : MonoBehaviour, IConnectionRider
    {
        public enum Plan { GoUp, GoDown, Flee, Patrol, Escape }
        public enum Mode { Idle, Running, Riding, Escaped, Captured }

        public GameConfig config;
        public Transform visualPivot;
        public Animator animator;
        public PoliceController police;

        public Mode CurrentMode { get; private set; } = Mode.Idle;
        public Plan CurrentPlan { get; private set; }
        public int CurrentFloor { get; private set; }
        public bool IsTransporting => CurrentMode == Mode.Riding;
        float speed = 4f;
        /// <summary>Velocidade de corrida; travada para nunca superar a do policial.</summary>
        public float Speed
        {
            get => speed;
            set => speed = config ? Mathf.Min(value, config.MaxFugitiveSpeed) : value;
        }

        float dir = -1f;
        float targetX;
        float commitTimer;
        float decideTimer;
        float patrolSign = 1f;
        FloorConnection targetConnection, activeConnection;
        float lastX;

        public System.Action OnEscaped;

        LevelLayout Layout => LevelLayout.Instance;
        public Transform RiderTransform => transform;

        public void ResetTo(int floor, float x, float facing)
        {
            if (activeConnection) activeConnection.Cancel(this);
            activeConnection = null;
            targetConnection = null;
            CurrentFloor = floor;
            transform.position = new Vector3(x, Layout.FloorY(floor), Layout.playZ);
            dir = Mathf.Sign(facing);
            commitTimer = 0f; decideTimer = 0f;
            CurrentMode = Mode.Idle;
            if (visualPivot) visualPivot.localRotation = Quaternion.Euler(0f, 90f * dir, 0f);
            if (animator) animator.speed = 1f;
            lastX = x;
        }

        public void StartRunning() { if (CurrentMode == Mode.Idle) CurrentMode = Mode.Running; }
        public void Stop(Mode m)
        {
            if (activeConnection) { activeConnection.Cancel(this); activeConnection = null; }
            CurrentMode = m;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            // Ritmo da corrida proporcional à velocidade (o clip foi calibrado para a corrida do policial).
            if (animator) animator.speed = CurrentMode == Mode.Captured || CurrentMode == Mode.Idle ? 0.0f : Mathf.Clamp(Speed / (config ? config.runSpeed : 8f), 0.75f, 1.1f);

            // Limita o passo em quedas de framerate para não "pular" pelo corredor.
            if (CurrentMode == Mode.Running) UpdateRunning(Mathf.Min(dt, 1f / 30f));
            if (CurrentMode == Mode.Riding)
            {
                float dx = transform.position.x - lastX;
                if (Mathf.Abs(dx) > 0.0005f) dir = Mathf.Sign(dx);
            }
            if (visualPivot)
            {
                var target = Quaternion.Euler(0f, 90f * dir, 0f);
                visualPivot.localRotation = Quaternion.RotateTowards(visualPivot.localRotation, target, 900f * dt);
            }
            lastX = transform.position.x;
        }

        void UpdateRunning(float dt)
        {
            commitTimer -= dt;
            decideTimer -= dt;
            if (decideTimer <= 0f) { Decide(); decideTimer = 0.15f; }

            float x = transform.position.x;
            float delta = targetX - x;
            if (Mathf.Abs(delta) > 0.02f) dir = Mathf.Sign(delta);
            float step = Speed * dt;
            if (Mathf.Abs(delta) <= step)
            {
                x = targetX;
                OnReachedTarget();
            }
            else x += dir * step;

            if (CurrentMode == Mode.Running)
            {
                x = Layout.ClampX(CurrentFloor, x, 0.3f);
                transform.position = new Vector3(x, Layout.FloorY(CurrentFloor), Layout.playZ);
            }
        }

        bool CopOnSameFloor => police && !police.IsTransporting && police.CurrentFloor == CurrentFloor;

        bool CopBlocks(float goalX, float range)
        {
            if (!CopOnSameFloor) return false;
            float me = transform.position.x;
            float cop = police.transform.position.x;
            float toGoal = goalX - me;
            float toCop = cop - me;
            if (Mathf.Abs(toCop) > range) return false;
            if (Mathf.Abs(toGoal) < 0.05f) return Mathf.Abs(toCop) < 1.5f;
            return Mathf.Sign(toCop) == Mathf.Sign(toGoal) && Mathf.Abs(toCop) < Mathf.Abs(toGoal) + 1f;
        }

        void SetPlan(Plan p, float x, FloorConnection c)
        {
            if (p != CurrentPlan) commitTimer = config.fugitiveMinCommitTime;
            CurrentPlan = p;
            targetX = x;
            targetConnection = c;
        }

        void Decide()
        {
            if (commitTimer > 0f && (CurrentPlan == Plan.GoDown || CurrentPlan == Plan.Flee)) return;

            var f = Layout.floors[CurrentFloor];
            float react = config.fugitiveReactDistance;
            float me = transform.position.x;

            if (CurrentFloor == Layout.TerraceIndex)
            {
                float escapeX = Layout.escapePoint ? Layout.escapePoint.position.x : f.minX + 0.5f;
                if (!CopBlocks(escapeX, react)) { SetPlan(Plan.Escape, escapeX, null); return; }
                var down = Layout.GetDown(CurrentFloor);
                if (down && !CopBlocks(down.EntryPosition.x, react)) { SetPlan(Plan.GoDown, down.EntryPosition.x, down); return; }
                SetPlan(Plan.Flee, FleeX(f), null);
                return;
            }

            var up = Layout.GetUp(CurrentFloor, true);
            bool upAllowed = up != null;
            // Não sobe se o policial estiver esperando perto da saída no andar de cima.
            if (up && police && !police.IsTransporting && police.CurrentFloor == up.toFloor &&
                Mathf.Abs(police.transform.position.x - up.ExitPosition.x) < config.fugitiveAvoidExitDistance)
                upAllowed = false;

            if (upAllowed && !CopBlocks(up.EntryPosition.x, react))
            {
                SetPlan(Plan.GoUp, up.EntryPosition.x, up);
                return;
            }

            if (CopOnSameFloor && Mathf.Abs(police.transform.position.x - me) < react)
            {
                var down = Layout.GetDown(CurrentFloor);
                if (down && !CopBlocks(down.EntryPosition.x, react)) { SetPlan(Plan.GoDown, down.EntryPosition.x, down); return; }
                SetPlan(Plan.Flee, FleeX(f), null);
                return;
            }

            // Patrulha: vai e volta pelo corredor, evitando a extremidade da escada vigiada.
            if (CurrentPlan != Plan.Patrol) patrolSign = up ? -Mathf.Sign(up.EntryPosition.x - me) : 1f;
            float px = patrolSign > 0 ? f.maxX - 1.5f : f.minX + 1.5f;
            SetPlan(Plan.Patrol, px, null);
        }

        float FleeX(LevelLayout.FloorInfo f)
        {
            float cop = police ? police.transform.position.x : 0f;
            return transform.position.x >= cop ? f.maxX - 0.6f : f.minX + 0.6f;
        }

        void OnReachedTarget()
        {
            switch (CurrentPlan)
            {
                case Plan.GoUp:
                case Plan.GoDown:
                    if (targetConnection && targetConnection.TryBegin(this, config.FugitiveEscalatorSpeed)) return;
                    break;
                case Plan.Escape:
                    CurrentMode = Mode.Escaped;
                    OnEscaped?.Invoke();
                    return;
                case Plan.Patrol:
                    patrolSign = -patrolSign;
                    decideTimer = 0f;
                    break;
                case Plan.Flee:
                    commitTimer = 0f;
                    decideTimer = 0f;
                    break;
            }
        }

        public void OnRideStart(FloorConnection c)
        {
            activeConnection = c;
            CurrentMode = Mode.Riding;
        }

        public void OnRideEnd(FloorConnection c)
        {
            activeConnection = null;
            CurrentFloor = c.toFloor;
            CurrentMode = Mode.Running;
            commitTimer = 0f;
            decideTimer = 0f;
            CurrentPlan = Plan.Patrol;
            Decide();
        }
    }
}
