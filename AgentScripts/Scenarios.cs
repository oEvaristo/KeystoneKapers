using System.Text;
using System.Threading.Tasks;
using KeystoneKapers;
using UnityEngine;

public static class Scenarios
{
    static GameManager GM => GameManager.Instance;
    static PoliceController P => GM.police;
    static GameInput In => GameInput.Instance;
    static LevelLayout L => LevelLayout.Instance;
    static StringBuilder sb;

    static void Log(string s) { sb.AppendLine($"[{Time.time:F2}] {s}"); Debug.Log("[TESTE] " + s); }
    static void Check(bool ok, string what) { sb.AppendLine((ok ? "  PASS " : "  FAIL ") + what); Debug.Log("[TESTE] " + (ok ? "PASS " : "FAIL ") + what); }
    static async Task Wait(float s) => await Awaitable.WaitForSecondsAsync(s);
    static async Task<bool> WaitUntil(System.Func<bool> cond, float timeout)
    {
        float t = 0;
        while (!cond() && t < timeout) { await Awaitable.NextFrameAsync(); t += Time.deltaTime; }
        return cond();
    }

    static string AnimState()
    {
        var st = P.animator.GetCurrentAnimatorStateInfo(0);
        foreach (var n in new[] { "Parado", "Correndo", "Pulando", "Abaixado" }) if (st.IsName(n)) return n;
        return "?";
    }

    static void Setup(int floor, float x)
    {
        if (GM.State != GameState.Playing) GM.StartNewGame();
        GM.spawner.ClearAll();
        GM.fugitive.Stop(FugitiveAI.Mode.Idle);
        GM.fugitive.ResetTo(3, -10, -1); // fora do caminho, no terraço, parado
        In.debugHorizontal = 0; In.debugDown = false;
        P.ResetTo(new Vector3(x, L.FloorY(floor), 0), 1);
        GM.DebugSetTime(50);
    }

    // ---------- Movimento ----------
    public static async Task<string> Movement()
    {
        sb = new StringBuilder();
        Setup(0, -8);
        await Wait(0.3f);
        Log($"idle anim={AnimState()} grounded={P.IsGrounded}");
        Check(AnimState() == "Parado" && P.IsGrounded, "parado no chão");
        float x0 = P.transform.position.x;
        In.debugHorizontal = 1;
        await Wait(0.5f);
        Log($"run x={P.transform.position.x:F2} anim={AnimState()} yaw={P.visualPivot.localEulerAngles.y:F0} z={P.transform.position.z:F3}");
        Check(P.transform.position.x > x0 + 2f && AnimState() == "Correndo", "corre para a direita");
        Check(Mathf.Abs(P.visualPivot.localEulerAngles.y - 90) < 5, "vira para a direita");
        In.debugHorizontal = -1;
        await Wait(0.4f);
        Check(Mathf.Abs(P.visualPivot.localEulerAngles.y - 270) < 5, $"vira para a esquerda (yaw={P.visualPivot.localEulerAngles.y:F0})");
        In.debugHorizontal = 0;
        await Wait(0.2f);
        // salto parado
        float y0 = P.transform.position.y, maxY = y0; int jumpsWhileAir = 0;
        In.DebugPress("jump");
        await Awaitable.NextFrameAsync(); await Awaitable.NextFrameAsync();
        bool sawJumpAnim = false;
        float t = 0;
        while (t < 1.2f)
        {
            maxY = Mathf.Max(maxY, P.transform.position.y);
            if (AnimState() == "Pulando") sawJumpAnim = true;
            if (!P.IsGrounded && t > 0.15f && t < 0.3f) { In.DebugPress("jump"); jumpsWhileAir++; }
            await Awaitable.NextFrameAsync(); t += Time.deltaTime;
        }
        Log($"jump peak={maxY - y0:F2} sawJumpAnim={sawJumpAnim} landed={P.IsGrounded} anim={AnimState()}");
        Check(Mathf.Abs(maxY - y0 - GM.config.jumpHeight) < 0.25f, "altura do salto ~ config (sem salto duplo apesar de " + jumpsWhileAir + " toques no ar)");
        Check(sawJumpAnim && P.IsGrounded, "animação de pulo e aterrissagem");
        // salto em movimento
        In.debugHorizontal = 1; await Wait(0.2f);
        float xs = P.transform.position.x; In.DebugPress("jump"); await Wait(0.3f);
        Check(!P.IsGrounded && P.transform.position.x > xs + 1f, "salto em movimento avança em X");
        await Wait(0.6f); In.debugHorizontal = 0; await Wait(0.2f);
        // abaixar
        In.debugDown = true; await Wait(0.4f);
        float h = P.GetComponent<CharacterController>().height;
        Log($"crouch h={h:F2} anim={AnimState()} hitTop={P.HitBounds.max.y - P.transform.position.y:F2}");
        Check(P.IsCrouching && Mathf.Abs(h - GM.config.crouchHeight) < 0.01f && AnimState() == "Abaixado", "abaixa e reduz o collider");
        In.debugHorizontal = 1; float xc = P.transform.position.x; await Wait(0.3f);
        Check(Mathf.Abs(P.transform.position.x - xc) < 0.01f, "não anda abaixado");
        In.debugHorizontal = 0;
        In.debugDown = false; await Wait(0.3f);
        Check(!P.IsCrouching && Mathf.Abs(P.GetComponent<CharacterController>().height - GM.config.standHeight) < 0.01f && AnimState() == "Parado", "levanta");
        // levantar sem espaço: coloca um bloco baixo sobre o policial abaixado
        In.debugDown = true; await Wait(0.2f);
        var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.layer = 8; block.transform.position = P.transform.position + new Vector3(0, 1.35f, 0); block.transform.localScale = new Vector3(1.5f, 0.3f, 1.5f);
        Physics.SyncTransforms();
        In.debugDown = false; await Wait(0.3f);
        Check(P.IsCrouching, "não levanta sem espaço acima");
        Object.Destroy(block); await Wait(0.2f);
        Check(!P.IsCrouching, "levanta quando o espaço libera");
        // limite do corredor
        In.debugHorizontal = -1; P.ResetTo(new Vector3(-46, 0, 0), -1); await Wait(1.0f);
        Log($"wall x={P.transform.position.x:F2}");
        Check(P.transform.position.x > -48.5f && P.transform.position.x < -47f, "parede lateral bloqueia");
        In.debugHorizontal = 0;
        return sb.ToString();
    }

