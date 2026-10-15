using UnityEngine;

namespace KeystoneKapers
{
    /// <summary>
    /// Controle do policial (jogador). Usa apenas o CharacterController para mover — sem Rigidbody nem root motion.
    /// Movimento restrito ao plano lateral (X/Y) com Z fixo.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PoliceController : MonoBehaviour, IConnectionRider
    {
        public enum Mode { Normal, Escalator, EnteringElevator, InElevator, ExitingElevator, Frozen }

        public GameConfig config;
        [Tooltip("Pivô visual que gira para a direção da corrida.")]
        public Transform visualPivot;
        public Animator animator;
        public LayerMask groundMask = 1 << 8;

        public Mode CurrentMode { get; private set; } = Mode.Frozen;
        public bool IsGrounded { get; private set; }
        public bool IsCrouching { get; private set; }
        public int CurrentFloor { get; private set; }
        public float Facing { get; private set; } = 1f;
        public bool IsInvulnerable => invulnerableTimer > 0f;
        public bool IsStunned => stunTimer > 0f;
        public bool CanBeHit => CurrentMode == Mode.Normal && !IsInvulnerable;
        public bool IsTransporting => CurrentMode != Mode.Normal && CurrentMode != Mode.Frozen;
        /// <summary>Correndo no chão (mesma condição da animação de corrida).</summary>
        public bool IsRunning => CurrentMode == Mode.Normal && IsGrounded && !IsCrouching && !IsStunned && In && Mathf.Abs(In.Horizontal) > 0.01f;

        CharacterController cc;
        Renderer[] renderers;
        float vy;
        float invulnerableTimer, stunTimer, blinkTimer;
        float lerpT;
        Vector3 lerpFrom, lerpTo;
        FloorConnection activeConnection;
        float lastX;
        bool controlEnabled;

        static readonly int SpeedHash = Animator.StringToHash("Speed");
        static readonly int GroundedHash = Animator.StringToHash("Grounded");
        static readonly int CrouchHash = Animator.StringToHash("Crouch");

        LevelLayout Layout => LevelLayout.Instance;
        GameInput In => GameInput.Instance;
        public Transform RiderTransform => transform;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            renderers = GetComponentsInChildren<Renderer>(true);
            if (animator) animator.applyRootMotion = false;
            ApplyCapsule(false);
        }

        public void SetControlEnabled(bool on)
        {
            controlEnabled = on;
            if (!on && CurrentMode == Mode.Normal) CurrentMode = Mode.Frozen;
            if (on && CurrentMode == Mode.Frozen) CurrentMode = Mode.Normal;
        }

        public void ResetTo(Vector3 position, float facing)
        {
            if (activeConnection) activeConnection.Cancel(this);
            activeConnection = null;
            if (Layout && Layout.elevator) Layout.elevator.ReleaseRider();
            cc.enabled = false;
            transform.position = position;
            cc.enabled = true;
            vy = 0f; invulnerableTimer = 0f; stunTimer = 0f;
            IsCrouching = false; ApplyCapsule(false);
            SetVisible(true);
            Facing = Mathf.Sign(facing);
            if (visualPivot) visualPivot.localRotation = Quaternion.Euler(0f, 90f * Facing, 0f);
            CurrentMode = controlEnabled ? Mode.Normal : Mode.Frozen;
            CurrentFloor = Layout ? Layout.GetFloorIndex(position.y) : 0;
            lastX = position.x;
            Physics.SyncTransforms();
        }

        void ApplyCapsule(bool crouch)
        {
            float h = crouch ? config.crouchHeight : config.standHeight;
            cc.height = h;
            cc.radius = config.capsuleRadius;
            cc.center = new Vector3(0f, h * 0.5f + cc.skinWidth, 0f);
        }

        bool HasHeadroom()
        {
            float r = config.capsuleRadius * 0.9f;
            Vector3 bottom = transform.position + Vector3.up * (config.crouchHeight - r + 0.05f);
            Vector3 top = transform.position + Vector3.up * (config.standHeight - r + cc.skinWidth);
            return !Physics.CheckCapsule(bottom, top, r, groundMask, QueryTriggerInteraction.Ignore);
        }

