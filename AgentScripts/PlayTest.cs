using System.Linq;
using System.Text;
using KeystoneKapers;
using UnityEngine;

public static class PlayTest
{
    static GameManager GM => GameManager.Instance;

    public static string Status()
    {
        var gm = GM;
        if (!gm) return "no GameManager (not playing?)";
        var sb = new StringBuilder();
        var p = gm.police; var f = gm.fugitive; var el = LevelLayout.Instance.elevator;
        sb.AppendLine($"state={gm.State} round={gm.Round} score={gm.Score} reserves={gm.Reserves} time={gm.TimeLeft:F2} timeScale={Time.timeScale}");
        sb.AppendLine($"police pos={p.transform.position:F2} floor={p.CurrentFloor} mode={p.CurrentMode} grounded={p.IsGrounded} crouch={p.IsCrouching} inv={p.IsInvulnerable} facing={p.Facing} ccH={p.GetComponent<CharacterController>().height:F2}");
        var an = p.animator; var st = an.GetCurrentAnimatorStateInfo(0);
        string sname = new[] { "Parado", "Correndo", "Pulando", "Abaixado" }.FirstOrDefault(n => st.IsName(n)) ?? "?";
        sb.AppendLine($"police anim={sname} t={st.normalizedTime:F2} speedP={an.GetFloat("Speed")} groundedP={an.GetBool("Grounded")} crouchP={an.GetBool("Crouch")} visualYaw={p.visualPivot.localEulerAngles.y:F0}");
        sb.AppendLine($"fugitive pos={f.transform.position:F2} floor={f.CurrentFloor} mode={f.CurrentMode} plan={f.CurrentPlan} speed={f.Speed}");
        sb.AppendLine($"elevator state={el.CurrentState} floor={el.CurrentFloor} door={el.DoorOpenAmount:F2} cabinY={el.cabin.position.y:F2} rider={el.HasRider}");
        foreach (var o in gm.spawner.Active) sb.AppendLine($"  obst {o.type} floor={o.Floor} pos={o.transform.position:F2} dir={o.Direction} hit={o.HitBounds.min:F2}-{o.HitBounds.max:F2}");
        foreach (var c in gm.spawner.Collectibles) if (c.Active) sb.AppendLine($"  coll floor={c.Floor} pos={c.transform.position:F2}");
        var cam = Camera.main; sb.AppendLine($"camera pos={cam.transform.position:F2}");
        if (gm.ui) sb.AppendLine($"ui msg='{gm.ui.messageText.text}' pen='{gm.ui.penaltyText.text}' timer='{gm.ui.timerText.text}' title={gm.ui.titlePanel.activeSelf} pause={gm.ui.pausePanel.activeSelf} over={gm.ui.gameOverPanel.activeSelf}");
        return sb.ToString();
    }

    public static string Start() { GM.StartNewGame(); return Status(); }
    public static string Input(float h, bool down) { GameInput.Instance.debugHorizontal = h; GameInput.Instance.debugDown = down; return "ok"; }
    public static string TeleportPolice(float x, int floor)
    {
        var l = LevelLayout.Instance;
        GM.police.ResetTo(new Vector3(x, l.FloorY(floor), l.playZ), 1f);
        return Status();
    }
    public static string TeleportFugitive(float x, int floor, float facing)
    {
        GM.fugitive.ResetTo(floor, x, facing);
        GM.fugitive.StartRunning();
        return Status();
    }
    public static string FreezeFugitive() { GM.fugitive.Stop(FugitiveAI.Mode.Idle); return "frozen"; }
    public static string SetTime(float t) { GM.DebugSetTime(t); return Status(); }
    public static string SetRound(int r) { GM.DebugSetRound(r); return "round " + r; }
    public static string SetTimeScale(float s) { Time.timeScale = s; return "ts " + s; }

    /// <summary>Coloca um obstáculo no andar do policial a uma distância dx, indo em direção a ele.</summary>
    public static string SpawnObstacle(string type, float dx)
    {
        var gm = GM; var p = gm.police;
        var t = (ObstacleType)System.Enum.Parse(typeof(ObstacleType), type);
        var prefab = t == ObstacleType.Cart ? gm.spawner.cartPrefab : t == ObstacleType.Ball ? gm.spawner.ballPrefab : t == ObstacleType.Plane ? gm.spawner.planePrefab : gm.spawner.radioPrefab;
        // usa o próprio spawner via reflexão para entrar no pool/lista ativa
        var mi = typeof(ObstacleSpawner).GetMethod("Get", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var o = (Obstacle)mi.Invoke(gm.spawner, new object[] { t });
        o.Launch(p.CurrentFloor, p.transform.position.x + dx, -Mathf.Sign(dx), gm.CurrentDifficulty);
        return $"spawned {t} at {o.transform.position:F2} hit={o.HitBounds.min:F2}-{o.HitBounds.max:F2} police={p.HitBounds.min:F2}-{p.HitBounds.max:F2}";
    }

    public static string StopSpawner() { GM.spawner.SetRunning(false); return "spawner off"; }
    public static string ClearObstacles() { GM.spawner.ClearAll(); return "cleared"; }
    public static string Difficulty(int round)
    {
        var d = GM.difficulty.Get(round);
        return JsonUtility.ToJson(d);
    }
}