    // ---------- Escada rolante ----------
    public static async Task<string> Escalator()
    {
        sb = new StringBuilder();
        Setup(0, 34);
        var c = L.GetUp(0, false);
        In.debugHorizontal = 1;
        bool started = await WaitUntil(() => P.CurrentMode == PoliceController.Mode.Escalator, 3f);
        In.debugHorizontal = 0;
        Check(started, "encostar na entrada inicia o transporte");
        float t0 = Time.time; float maxStep = 0; Vector3 last = P.transform.position; bool retrigger = false;
        while (P.CurrentMode == PoliceController.Mode.Escalator && Time.time - t0 < 8f)
        {
            await Awaitable.NextFrameAsync();
            maxStep = Mathf.Max(maxStep, (P.transform.position - last).magnitude); last = P.transform.position;
            if (P.CurrentMode == PoliceController.Mode.Escalator && c.TryBegin(P, 3f)) retrigger = true;
        }
        Log($"ride time={Time.time - t0:F2} maxStep={maxStep:F3} end={P.transform.position:F2} floor={P.CurrentFloor} mode={P.CurrentMode}");
        Check(Time.time - t0 > 1.5f && maxStep < 0.3f, "trajetória contínua, sem teletransporte");
        Check(!retrigger, "sem ativação repetida durante o transporte");
        Check(P.CurrentFloor == 1 && P.CurrentMode == PoliceController.Mode.Normal && Mathf.Abs(P.transform.position.z) < 0.01f, "chega ao andar 1 e devolve o controle");
        await Wait(0.3f);
        Check(P.IsGrounded && Mathf.Abs(P.transform.position.y - 4f) < 0.1f, "apoiado no piso do andar 1");
        // descida exclusiva do fugitivo: policial não é transportado
        var down = L.GetDown(1);
        P.ResetTo(down.EntryPosition + new Vector3(-1.5f, 0, 0), 1);
        In.debugHorizontal = 1; await Wait(0.8f); In.debugHorizontal = 0;
        Check(P.CurrentMode == PoliceController.Mode.Normal && P.CurrentFloor == 1, "escada de descida do fugitivo não afeta o policial");
        // terraço só por escada
        Setup(2, 34); In.debugHorizontal = 1;
        await WaitUntil(() => P.CurrentMode == PoliceController.Mode.Escalator, 3f);
        In.debugHorizontal = 0;
        await WaitUntil(() => P.CurrentMode == PoliceController.Mode.Normal, 8f);
        await Wait(0.2f);
        Check(P.CurrentFloor == 3, "escada do 2º andar leva ao terraço");
        return sb.ToString();
    }

    // ---------- Elevador ----------
    public static async Task<string> Elevator()
    {
        sb = new StringBuilder();
        Setup(0, -3);
        var el = L.elevator;
        // ciclo
        var seen = new System.Collections.Generic.List<string>();
        string lastS = "";
        float t0 = Time.time;
        while (Time.time - t0 < 14f)
        {
            string s = el.CurrentState + "@" + el.CurrentFloor;
            if (s != lastS) { seen.Add(s + $"(door={el.DoorOpenAmount:F1},y={el.cabin.position.y:F1})"); lastS = s; }
            await Awaitable.NextFrameAsync();
        }
        Log("ciclo: " + string.Join(" > ", seen));
        Check(seen.Count >= 6, "ciclo abrir/esperar/fechar/viajar");
        // embarque: espera porta aberta no térreo
        P.ResetTo(new Vector3(0.3f, 0, 0), 1);
        In.DebugPress("up"); await Wait(0.2f);
        bool closedNow = el.OpenFloor != 0;
        Check(!closedNow || P.CurrentMode == PoliceController.Mode.Normal, "não entra com porta fechada/fora do andar");
        await WaitUntil(() => el.OpenFloor == 0, 20f);
        In.DebugPress("up");
        await Wait(0.5f);
        Log($"embarque mode={P.CurrentMode} pos={P.transform.position:F2}");
        Check(P.CurrentMode == PoliceController.Mode.InElevator, "entra no elevador com porta aberta");
        In.debugHorizontal = 1; In.DebugPress("jump"); await Wait(0.3f); In.debugHorizontal = 0;
        Check(P.CurrentMode == PoliceController.Mode.InElevator && Mathf.Abs(P.transform.position.x - el.riderPoint.position.x) < 0.01f, "corrida e salto bloqueados na cabine");
        // viaja até o 2º andar
        bool reached = await WaitUntil(() => el.OpenFloor == 2, 25f);
        Log($"chegou ao 2º? {reached} y={P.transform.position.y:F2} cabinY={el.cabin.position.y:F2}");
        Check(reached && Mathf.Abs(P.transform.position.y - 8f) < 0.1f, "transportado com a cabine");
        In.DebugPress("down");
        await Wait(0.6f);
        Log($"saida mode={P.CurrentMode} pos={P.transform.position:F2} floor={P.CurrentFloor} grounded={P.IsGrounded}");
        Check(P.CurrentMode == PoliceController.Mode.Normal && P.CurrentFloor == 2 && Mathf.Abs(P.transform.position.z) < 0.01f, "sai no corredor do 2º andar com controle");
        // poço não é armadilha: anda sobre a região do elevador no andar 1 sem cabine
        P.ResetTo(new Vector3(-3, 4, 0), 1);
        In.debugHorizontal = 1; await Wait(1.0f); In.debugHorizontal = 0; await Wait(0.2f);
        Check(P.CurrentFloor == 1 && P.IsGrounded, "corredor contínuo na frente do poço (sem queda)");
        return sb.ToString();
    }