        bool CheckGround()
        {
            if (vy > 0.01f) return false;
            if (cc.isGrounded) return true;
            float r = config.capsuleRadius * 0.9f;
            Vector3 origin = transform.position + Vector3.up * (r + 0.1f);
            return Physics.SphereCast(origin, r, Vector3.down, out _, 0.2f, groundMask, QueryTriggerInteraction.Ignore);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            TickTimers(dt);

            switch (CurrentMode)
            {
                case Mode.Normal: UpdateNormal(dt); break;
                case Mode.Frozen: UpdateFrozen(dt); break;
                case Mode.Escalator: UpdateFacingFromMotion(); break;
                case Mode.EnteringElevator: UpdateLerp(dt, Mode.InElevator); break;
                case Mode.ExitingElevator: UpdateLerp(dt, Mode.Normal); break;
                case Mode.InElevator: UpdateInElevator(); break;
            }

            UpdateVisualFacing(dt);
            UpdateAnimator();
            lastX = transform.position.x;
        }

        void TickTimers(float dt)
        {
            if (stunTimer > 0f) stunTimer -= dt;
            if (invulnerableTimer > 0f)
            {
                invulnerableTimer -= dt;
                blinkTimer -= dt;
                if (blinkTimer <= 0f) { blinkTimer = config.blinkInterval; SetVisible(!renderers[0].enabled); }
                if (invulnerableTimer <= 0f) SetVisible(true);
            }
        }

        void SetVisible(bool v)
        {
            foreach (var r in renderers) if (r) r.enabled = v;
        }

        void UpdateFrozen(float dt)
        {
            // Mantém a gravidade para assentar no chão mesmo sem controle.
            if (!cc.enabled) return;
            vy = Mathf.Max(vy - config.gravity * dt, -config.maxFallSpeed);
            var flags = cc.Move(new Vector3(0f, vy * dt, Layout ? Layout.playZ - transform.position.z : 0f));
            IsGrounded = CheckGround();
            if (IsGrounded && vy < 0f) vy = -2f;
        }

        void UpdateNormal(float dt)
        {
            IsGrounded = CheckGround();
            float input = In ? In.Horizontal : 0f;
            if (IsStunned) input = 0f;

            // Abaixar / levantar
            bool wantCrouch = In && In.DownHeld && IsGrounded && !IsStunned;
            if (wantCrouch && !IsCrouching) { IsCrouching = true; ApplyCapsule(true); }
            else if (!wantCrouch && IsCrouching && HasHeadroom()) { IsCrouching = false; ApplyCapsule(false); }
            if (IsCrouching) input = 0f;

            // Salto apenas a partir do chão
            if (IsGrounded && !IsCrouching && !IsStunned && In && In.JumpPressed)
            {
                vy = config.JumpVelocity;
                IsGrounded = false;
                GameAudio.PlayJump();
            }

            if (Mathf.Abs(input) > 0f) Facing = Mathf.Sign(input);

            float control = IsGrounded ? 1f : config.airControl;
            float vx = input * config.runSpeed * control;
            vy = Mathf.Max(vy - config.gravity * dt, -config.maxFallSpeed);
            float dz = Layout ? Layout.playZ - transform.position.z : 0f;
            var flags = cc.Move(new Vector3(vx * dt, vy * dt, dz));
            if ((flags & CollisionFlags.Below) != 0 && vy < 0f) vy = -2f;
            if ((flags & CollisionFlags.Above) != 0 && vy > 0f) vy = 0f;
            IsGrounded = CheckGround();
            if (IsGrounded) CurrentFloor = Layout.GetFloorIndex(transform.position.y);

            if (!IsGrounded || IsCrouching || IsStunned || !Layout) return;

            // Escada rolante (somente subida para o policial)
            foreach (var c in Layout.connections)
            {
                if (!c || !c.policeCanUse || !c.IsUp || c.fromFloor != CurrentFloor) continue;
                if (!c.InEntryZone(transform.position)) continue;
                if (input != 0f && Mathf.Sign(input) != c.EntryDirection) continue;
                if (c.TryBegin(this, config.escalatorSpeed)) return;
            }

            // Elevador
            var el = Layout.elevator;
            if (el && In.UpPressed && el.CanBoard(this, CurrentFloor) && el.Board(this))
            {
                StartLerp(el.riderPoint.position, Mode.EnteringElevator);
            }
        }

