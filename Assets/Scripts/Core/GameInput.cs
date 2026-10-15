using UnityEngine;
using UnityEngine.InputSystem;

namespace KeystoneKapers
{
    /// <summary>
    /// Entrada do jogo usando o Input System do projeto.
    /// A/D ou setas: correr | Espaço: pular | S/↓: abaixar ou sair do elevador | W/↑: entrar no elevador | Esc: pausar.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class GameInput : MonoBehaviour
    {
        public static GameInput Instance { get; private set; }

        InputAction move, jump, down, up, pause;

        public float Horizontal { get; private set; }
        public bool JumpPressed { get; private set; }
        public bool DownHeld { get; private set; }
        public bool DownPressed { get; private set; }
        public bool UpPressed { get; private set; }
        public bool PausePressed { get; private set; }

        // Usado para testes automatizados (simulação sem teclado).
        [HideInInspector] public float debugHorizontal;
        [HideInInspector] public bool debugDown;
        bool debugJumpOnce, debugUpOnce, debugDownOnce;

        /// <summary>Simula um toque de tecla por um frame (testes): "jump", "up", "down".</summary>
        public void DebugPress(string action)
        {
            if (action == "jump") debugJumpOnce = true;
            else if (action == "up") debugUpOnce = true;
            else if (action == "down") debugDownOnce = true;
        }

        void Awake()
        {
            Instance = this;
            move = new InputAction("Move", InputActionType.Value);
            move.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/a").With("Positive", "<Keyboard>/d");
            move.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/leftArrow").With("Positive", "<Keyboard>/rightArrow");
            move.AddBinding("<Gamepad>/leftStick/x");
            move.AddBinding("<Gamepad>/dpad/x");

            jump = new InputAction("Jump", InputActionType.Button);
            jump.AddBinding("<Keyboard>/space");
            jump.AddBinding("<Gamepad>/buttonSouth");

            down = new InputAction("Down", InputActionType.Button);
            down.AddBinding("<Keyboard>/s");
            down.AddBinding("<Keyboard>/downArrow");
            down.AddBinding("<Gamepad>/dpad/down");

            up = new InputAction("Up", InputActionType.Button);
            up.AddBinding("<Keyboard>/w");
            up.AddBinding("<Keyboard>/upArrow");
            up.AddBinding("<Gamepad>/dpad/up");

            pause = new InputAction("Pause", InputActionType.Button);
            pause.AddBinding("<Keyboard>/escape");
            pause.AddBinding("<Gamepad>/start");
        }

        void OnEnable() { move.Enable(); jump.Enable(); down.Enable(); up.Enable(); pause.Enable(); }
        void OnDisable() { move.Disable(); jump.Disable(); down.Disable(); up.Disable(); pause.Disable(); }
        void OnDestroy()
        {
            move.Dispose(); jump.Dispose(); down.Dispose(); up.Dispose(); pause.Dispose();
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            float h = move.ReadValue<float>();
            if (Mathf.Abs(debugHorizontal) > 0.01f) h = debugHorizontal;
            Horizontal = Mathf.Abs(h) < 0.2f ? 0f : Mathf.Sign(h);
            JumpPressed = jump.WasPressedThisFrame() || debugJumpOnce;
            DownHeld = down.IsPressed() || debugDown;
            DownPressed = down.WasPressedThisFrame() || debugDownOnce;
            UpPressed = up.WasPressedThisFrame() || debugUpOnce;
            debugJumpOnce = debugUpOnce = debugDownOnce = false;
            PausePressed = pause.WasPressedThisFrame();
        }
    }
}
