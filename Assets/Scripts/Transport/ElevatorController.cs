using UnityEngine;

namespace KeystoneKapers
{
    /// <summary>
    /// Elevador automático entre os andares internos. Move apenas a cabine (prefab Elevador) e anima
    /// as portas existentes (PortaEsquerda/PortaDireita) pelo blendshape "Key 1" do modelo.
    /// Ciclo: chegar → abrir → esperar → fechar → viajar.
    /// </summary>
    public class ElevatorController : MonoBehaviour
    {
        public enum State { Opening, Open, Closing, Moving }

        [Tooltip("Cabine móvel (instância do prefab Elevador).")]
        public Transform cabin;
        [Tooltip("Renderers das portas que possuem o blendshape de abertura.")]
        public SkinnedMeshRenderer[] doors;
        [Tooltip("Ponto onde o passageiro fica dentro da cabine (filho da cabine).")]
        public Transform riderPoint;
        [Tooltip("Deslocamento vertical da cabine em relação ao piso do andar.")]
        public float cabinFloorOffset = 0f;
        public int bottomFloor = 0;
        public int topFloor = 2;
        public GameConfig config;

        public State CurrentState { get; private set; }
        public int CurrentFloor { get; private set; }
        public float DoorOpenAmount { get; private set; }
        public int TopFloor => topFloor;
        public bool HasRider => rider != null;

        int direction = 1;
        float timer;
        PoliceController rider;
        float holdOpenTimer;
        float startHold;

        LevelLayout Layout => LevelLayout.Instance;

        void Start() { ResetElevator(); }

        /// <summary>
        /// Começa parado no andar inicial (GameConfig.elevatorStartFloor) com as portas fechadas e depois
        /// desce até o térreo: correndo direto, o policial chega a tempo de pegar a cabine subindo.
        /// </summary>
        public void ResetElevator()
        {
            rider = null;
            CurrentFloor = Mathf.Clamp(config ? config.elevatorStartFloor : bottomFloor, bottomFloor, topFloor);
            direction = CurrentFloor > bottomFloor ? -1 : 1;
            SetCabinY(FloorCabinY(CurrentFloor));
            timer = 0f;
            SetDoors(0f);
            bool above = CurrentFloor > bottomFloor;
            CurrentState = above ? State.Closing : State.Opening; // Closing com portas fechadas = parte em seguida
            startHold = above && config ? config.elevatorStartDelay : 0f;
        }

        float FloorCabinY(int floor) => Layout.FloorY(floor) + cabinFloorOffset;

        void SetCabinY(float y)
        {
            var p = cabin.position; p.y = y; cabin.position = p;
        }

        void SetDoors(float amount)
        {
            DoorOpenAmount = Mathf.Clamp01(amount);
            if (doors == null) return;
            foreach (var d in doors)
                if (d && d.sharedMesh && d.sharedMesh.blendShapeCount > 0)
                    d.SetBlendShapeWeight(0, DoorOpenAmount * 100f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            float doorTime = config ? config.elevatorDoorTime : 0.6f;
            float waitTime = config ? config.elevatorWaitTime : 2.5f;
            float speed = config ? config.elevatorTravelSpeed : 2f;
            holdOpenTimer -= dt;
            if (startHold > 0f) { startHold -= dt; return; }

            switch (CurrentState)
            {
                case State.Opening:
                    SetDoors(DoorOpenAmount + dt / doorTime);
                    if (DoorOpenAmount >= 1f) { CurrentState = State.Open; timer = waitTime; }
                    break;
                case State.Open:
                    timer -= dt;
                    if (timer <= 0f && holdOpenTimer <= 0f) CurrentState = State.Closing;
                    break;
                case State.Closing:
                    SetDoors(DoorOpenAmount - dt / doorTime);
                    if (DoorOpenAmount <= 0f)
                    {
                        int next = CurrentFloor + direction;
                        if (next > topFloor || next < bottomFloor) { direction = -direction; next = CurrentFloor + direction; }
                        CurrentFloor = next;
                        CurrentState = State.Moving;
                    }
                    break;
                case State.Moving:
                    float targetY = FloorCabinY(CurrentFloor);
                    float y = Mathf.MoveTowards(cabin.position.y, targetY, speed * dt);
                    SetCabinY(y);
                    if (Mathf.Approximately(y, targetY))
                    {
                        if (CurrentFloor == topFloor) direction = -1;
                        else if (CurrentFloor == bottomFloor) direction = 1;
                        CurrentState = State.Opening;
                    }
                    break;
            }
        }

        void LateUpdate()
        {
            if (rider) rider.FollowElevator(riderPoint.position);
        }

        /// <summary>Andar (índice) em que a cabine está alinhada e aberta, ou -1.</summary>
        public int OpenFloor => CurrentState == State.Open ? CurrentFloor : -1;

        public bool CanBoard(PoliceController p, int floor)
        {
            if (rider || CurrentState != State.Open || CurrentFloor != floor) return false;
            return Mathf.Abs(p.transform.position.x - cabin.position.x) <= (config ? config.elevatorEnterDistance : 1f);
        }

        public bool Board(PoliceController p)
        {
            if (rider) return false;
            rider = p;
            holdOpenTimer = 0.4f;
            return true;
        }

        public bool CanExit => rider && CurrentState == State.Open;

        public Vector3 ExitPosition()
        {
            return new Vector3(cabin.position.x, Layout.FloorY(CurrentFloor), Layout.playZ);
        }

        public void ReleaseRider()
        {
            rider = null;
            holdOpenTimer = 0.4f;
        }

        /// <summary>Altura (Y do piso da cabine) atual, para o minimapa.</summary>
        public float CabinFloorY => cabin.position.y - cabinFloorOffset;
    }
}