        void StartLerp(Vector3 target, Mode mode)
        {
            cc.enabled = false;
            lerpFrom = transform.position;
            lerpTo = target;
            lerpT = 0f;
            CurrentMode = mode;
            vy = 0f;
        }

        void UpdateLerp(float dt, Mode next)
        {
            lerpT += dt / 0.3f;
            if (CurrentMode == Mode.EnteringElevator && Layout.elevator) lerpTo = Layout.elevator.riderPoint.position;
            transform.position = Vector3.Lerp(lerpFrom, lerpTo, Mathf.SmoothStep(0f, 1f, lerpT));
            if (lerpT >= 1f)
            {
                CurrentMode = next;
                if (next == Mode.Normal)
                {
                    cc.enabled = true;
                    Physics.SyncTransforms();
                    CurrentFloor = Layout.GetFloorIndex(transform.position.y);
                    if (!controlEnabled) CurrentMode = Mode.Frozen;
                }
            }
        }

        void UpdateInElevator()
        {
            var el = Layout.elevator;
            if (In && In.DownPressed && el.CanExit)
            {
                Vector3 exit = el.ExitPosition();
                el.ReleaseRider();
                StartLerp(exit, Mode.ExitingElevator);
            }
        }

        /// <summary>Chamado pelo elevador em LateUpdate enquanto o policial está na cabine.</summary>
        public void FollowElevator(Vector3 riderPos)
        {
            if (CurrentMode == Mode.InElevator) transform.position = riderPos;
        }

        void UpdateFacingFromMotion()
        {
            float dx = transform.position.x - lastX;
            if (Mathf.Abs(dx) > 0.0005f) Facing = Mathf.Sign(dx);
        }

        void UpdateVisualFacing(float dt)
        {
            if (!visualPivot) return;
            float yaw = CurrentMode == Mode.InElevator || CurrentMode == Mode.EnteringElevator ? 180f : 90f * Facing;
            var target = Quaternion.Euler(0f, yaw, 0f);
            visualPivot.localRotation = Quaternion.RotateTowards(visualPivot.localRotation, target, config.turnSpeed * dt);
        }

        void UpdateAnimator()
        {
            if (!animator) return;
            float speed = 0f;
            bool grounded = true;
            bool crouch = false;
            if (CurrentMode == Mode.Normal)
            {
                speed = IsGrounded ? Mathf.Abs(In ? In.Horizontal : 0f) * (IsCrouching || IsStunned ? 0f : 1f) : 0f;
                grounded = IsGrounded;
                crouch = IsCrouching;
            }
            else if (CurrentMode == Mode.EnteringElevator || CurrentMode == Mode.ExitingElevator)
            {
                speed = 1f;
            }
            else if (CurrentMode == Mode.Frozen)
            {
                grounded = IsGrounded || !cc.enabled;
            }
            animator.SetFloat(SpeedHash, speed);
            animator.SetBool(GroundedHash, grounded);
            animator.SetBool(CrouchHash, crouch);
        }

        // ---- Impactos ----
        public void ApplyHit(float hopVelocity = 4f)
        {
            invulnerableTimer = config.invulnerabilityTime;
            stunTimer = config.stunTime;
            blinkTimer = 0f;
            if (IsGrounded && CurrentMode == Mode.Normal) vy = hopVelocity;
        }

        public Bounds HitBounds
        {
            get
            {
                float h = IsCrouching ? config.crouchHeight : config.standHeight;
                float r = config.capsuleRadius;
                var p = transform.position;
                return new Bounds(new Vector3(p.x, p.y + h * 0.5f, p.z), new Vector3(r * 2f, h, r * 2f));
            }
        }

        // ---- Escada rolante ----
        public void OnRideStart(FloorConnection c)
        {
            activeConnection = c;
            cc.enabled = false;
            IsCrouching = false; ApplyCapsule(false);
            CurrentMode = Mode.Escalator;
            vy = 0f;
        }

        public void OnRideEnd(FloorConnection c)
        {
            activeConnection = null;
            cc.enabled = true;
            Physics.SyncTransforms();
            vy = -2f;
            CurrentFloor = c.toFloor;
            CurrentMode = controlEnabled ? Mode.Normal : Mode.Frozen;
        }
    }
}