    // ---------- Obstáculos ----------
    static Obstacle Spawn(ObstacleType t, float dx, RoundDifficulty d)
    {
        var mi = typeof(ObstacleSpawner).GetMethod("Get", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var o = (Obstacle)mi.Invoke(GM.spawner, new object[] { t });
        o.Launch(P.CurrentFloor, P.transform.position.x + dx, -Mathf.Sign(dx), d);
        return o;
    }

    public static async Task<string> Obstacles()
    {
        sb = new StringBuilder();
        var d = GM.difficulty.Get(8);
        foreach (var t in new[] { ObstacleType.Cart, ObstacleType.Radio, ObstacleType.Ball })
        {
            Setup(0, -6);
            await Wait(0.2f);
            var o = Spawn(t, t == ObstacleType.Radio ? 1.2f : 4f, GM.difficulty.Get(3));
            Log($"{t} hit={o.HitBounds.size:F2} bottom={o.HitBounds.min.y:F2} top={o.HitBounds.max.y:F2}");
            if (t == ObstacleType.Radio) In.debugHorizontal = 1;
            float before = GM.TimeLeft, ts = Time.time;
            await WaitUntil(() => P.IsInvulnerable, 3f);
            In.debugHorizontal = 0;
            float lost = before - GM.TimeLeft - (Time.time - ts);
            Log($"{t}: penalidade {lost:F2}s inv={P.IsInvulnerable}");
            Check(lost > 8.8f && lost < 9.2f && P.IsInvulnerable, t + " tira 9 segundos");
            await Wait(0.5f);
            lost = before - GM.TimeLeft - (Time.time - ts);
            Check(lost < 9.2f, t + " proteção impede penalidade múltipla no mesmo contato");
            // saltar por cima (parado, obstáculo vindo / correndo até o rádio)
            Setup(0, -6); await Wait(0.3f);
            var o2 = Spawn(t, t == ObstacleType.Radio ? 3.5f : 6f, GM.difficulty.Get(3));
            before = GM.TimeLeft; ts = Time.time;
            if (t == ObstacleType.Radio || t == ObstacleType.Ball)
            {
                In.debugHorizontal = 1;
                await WaitUntil(() => o2.transform.position.x - P.transform.position.x < (t == ObstacleType.Ball ? 2.6f : 1.9f), 3f);
            }
            else
            {
                // pula parado quando o obstáculo está a (meia largura + velocidade x 0,15 s)
                float trigger = (o2.HitBounds.size.x + 0.6f) * 0.5f + GM.difficulty.Get(3).cartSpeed * 0.15f;
                await WaitUntil(() => o2.transform.position.x - P.transform.position.x < trigger, 4f);
            }
            In.DebugPress("jump");
            await Wait(1.3f);
            In.debugHorizontal = 0;
            lost = before - GM.TimeLeft - (Time.time - ts);
            Log($"{t} salto: penalidade {lost:F2}s");
            Check(lost < 0.5f, t + " pode ser saltado");
        }
        // bola alta: passar abaixado (chegando no ápice do quique)
        Setup(0, -6); await Wait(0.2f);
        In.debugDown = true; await Wait(0.2f);
        var dHigh = GM.difficulty.Get(8);
        var ball = Spawn(ObstacleType.Ball, dHigh.ballSpeed * dHigh.ballBouncePeriod * 2.5f, dHigh);
        float b0 = GM.TimeLeft, bt = Time.time;
        await WaitUntil(() => ball.transform.position.x < P.transform.position.x - 1.5f, 6f);
        float bl = b0 - GM.TimeLeft - (Time.time - bt);
        Log($"bola alta abaixado: penalidade {bl:F2}s (quique={dHigh.ballBounceHeight})");
        Check(bl < 0.5f, "bola de quique alto passa sobre o policial abaixado");
        In.debugDown = false;
        // aviao: abaixado passa por baixo
        Setup(0, -6); await Wait(0.2f);
        In.debugDown = true; await Wait(0.2f);
        int reserves = GM.Reserves;
        var plane = Spawn(ObstacleType.Plane, 6f, d);
        Log($"aviao hit bottom={plane.HitBounds.min.y:F2} top={plane.HitBounds.max.y:F2} policialAbaixadoTop={P.HitBounds.max.y:F2}");
        await WaitUntil(() => plane.transform.position.x < P.transform.position.x - 1.5f, 5f);
        Check(GM.State == GameState.Playing && GM.Reserves == reserves && !P.IsInvulnerable, "avião passa por cima do policial abaixado");
        In.debugDown = false; await Wait(0.3f);
        // aviao em pé: perde vida
        Spawn(ObstacleType.Plane, 5f, d);
        bool lost2 = await WaitUntil(() => GM.State == GameState.LifeLost, 4f);
        Log($"aviao em pé state={GM.State} msg='{GM.ui.messageText.text}'");
        Check(lost2, "avião atinge o policial em pé e tira uma vida");
        await WaitUntil(() => GM.State == GameState.Playing, 5f);
        Check(GM.Reserves == reserves - 1, $"reserva consumida e tentativa reiniciada (reservas={GM.Reserves})");
        return sb.ToString();
    }

    // ---------- Partida: captura, tempo, fuga, game over, pausa ----------
    public static async Task<string> Match()
    {
        sb = new StringBuilder();
        GM.StartNewGame();
        await Wait(0.3f);
        // captura
        GM.spawner.ClearAll();
        P.ResetTo(new Vector3(-5, 4, 0), 1);
        GM.fugitive.ResetTo(1, -2, -1); GM.fugitive.StartRunning();
        GM.DebugSetTime(32.4f);
        In.debugHorizontal = 1;
        bool won = await WaitUntil(() => GM.State == GameState.RoundWon, 4f);
        In.debugHorizontal = 0;
        Log($"captura state={GM.State} score={GM.Score} msg='{GM.ui.messageText.text.Replace("\n", " ")}'");
        Check(won && (GM.Score == 33 * 100 || GM.Score == 32 * 100), "captura soma tempo restante (arredondado) x 100");
        int scoreAfter = GM.Score;
        await Wait(0.5f);
        Check(GM.Score == scoreAfter, "rodada encerrada uma única vez");
        await WaitUntil(() => GM.State == GameState.Playing, 5f);
        Check(GM.Round == 2 && Mathf.Abs(GM.TimeLeft - GM.RoundTimeLimit) < 0.5f && Vector3.Distance(P.transform.position, GM.policeSpawn.position) < 0.5f, "próxima rodada: personagens reposicionados e cronômetro reiniciado");
        // capturar entre andares não vale
        GM.spawner.ClearAll();
        P.ResetTo(new Vector3(3, 0, 0), 1);
        GM.fugitive.ResetTo(1, 3, 1); GM.fugitive.Stop(FugitiveAI.Mode.Idle);
        GM.fugitive.StartRunning();
        await Wait(0.2f);
        Check(GM.State == GameState.Playing, "sem captura através da laje (andares diferentes)");
        // tempo esgotado
        int res = GM.Reserves;
        GM.DebugSetTime(0.3f);
        bool timeout = await WaitUntil(() => GM.State == GameState.LifeLost, 2f);
        Log($"tempo: state={GM.State} msg='{GM.ui.messageText.text}'");
        Check(timeout, "tempo esgotado tira vida");
        await WaitUntil(() => GM.State == GameState.Playing, 5f);
        Check(GM.Reserves == res - 1 && GM.Round == 2, "tentativa da rodada atual reiniciada");
        // fuga pelo terraço
        P.ResetTo(new Vector3(-15, 0, 0), 1);
        GM.fugitive.ResetTo(3, -42, -1); GM.fugitive.StartRunning();
        bool esc = await WaitUntil(() => GM.State == GameState.LifeLost, 4f);
        Log($"fuga: state={GM.State} msg='{GM.ui.messageText.text}' fug={GM.fugitive.CurrentMode}");
        Check(esc, "fugitivo escapa pelo terraço e tira vida");
        await WaitUntil(() => GM.State == GameState.Playing, 5f);
        // pausa
        GM.Pause();
        float tp = GM.TimeLeft; await Task.Delay(50);
        Check(GM.State == GameState.Paused && Time.timeScale == 0 && GM.ui.pausePanel.activeSelf, "pausa congela o jogo e mostra o menu");
        await Task.Delay(500);
        Check(Mathf.Approximately(tp, GM.TimeLeft), "cronômetro parado durante a pausa");
        GM.Resume();
        Check(GM.State == GameState.Playing && Time.timeScale == 1 && !GM.ui.pausePanel.activeSelf, "continuar retoma");
        // vida extra
        int r0 = GM.Reserves;
        typeof(GameManager).GetMethod("AddScore", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(GM, new object[] { 10000 });
        Check(GM.Reserves == Mathf.Min(r0 + 1, GM.config.maxReserveLives), $"vida extra a cada 10.000 (reservas {r0}->{GM.Reserves})");
        // game over
        while (GM.State != GameState.GameOver)
        {
            if (GM.State == GameState.Playing) GM.DebugSetTime(0.05f);
            await Wait(0.2f);
            if (Time.time > 600) break;
        }
        Log($"game over: panel={GM.ui.gameOverPanel.activeSelf} text='{GM.ui.gameOverScoreText.text.Replace("\n", " ")}'");
        Check(GM.State == GameState.GameOver && GM.ui.gameOverPanel.activeSelf, "Game Over ao acabar as vidas");
        GM.StartNewGame();
        Check(GM.State == GameState.Playing && GM.Score == 0 && GM.Reserves == GM.config.startingReserveLives, "jogar novamente reinicia");
        // pontuação por faixa
        Check(GM.config.GetScoreMultiplier(8) == 100 && GM.config.GetScoreMultiplier(9) == 200 && GM.config.GetScoreMultiplier(17) == 300, "multiplicadores 100/200/300");
        return sb.ToString();
    }

    // ---------- IA ----------
    public static async Task<string> AI()
    {
        sb = new StringBuilder();
        GM.StartNewGame();
        GM.spawner.ClearAll(); GM.spawner.SetRunning(false);
        var f = GM.fugitive;
        P.ResetTo(new Vector3(-20, 0, 0), 1);
        P.SetControlEnabled(false);
        string last = "";
        float t0 = Time.time; float maxStep = 0; Vector3 lp = f.transform.position;
        while (Time.time - t0 < 70f && GM.State == GameState.Playing)
        {
            string s = $"F{f.CurrentFloor}:{f.CurrentPlan}:{f.CurrentMode}";
            if (s != last) { Log(s + $" x={f.transform.position.x:F1}"); last = s; }
            float stp = (f.transform.position - lp).magnitude; if (stp > maxStep) { maxStep = stp; if (stp > 0.3f) Log($"salto grande {lp:F2} -> {f.transform.position:F2} mode={f.CurrentMode}"); } lp = f.transform.position;
            await Awaitable.NextFrameAsync();
        }
        Log($"fim state={GM.State} msg='{GM.ui.messageText.text}' maxStep={maxStep:F3}");
        Check(maxStep < 0.3f, "fugitivo sem teletransporte");
        P.SetControlEnabled(true);
        // reação: policial no mesmo andar bloqueando
        await WaitUntil(() => GM.State == GameState.Playing, 6f);
        GM.spawner.ClearAll();
        f.ResetTo(2, -5, 1); f.StartRunning();
        P.ResetTo(new Vector3(3, 8, 0), -1);
        await Wait(0.4f);
        Log($"bloqueio: plan={f.CurrentPlan} dir x0=-5 x={f.transform.position.x:F2}");
        Check(f.CurrentPlan == FugitiveAI.Plan.GoDown && f.transform.position.x < -5f, "inverte a direção e busca a descida exclusiva");
        return sb.ToString();
    }
}
public static class AnimCheck {
    public static async Task<string> Main() {
        var gm = GameManager.Instance; gm.StartNewGame(); gm.spawner.ClearAll();
        var f = gm.fugitive;
        var leg = f.animator.transform.Find("mixamorig:Hips/mixamorig:RightUpLeg");
        var hips = f.animator.transform.Find("mixamorig:Hips");
        float minA = 999, maxA = -999, minY = 999, maxY = -999;
        float t = 0;
        while (t < 1.5f) {
            await Awaitable.NextFrameAsync(); t += Time.deltaTime;
            float a = leg.localEulerAngles.x; if (a > 180) a -= 360;
            minA = Mathf.Min(minA, a); maxA = Mathf.Max(maxA, a);
            minY = Mathf.Min(minY, hips.localPosition.y); maxY = Mathf.Max(maxY, hips.localPosition.y);
        }
        var st = f.animator.GetCurrentAnimatorStateInfo(0);
        var feet = f.animator.transform.Find("mixamorig:Hips/mixamorig:RightUpLeg/mixamorig:RightLeg/mixamorig:RightFoot");
        return $"mode={f.CurrentMode} speed={f.Speed} policeRun={gm.config.runSpeed} animSpeed={f.animator.speed:F2} state=Correndo:{st.IsName("Correndo")} loop={st.loop} legSwing={maxA - minA:F1}deg hipsY={minY:F3}-{maxY:F3} footY={feet.position.y - f.transform.position.y:F3}";
    }
}
public static class Blocks {
    static StringBuilder sb;
    static void Check(bool ok, string w) { sb.AppendLine((ok ? "  PASS " : "  FAIL ") + w); }
    public static async Task<string> Main() {
        sb = new StringBuilder();
        var gm = GameManager.Instance; gm.StartNewGame(); gm.spawner.ClearAll(); gm.spawner.SetRunning(false);
        var L = LevelLayout.Instance; var P = gm.police; var cam = Camera.main; var follow = cam.GetComponent<CameraFollow>();
        gm.fugitive.Stop(FugitiveAI.Mode.Idle);
        float bw = L.BlockWidth;
        float viewW = cam.orthographicSize * cam.aspect * 2f;
        sb.AppendLine($"blocos={L.blockCount} largura={bw:F2} visivel={viewW:F2} aspect={cam.aspect:F2}");
        Check(Mathf.Abs(viewW - bw) < 0.05f, "a tela mostra exatamente um bloco");
        // bloco central
        P.ResetTo(new Vector3(0, 0, 0), 1); follow.SnapToTarget(); await Awaitable.WaitForSecondsAsync(0.2f);
        float camX(){ return (cam.transform.position + cam.transform.forward * follow.distance).x; }
        Check(Mathf.Abs(camX() - L.BlockCenter(3)) < 0.01f && follow.CurrentBlock == 3, $"bloco central (camX={camX():F2})");
        // anda até a borda direita: a câmera fica parada e troca ao passar
        GameInput.Instance.debugHorizontal = 1;
        float edge = L.BlockMax(3); float lastCam = camX(); int moves = 0; bool switched = false; float switchAtX = 0;
        float t = 0;
        while (t < 2.5f) {
            await Awaitable.NextFrameAsync(); t += Time.deltaTime;
            float cx = camX();
            if (Mathf.Abs(cx - lastCam) > 0.001f) { moves++; if (!switched) { switched = true; switchAtX = P.transform.position.x; } }
            lastCam = cx;
            if (P.transform.position.x > edge + 2f) break;
        }
        GameInput.Instance.debugHorizontal = 0;
        sb.AppendLine($"troca em x={switchAtX:F2} (borda {edge:F2}) movimentos da câmera={moves} camX={camX():F2}");
        Check(switched && moves == 1 && Mathf.Abs(switchAtX - edge) < 0.3f && Mathf.Abs(camX() - L.BlockCenter(4)) < 0.01f, "câmera troca de bloco de uma vez ao passar da borda");
        // contagem de blocos até a escada
        int escBlock = L.BlockIndex(L.GetUp(0, false).EntryPosition.x);
        sb.AppendLine($"escada do térreo no bloco {escBlock} (centro=3) -> {escBlock - 3} blocos para o lado");
        Check(escBlock - 3 == 3, "3 blocos para cada lado até a escada");

        // nascimento dos obstáculos
        gm.DebugSetRound(5);
        var mi = typeof(GameManager).GetMethod("BeginAttempt", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        mi.Invoke(gm, null);
        gm.fugitive.Stop(FugitiveAI.Mode.Idle);
        gm.DebugSetTime(999);
        P.ResetTo(new Vector3(L.BlockCenter(2), 0, 0), 1);
        var seen = new System.Collections.Generic.HashSet<Obstacle>();
        int spawned = 0, visibleSpawn = 0, awayFromCop = 0; string info = "";
        foreach (var o in gm.spawner.Active) seen.Add(o);
        t = 0;
        while (t < 25f) {
            await Awaitable.NextFrameAsync(); t += Time.deltaTime;
            if (P.IsInvulnerable) gm.DebugSetTime(999);
            foreach (var o in gm.spawner.Active) {
                if (o.type == ObstacleType.Radio || seen.Contains(o)) continue;
                seen.Add(o); spawned++;
                float x = o.transform.position.x, px = P.transform.position.x;
                bool off = gm.spawner.IsOffscreen(x);
                if (!off) visibleSpawn++;
                if (Mathf.Sign(px - x) != o.Direction) awayFromCop++;
                if (spawned <= 6) info += $"\n   {o.type} x={x:F1} dir={o.Direction} policial={px:F1} foraDaTela={off}";
            }
            if (gm.State != GameState.Playing) break;
        }
        sb.AppendLine($"obstáculos móveis gerados={spawned} visíveis ao nascer={visibleSpawn} indo para longe={awayFromCop}" + info);
        Check(spawned >= 3 && visibleSpawn == 0, "obstáculos nascem fora da tela");
        Check(awayFromCop == 0, "obstáculos entram vindo em direção ao policial");
        gm.spawner.SetRunning(false);
        return sb.ToString();
    }

    /// <summary>Entra em blocos e verifica que o obstáculo nasce na ponta oposta, vindo em direção ao policial, sem emboscada na entrada.</summary>
    public static async Task<string> Entry() {
        sb = new StringBuilder();
        var gm = GameManager.Instance; var L = LevelLayout.Instance; var P = gm.police; var In = GameInput.Instance;
        gm.DebugSetRound(2);
        typeof(GameManager).GetMethod("BeginAttempt", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(gm, null);
        gm.fugitive.Stop(FugitiveAI.Mode.Idle);
        gm.DebugSetTime(999);
        int entries = 0, ambushHits = 0, firstSpawnOk = 0, firstSpawnChecked = 0, leftovers = 0;
        // atravessa os blocos 1 -> 5 no térreo, alternando sentido para testar os dois lados
        foreach (var dirRun in new[] { 1f, -1f })
        {
            int startBlock = dirRun > 0 ? 1 : 5;
            P.ResetTo(new Vector3(L.BlockCenter(startBlock), 0, 0), dirRun);
            await Awaitable.WaitForSecondsAsync(0.3f);
            In.debugHorizontal = dirRun;
            int lastBlock = L.BlockIndex(P.transform.position.x);
            float t = 0;
            while (t < 6.5f)
            {
                await Awaitable.NextFrameAsync(); t += Time.deltaTime;
                if (gm.State != GameState.Playing) break;
                gm.DebugSetTime(999);
                int bnow = L.BlockIndex(P.transform.position.x);
                if (bnow != lastBlock && !P.IsTransporting && P.CurrentMode == PoliceController.Mode.Normal)
                {
                    lastBlock = bnow; entries++;
                    In.debugHorizontal = 0; // para logo após entrar, para ver de onde vem o obstáculo
                    await Awaitable.NextFrameAsync();
                    foreach (var o in gm.spawner.Active) if (o.type != ObstacleType.Radio) leftovers++;
                    float te = 0; bool hit = false; bool wasInv = P.IsInvulnerable; Obstacle first = null;
                    while (te < 1.2f) { await Awaitable.NextFrameAsync(); te += Time.deltaTime; gm.DebugSetTime(999); if (!P.IsInvulnerable) wasInv = false; else if (!wasInv && te < 1.0f) hit = true; foreach (var o in gm.spawner.Active) if (o.type != ObstacleType.Radio && first == null) first = o; }
                    if (hit) ambushHits++;
                    if (first)
                    {
                        firstSpawnChecked++;
                        float fx = first.transform.position.x, px = P.transform.position.x;
                        bool farSide = Mathf.Sign(fx - px) == dirRun; // vem da ponta à frente (oposta à entrada)
                        bool toward = Mathf.Sign(px - fx) == first.Direction;
                        if (farSide && toward && Mathf.Abs(fx - px) > 6f) firstSpawnOk++;
                        sb.AppendLine($"  bloco {bnow}: entrou em x={px:F1}, {first.type} em x={fx:F1} dir={first.Direction} (borda oposta {(dirRun > 0 ? L.BlockMax(bnow) : L.BlockMin(bnow)):F1}) atingido={hit}");
                    }
                    // espera o obstáculo passar (pula) e segue
                    In.debugHorizontal = dirRun;
                }
                if (P.IsInvulnerable) { }
                if (first_jump(P, gm)) In.DebugPress("jump");
            }
            In.debugHorizontal = 0;
        }
        sb.Insert(0, $"entradas={entries} atingido ao entrar={ambushHits} obstáculos do bloco anterior presentes={leftovers} primeiro obstáculo na ponta oposta={firstSpawnOk}/{firstSpawnChecked}\n");
        Check(entries >= 4, "atravessou vários blocos nos dois sentidos");
        Check(leftovers == 0, "obstáculos do bloco anterior são removidos ao entrar");
        Check(ambushHits == 0, "não é atingido logo ao entrar no bloco");
        Check(firstSpawnChecked >= 3 && firstSpawnOk == firstSpawnChecked, "obstáculo nasce na ponta oposta e vem em direção ao policial");
        return sb.ToString();
    }

    // salta automaticamente quando um obstáculo baixo se aproxima (para seguir atravessando blocos)
    static bool first_jump(PoliceController P, GameManager gm)
    {
        if (!P.IsGrounded) return false;
        foreach (var o in gm.spawner.Active)
        {
            if (o.Floor != P.CurrentFloor || o.type == ObstacleType.Plane) continue;
            float dx = o.transform.position.x - P.transform.position.x;
            if (Mathf.Abs(dx) < 2.2f && Mathf.Sign(dx) == P.Facing) return true;
        }
        return false;
    }

    public static async Task<string> Chase() {
        sb = new StringBuilder();
        var gm = GameManager.Instance; gm.StartNewGame(); gm.spawner.ClearAll(); gm.spawner.SetRunning(false);
        var P = gm.police; var f = gm.fugitive;
        sb.AppendLine($"policial={gm.config.runSpeed} fugitivo={f.Speed} ({f.Speed / gm.config.runSpeed:P0})");
        Check(f.Speed <= gm.config.runSpeed * 0.8f + 0.001f, "fugitivo bem mais lento que o policial");
        f.ResetTo(2, -20, 1); f.StartRunning();
        P.ResetTo(new Vector3(-32, 8, 0), 1);
        GameInput.Instance.debugHorizontal = 1;
        float t0 = Time.time;
        bool won = false; float tt = 0;
        while (tt < 15f) { await Awaitable.NextFrameAsync(); tt += Time.deltaTime; if (gm.State == GameState.RoundWon) { won = true; break; } }
        GameInput.Instance.debugHorizontal = 0;
        sb.AppendLine($"perseguição de 12 m no mesmo andar: capturou={won} em {tt:F1}s plano={f.CurrentPlan}");
        Check(won, "policial alcança o fugitivo correndo");
        return sb.ToString();
    }
}
public static class Timing {
    static StringBuilder sb;
    static void Check(bool ok, string w) { sb.AppendLine((ok ? "  PASS " : "  FAIL ") + w); }
    static MethodInfoHolder H = new MethodInfoHolder();
    class MethodInfoHolder { public System.Reflection.MethodInfo begin = typeof(GameManager).GetMethod("BeginAttempt", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance); }

    public static string Table() {
        sb = new StringBuilder();
        var gm = GameManager.Instance; var cop = gm.config.runSpeed;
        bool allSlower = true, allFit = true;
        sb.AppendLine($"policial corre {cop} m/s, escada {gm.config.escalatorSpeed} | fugitivo escada {gm.config.FugitiveEscalatorSpeed}");
        foreach (int r in new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 12, 16, 20, 30, 60 }) {
            gm.DebugSetRound(r); H.begin.Invoke(gm, null);
            float sp = gm.fugitive.Speed;
            if (sp >= cop) allSlower = false;
            if (gm.EscapeTimeEstimate >= gm.RoundTimeLimit) allFit = false;
            sb.AppendLine($"  rodada {r,2}: config={gm.config.fugitiveSpeed:F2} efetiva={sp:F2} ({sp / cop:P0} do policial) fuga≈{gm.EscapeTimeEstimate:F1}s cronômetro={gm.RoundTimeLimit:F0}s");
        }
        Check(allSlower, "fugitivo sempre mais lento que o policial, em qualquer rodada");
        Check(gm.config.FugitiveEscalatorSpeed <= gm.config.escalatorSpeed, "fugitivo não sobe a escada mais rápido que o policial");
        Check(allFit, "cronômetro sempre maior que o tempo de fuga do fugitivo");
        gm.fugitive.Speed = 999f;
        Check(gm.fugitive.Speed <= gm.config.MaxFugitiveSpeed, $"trava de velocidade (pedido 999 -> {gm.fugitive.Speed})");
        gm.StartNewGame();
        return sb.ToString();
    }

    public static async Task<string> Escape(int round) {
        sb = new StringBuilder();
        var gm = GameManager.Instance;
        gm.StartNewGame(); // cancela transições pendentes (perda de vida/rodada) de testes anteriores
        gm.DebugSetRound(round); H.begin.Invoke(gm, null);
        gm.spawner.ClearAll(); gm.spawner.SetRunning(false);
        gm.police.SetControlEnabled(false);
        float limit = gm.RoundTimeLimit, est = gm.EscapeTimeEstimate, t0 = Time.time;
        while (gm.State == GameState.Playing && Time.time - t0 < limit + 5f) await Awaitable.NextFrameAsync();
        float took = Time.time - t0;
        sb.AppendLine($"rodada {round}: fugitivo {gm.fugitive.Speed} m/s, estimado {est:F1}s, cronômetro {limit:F0}s -> resultado '{gm.ui.messageText.text}' em {took:F1}s (sobrou {gm.TimeLeft:F1}s)");
        Check(gm.ui.messageText.text.Contains("ESCAPOU"), "fugitivo consegue chegar ao ponto de fuga antes do tempo acabar");
        gm.police.SetControlEnabled(true);
        return sb.ToString();
    }
    public static Task<string> Escape1() => Escape(1);
    public static Task<string> Escape8() => Escape(8);
}

// ---------- Ajustes: elevador começa no 2º andar, fugitivo com velocidade fixa, carrinhos colados ----------
public static class Ajustes {
    static StringBuilder sb;
    static GameManager GM => GameManager.Instance;
    static PoliceController P => GM.police;
    static GameInput In => GameInput.Instance;
    static void Check(bool ok, string w) { sb.AppendLine((ok ? "  PASS " : "  FAIL ") + w); Debug.Log("[TESTE] " + (ok ? "PASS " : "FAIL ") + w); }
    static async Task<bool> WaitUntil(System.Func<bool> cond, float timeout) {
        float t = 0;
        while (!cond() && t < timeout) { await Awaitable.NextFrameAsync(); t += Time.deltaTime; }
        return cond();
    }
    static readonly System.Reflection.MethodInfo begin = typeof(GameManager).GetMethod("BeginAttempt", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    static readonly System.Reflection.MethodInfo get = typeof(ObstacleSpawner).GetMethod("Get", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

    public static async Task<string> Elevador() {
        sb = new StringBuilder();
        var el = LevelLayout.Instance.elevator;
        GM.StartNewGame();
        GM.spawner.ClearAll(); GM.spawner.enabled = false;
        Check(el.CurrentFloor == 1 && el.DoorOpenAmount == 0f, $"elevador começa no 2º andar com as portas fechadas (andar {el.CurrentFloor})");
        float t0 = Time.time, abriu = -1f, fechou = -1f, chegou = -1f;
        In.debugHorizontal = 1;
        bool entrou = false;
        while (Time.time - t0 < 10f && !entrou) {
            if (abriu < 0 && el.OpenFloor == 0) abriu = Time.time - t0;
            if (abriu >= 0 && fechou < 0 && el.OpenFloor != 0) fechou = Time.time - t0;
            if (chegou < 0 && Mathf.Abs(P.transform.position.x - el.cabin.position.x) < 0.4f) { chegou = Time.time - t0; In.debugHorizontal = 0; }
            if (chegou >= 0 && el.OpenFloor == 0) In.DebugPress("up");
            await Awaitable.NextFrameAsync();
            entrou = P.CurrentMode == PoliceController.Mode.InElevator;
        }
        In.debugHorizontal = 0;
        sb.AppendLine($"  porta aberta no térreo em {abriu:F1}s, policial chegou em {chegou:F1}s, porta fechou em {(fechou < 0 ? "-" : fechou.ToString("F1"))}s");
        Check(entrou, "correndo direto do início, o policial pega o elevador no térreo");
        bool subiu = await WaitUntil(() => el.OpenFloor == 1, 12f);
        Check(subiu && P.CurrentMode == PoliceController.Mode.InElevator, "e sobe com a cabine até o 2º andar");
        GM.spawner.enabled = true;
        GM.StartNewGame();
        return sb.ToString();
    }

    public static string Fugitivo() {
        sb = new StringBuilder();
        var vs = new System.Collections.Generic.List<float>();
        foreach (int r in new[] { 1, 2, 4, 8, 12, 30 }) { GM.DebugSetRound(r); begin.Invoke(GM, null); vs.Add(GM.fugitive.Speed); }
        sb.AppendLine("  velocidades: " + string.Join(", ", vs));
        Check(vs.TrueForAll(v => Mathf.Approximately(v, GM.config.fugitiveSpeed)) && Mathf.Approximately(GM.config.fugitiveSpeed, 4f), "fugitivo com a mesma velocidade (4 m/s, a da rodada 1) em todas as rodadas");
        GM.StartNewGame();
        return sb.ToString();
    }

    // Policial correndo em direção a uma fila de carrinhos da mesma onda: um único pulo passa por todos.
    static async Task<bool> PularFila(int rodada, int qtd) {
        GM.StartNewGame();
        GM.spawner.ClearAll(); GM.spawner.enabled = false;
        GM.fugitive.Stop(FugitiveAI.Mode.Idle);
        GM.fugitive.ResetTo(3, -10, -1);
        P.ResetTo(new Vector3(-40, 0, 0), 1);
        GM.DebugSetTime(50);
        await Awaitable.NextFrameAsync();
        var d = GM.difficulty.Get(rodada);
        var carts = new System.Collections.Generic.List<Obstacle>();
        for (int i = 0; i < qtd; i++) {
            var o = (Obstacle)get.Invoke(GM.spawner, new object[] { ObstacleType.Cart });
            o.Launch(0, P.transform.position.x + 18f + i * d.cartWaveSpacing, -1f, d);
            carts.Add(o);
        }
        Physics.SyncTransforms();
        var cfg = GM.config;
        float top = carts[0].HitBounds.max.y, halfSum = (carts[0].HitBounds.size.x + 2f * cfg.capsuleRadius) * 0.5f;
        float v = cfg.JumpVelocity, g = cfg.gravity, disc = Mathf.Sqrt(v * v - 2f * g * top);
        float t1 = (v - disc) / g, t2 = (v + disc) / g, vRel = cfg.runSpeed + d.cartSpeed;
        float lo = halfSum + vRel * t1, hi = vRel * t2 - halfSum - (qtd - 1) * d.cartWaveSpacing;
        sb.AppendLine($"  rodada {rodada}, {qtd} carrinhos a {d.cartWaveSpacing} m: pular com o 1º entre {lo:F2} e {hi:F2} m (folga {hi - lo:F2} m)");
        float trigger = (lo + hi) * 0.5f;
        In.debugHorizontal = 1;
        await WaitUntil(() => carts[0].transform.position.x - P.transform.position.x < trigger, 5f);
        float before = GM.TimeLeft, ts = Time.time;
        In.DebugPress("jump");
        await WaitUntil(() => carts[qtd - 1].transform.position.x < P.transform.position.x - 1.5f, 3f);
        await Awaitable.NextFrameAsync();
        In.debugHorizontal = 0;
        float lost = before - GM.TimeLeft - (Time.time - ts);
        GM.spawner.ClearAll(); GM.spawner.enabled = true;
        return hi > lo && lost < 0.5f && !P.IsInvulnerable;
    }

    public static async Task<string> Carrinhos() {
        sb = new StringBuilder();
        Check(await PularFila(1, 2), "rodada 1: dois carrinhos seguidos, um único pulo");
        Check(await PularFila(3, 2), "rodada 3: dois carrinhos seguidos, um único pulo");
        Check(await PularFila(8, 3), "rodada 8: três carrinhos seguidos, um único pulo");
        GM.StartNewGame();
        return sb.ToString();
    }
}

// ---------- Sons: corrida em loop, pulo e item ----------
public static class Sons {
    static StringBuilder sb;
    static void Check(bool ok, string w) { sb.AppendLine((ok ? "  PASS " : "  FAIL ") + w); Debug.Log("[TESTE] " + (ok ? "PASS " : "FAIL ") + w); }
    public static async Task<string> Main() {
        sb = new StringBuilder();
        var gm = GameManager.Instance; var P = gm.police; var In = GameInput.Instance;
        var ga = GameAudio.Instance;
        Check(ga && ga.runLoop && ga.jump && ga.itemPickup, "GameAudio na cena com os três clipes");
        var srcs = ga.GetComponents<AudioSource>();
        AudioSource loop = System.Array.Find(srcs, s => s.loop), sfx = System.Array.Find(srcs, s => !s.loop);
        gm.StartNewGame(); gm.spawner.ClearAll(); gm.spawner.enabled = false;
        gm.fugitive.Stop(FugitiveAI.Mode.Idle); gm.fugitive.ResetTo(3, -10, -1);
        P.ResetTo(new Vector3(-30, 0, 0), 1);
        await Awaitable.WaitForSecondsAsync(0.3f);
        Check(!loop.isPlaying, "parado: sem som de passos");
        In.debugHorizontal = 1; await Awaitable.WaitForSecondsAsync(0.4f);
        Check(loop.isPlaying && loop.clip == ga.runLoop, "correndo: som andando.mp3 em loop");
        In.debugHorizontal = 0; await Awaitable.WaitForSecondsAsync(0.2f);
        Check(!loop.isPlaying, "parou de correr: passos param");
        In.DebugPress("jump"); await Awaitable.NextFrameAsync(); await Awaitable.NextFrameAsync();
        Check(sfx.isPlaying, "pulo: toca pulo.mp3");
        await Awaitable.WaitForSecondsAsync(1.2f);
        var c = gm.spawner.Collectibles.Count > 0 ? gm.spawner.Collectibles[0] : null;
        Check(c != null, $"há itens coletáveis na fase ({gm.spawner.Collectibles.Count})");
        if (c != null) {
            sfx.Stop();
            int pts = gm.Score;
            c.Place(P.CurrentFloor, P.transform.position + Vector3.up * 0.8f);
            await Awaitable.NextFrameAsync(); await Awaitable.NextFrameAsync();
            Check(!c.Active && gm.Score > pts && sfx.isPlaying, "pegar item: toca getItens.mp3 e soma pontos");
        }
        In.debugHorizontal = 1; await Awaitable.WaitForSecondsAsync(0.3f);
        gm.Pause(); await Task.Delay(300);
        Check(!loop.isPlaying, "pausado: passos param");
        gm.Resume(); In.debugHorizontal = 0;
        gm.spawner.enabled = true; gm.StartNewGame();
        return sb.ToString();
    }
}
