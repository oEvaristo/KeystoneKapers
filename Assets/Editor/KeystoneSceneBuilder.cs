using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace KeystoneKapers.EditorTools
{
    /// <summary>
    /// Monta a cena principal (loja de departamentos) a partir dos prefabs existentes e de primitivos.
    /// Menu: Keystone Kapers/Construir Cena Principal. Não altera os modelos, clips ou prefabs originais.
    /// </summary>
    public static class KeystoneSceneBuilder
    {
        // ---- Caminhos ----
        const string ScenePath = "Assets/Scenes/LojaDepartamentos.unity";
        const string AnimDir = "Assets/Animations";
        const string MatDir = "Assets/Materials";
        const string ConfigDir = "Assets/Config";
        const string GameplayPrefabDir = "Assets/Prefabs/Gameplay";

        const string PolicialPrefab = "Assets/Prefabs/Policial/Policial.prefab";
        const string FugitivoPrefab = "Assets/Prefabs/Fugitivo/Fugitivo.prefab";
        const string ElevadorPrefab = "Assets/Prefabs/Cenario/Elevador.prefab";
        const string EscadaPrefab = "Assets/Prefabs/Cenario/escadaRolante.prefab";
        const string CarrinhoPrefab = "Assets/Prefabs/Inimigos/carrinhoCompra.prefab";
        const string BolaPrefab = "Assets/Prefabs/Inimigos/Bola.prefab";
        const string RadioPrefab = "Assets/Prefabs/Inimigos/radioAntigo.prefab";
        const string AviaoPrefab = "Assets/Prefabs/Inimigos/aviaoBrinquedo.prefab";

        // ---- Layers ----
        public const int GroundLayer = 8, PlayerLayer = 9, FugitiveLayer = 10, ObstacleLayer = 11;

        // ---- Dimensões da loja ----
        const float FloorSpacing = 4f;
        const int FloorCount = 4; // 3 internos + terraço
        const float HalfLength = 48f; // ~3 telas de cada lado do centro até as escadas
        const float SlabThickness = 0.3f;
        const float FrontZ = -1.5f, BackZ = 5.6f, BackZoneZ = 0.5f;
        const float EscUpZ = 1.75f, EscDownZ = 4.2f;
        const float EscLowX = 39f;
        const float ElevatorZ = 2.3f;
        const float CharacterScale = 1.6f;
        const float ElevatorScale = 1.75f;
        static readonly int[] PairSide = { +1, -1, +1 }; // extremidade da escada entre andar i e i+1

        static Font font;
        static AnimatorController policeController, fugitiveController;
        static Material matFloor, matFloorAlt, matWall, matWallAlt, matTrim, matDark, matShaft, matGreen, matSignBg, matBrown, matMoney, matTerrace, matEscDown, matRail, matShelf;

        [MenuItem("Keystone Kapers/Construir Cena Principal")]
        public static void BuildMenu() { Debug.Log(Build()); }

        public static string Build()
        {
            var log = new System.Text.StringBuilder();
            EnsureFolders();
            EnsureLayers();
            PlayerSettings.runInBackground = true;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            CreateMaterials();
            var config = CreateConfig();
            var difficulty = CreateDifficulty();
            var policeCtrl = CreatePoliceAnimator();
            var fugitiveCtrl = CreateFugitiveAnimator();
            var policePrefab = CreatePolicePrefab(policeCtrl, config);
            var fugitivePrefab = CreateFugitivePrefab(fugitiveCtrl, config);
            var obstacles = CreateObstaclePrefabs();
            var collectibles = CreateCollectiblePrefabs();
            AssetDatabase.SaveAssets();

            BuildScene(config, difficulty, policePrefab, fugitivePrefab, obstacles, collectibles, log);
            return log.ToString();
        }

        // =====================================================================
        // Pastas / layers / materiais
        // =====================================================================
        static void EnsureFolders()
        {
            foreach (var p in new[] { AnimDir, MatDir, ConfigDir, GameplayPrefabDir })
            {
                if (AssetDatabase.IsValidFolder(p)) continue;
                var parent = Path.GetDirectoryName(p).Replace('\\', '/');
                AssetDatabase.CreateFolder(parent, Path.GetFileName(p));
            }
        }

        static void EnsureLayers()
        {
            var tm = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tm.FindProperty("layers");
            void Set(int i, string n) { var p = layers.GetArrayElementAtIndex(i); if (string.IsNullOrEmpty(p.stringValue)) p.stringValue = n; }
            Set(GroundLayer, "Ground");
            Set(PlayerLayer, "Player");
            Set(FugitiveLayer, "Fugitive");
            Set(ObstacleLayer, "Obstacle");
            tm.ApplyModifiedPropertiesWithoutUndo();
        }

        static Material Mat(string name, Color c, float smooth = 0.3f, float metal = 0f, Color? emission = null)
        {
            string path = MatDir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metal);
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        static void CreateMaterials()
        {
            matFloor = Mat("M_Piso", new Color(0.78f, 0.72f, 0.62f), 0.45f);
            matFloorAlt = Mat("M_PisoAlt", new Color(0.62f, 0.66f, 0.72f), 0.45f);
            matWall = Mat("M_Parede", new Color(0.86f, 0.80f, 0.70f), 0.1f);
            matWallAlt = Mat("M_ParedeAlt", new Color(0.70f, 0.78f, 0.84f), 0.1f);
            matTrim = Mat("M_Acabamento", new Color(0.75f, 0.15f, 0.12f), 0.5f);
            matDark = Mat("M_Escuro", new Color(0.18f, 0.18f, 0.2f), 0.2f);
            matShaft = Mat("M_PocoElevador", new Color(0.32f, 0.32f, 0.36f), 0.4f, 0.4f);
            matGreen = Mat("M_SaidaFuga", new Color(0.1f, 0.85f, 0.25f), 0.3f, 0f, new Color(0.05f, 0.6f, 0.15f));
            matSignBg = Mat("M_Placa", new Color(0.12f, 0.2f, 0.45f), 0.3f);
            matBrown = Mat("M_Maleta", new Color(0.45f, 0.27f, 0.12f), 0.4f);
            matMoney = Mat("M_SacoDinheiro", new Color(0.35f, 0.6f, 0.25f), 0.2f);
            matTerrace = Mat("M_Terraco", new Color(0.5f, 0.5f, 0.5f), 0.15f);
            matRail = Mat("M_Corrimao", new Color(0.8f, 0.82f, 0.85f), 0.8f, 0.8f);
            matShelf = Mat("M_Prateleira", new Color(0.55f, 0.4f, 0.26f), 0.3f);
            var escSrc = AssetDatabase.LoadAssetAtPath<Material>("Assets/Modelos/Cenarios/Escada Rolante/escadaRolante.mat");
            string escDownPath = MatDir + "/M_EscadaDescidaFugitivo.mat";
            matEscDown = AssetDatabase.LoadAssetAtPath<Material>(escDownPath);
            if (!matEscDown && escSrc) { matEscDown = new Material(escSrc); AssetDatabase.CreateAsset(matEscDown, escDownPath); }
            if (matEscDown)
            {
                if (matEscDown.HasProperty("_BaseColor")) matEscDown.SetColor("_BaseColor", new Color(1f, 0.62f, 0.35f));
                EditorUtility.SetDirty(matEscDown);
            }
        }

        // =====================================================================
        // Configurações
        // =====================================================================
        /// <summary>Sons de corrida, pulo e item (Assets/Audios). Também usado para atualizar a cena sem reconstruí-la.</summary>
        public static GameAudio AddGameAudio(GameObject managers)
        {
            var audio = managers.GetComponent<GameAudio>();
            if (!audio) audio = managers.AddComponent<GameAudio>();
            audio.runLoop = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audios/andando.mp3");
            audio.jump = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audios/pulo.mp3");
            audio.itemPickup = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audios/getItens.mp3");
            EditorUtility.SetDirty(audio);
            return audio;
        }

        static GameConfig CreateConfig()
        {
            string path = ConfigDir + "/GameConfig.asset";
            var c = AssetDatabase.LoadAssetAtPath<GameConfig>(path);
            if (!c) { c = ScriptableObject.CreateInstance<GameConfig>(); AssetDatabase.CreateAsset(c, path); }
            return c;
        }

        static DifficultySettings CreateDifficulty()
        {
            string path = ConfigDir + "/DifficultySettings.asset";
            var d = AssetDatabase.LoadAssetAtPath<DifficultySettings>(path);
            if (d) return d;
            d = ScriptableObject.CreateInstance<DifficultySettings>();
            RoundDifficulty R(string label, bool balls, bool planes, int radios, int perWave, float interval, float cart, float ball, float plane, float bounce, float period, int coll)
                => new RoundDifficulty { label = label, carts = true, balls = balls, planes = planes, radiosPerFloor = radios, obstaclesPerWave = perWave, waveInterval = interval, waveSpacing = 3.5f, cartWaveSpacing = 1.5f, cartSpeed = cart, ballSpeed = ball, planeSpeed = plane, ballBounceHeight = bounce, ballBouncePeriod = period, collectibles = coll };
            d.rounds = new List<RoundDifficulty>
            {
                R("1 - Carrinhos", false, false, 0, 1, 5.5f, 4.0f, 3.2f, 5.0f, 0.35f, 1.1f, 2),
                R("2 - Carrinhos + rádios", false, false, 1, 1, 5.0f, 4.3f, 3.2f, 5.0f, 0.35f, 1.1f, 2),
                R("3 - Bolas baixas", true,  false, 1, 2, 5.0f, 4.6f, 3.4f, 5.0f, 0.35f, 1.1f, 3),
                R("4 - Aviões", true,  true,  1, 2, 4.6f, 4.8f, 3.6f, 5.4f, 0.45f, 1.1f, 3),
                R("5 - Ondas maiores", true,  true,  1, 2, 4.3f, 5.2f, 3.8f, 5.8f, 0.6f,  1.2f, 3),
                R("6 - Bolas altas", true,  true,  2, 3, 4.0f, 5.5f, 3.8f, 6.2f, 1.9f,  1.5f, 3),
                R("7 - Rápido", true,  true,  2, 3, 3.7f, 6.0f, 4.0f, 6.7f, 2.0f,  1.5f, 3),
                R("8 - Muito rápido", true,  true,  2, 3, 3.4f, 6.5f, 4.2f, 7.2f, 2.1f,  1.5f, 3),
            };
            AssetDatabase.CreateAsset(d, path);
            return d;
        }

        // =====================================================================
        // Animações (cópias dos clips originais, com loop) e Animator Controllers
        // =====================================================================
        static AnimationClip SourceClip(string fbx)
        {
            return AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview"));
        }

        static AnimationClip CopyClip(string fbx, string newName, bool loop, bool flattenHipsY, float hipsYOffset = 0f)
        {
            string path = AnimDir + "/" + newName + ".anim";
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            var src = SourceClip(fbx);
            var copy = Object.Instantiate(src);
            copy.name = newName;
            if (flattenHipsY)
            {
                var b = EditorCurveBinding.FloatCurve("mixamorig:Hips", typeof(Transform), "m_LocalPosition.y");
                var c = AnimationUtility.GetEditorCurve(copy, b);
                if (c != null)
                {
                    float baseY = c.Evaluate(0f);
                    var keys = c.keys;
                    for (int i = 0; i < keys.Length; i++) { keys[i].value = Mathf.Min(keys[i].value, baseY); keys[i].inTangent = keys[i].outTangent = 0f; }
                    AnimationUtility.SetEditorCurve(copy, b, new AnimationCurve(keys));
                }
            }
            if (Mathf.Abs(hipsYOffset) > 0f)
            {
                var b = EditorCurveBinding.FloatCurve("mixamorig:Hips", typeof(Transform), "m_LocalPosition.y");
                var c = AnimationUtility.GetEditorCurve(copy, b);
                if (c != null)
                {
                    var keys = c.keys;
                    for (int i = 0; i < keys.Length; i++) keys[i].value += hipsYOffset;
                    AnimationUtility.SetEditorCurve(copy, b, new AnimationCurve(keys));
                }
            }
            var s = AnimationUtility.GetAnimationClipSettings(copy);
            s.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(copy, s);
            if (existing)
            {
                // Mantém o GUID do asset para não quebrar referências existentes.
                EditorUtility.CopySerialized(copy, existing);
                existing.name = newName;
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(copy);
                return existing;
            }
            AssetDatabase.CreateAsset(copy, path);
            return copy;
        }

        /// <summary>Carrega o controller existente (mesmo GUID) e o limpa, ou cria um novo.</summary>
        static AnimatorController GetCleanController(string path)
        {
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (!ctrl) return AnimatorController.CreateAnimatorControllerAtPath(path);
            foreach (var p in ctrl.parameters) ctrl.RemoveParameter(p);
            var sm = ctrl.layers[0].stateMachine;
            foreach (var st in sm.states.ToArray()) sm.RemoveState(st.state);
            foreach (var t in sm.anyStateTransitions.ToArray()) sm.RemoveAnyStateTransition(t);
            return ctrl;
        }

        static AnimatorStateTransition Tr(AnimatorState from, AnimatorState to, float dur)
        {
            var t = from.AddTransition(to);
            t.hasExitTime = false;
            t.duration = dur;
            t.canTransitionToSelf = false;
            return t;
        }

        static AnimatorController CreatePoliceAnimator()
        {
            var idle = CopyClip("Assets/Modelos/Policial/Policial.fbx", "Policial_Parado", true, false);
            var run = CopyClip("Assets/Modelos/Policial/Animacoes/PolicialCorrendo.fbx", "Policial_Correndo", true, false);
            var jump = CopyClip("Assets/Modelos/Policial/Animacoes/PolicialPulando.fbx", "Policial_Pulando", false, true);
            var crouch = CopyClip("Assets/Modelos/Policial/Animacoes/PolicialAgachado.fbx", "Policial_Abaixado", true, false);

            string path = AnimDir + "/Policial.controller";
            var ctrl = GetCleanController(path);
            ctrl.AddParameter(new AnimatorControllerParameter { name = "Speed", type = AnimatorControllerParameterType.Float });
            ctrl.AddParameter(new AnimatorControllerParameter { name = "Grounded", type = AnimatorControllerParameterType.Bool, defaultBool = true });
            ctrl.AddParameter(new AnimatorControllerParameter { name = "Crouch", type = AnimatorControllerParameterType.Bool });
            var sm = ctrl.layers[0].stateMachine;
            var sIdle = sm.AddState("Parado", new Vector3(300, 0));
            var sRun = sm.AddState("Correndo", new Vector3(300, 120));
            var sJump = sm.AddState("Pulando", new Vector3(600, 60));
            var sCrouch = sm.AddState("Abaixado", new Vector3(0, 60));
            sIdle.motion = idle; sRun.motion = run; sJump.motion = jump; sCrouch.motion = crouch;
            sJump.speed = 1.3f;
            sm.defaultState = sIdle;

            Tr(sIdle, sRun, 0.08f).AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            Tr(sRun, sIdle, 0.1f).AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
            Tr(sIdle, sJump, 0.05f).AddCondition(AnimatorConditionMode.IfNot, 0, "Grounded");
            Tr(sRun, sJump, 0.05f).AddCondition(AnimatorConditionMode.IfNot, 0, "Grounded");
            var j2i = Tr(sJump, sIdle, 0.1f); j2i.AddCondition(AnimatorConditionMode.If, 0, "Grounded"); j2i.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
            var j2r = Tr(sJump, sRun, 0.1f); j2r.AddCondition(AnimatorConditionMode.If, 0, "Grounded"); j2r.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            Tr(sIdle, sCrouch, 0.1f).AddCondition(AnimatorConditionMode.If, 0, "Crouch");
            Tr(sRun, sCrouch, 0.1f).AddCondition(AnimatorConditionMode.If, 0, "Crouch");
            Tr(sCrouch, sIdle, 0.1f).AddCondition(AnimatorConditionMode.IfNot, 0, "Crouch");
            EditorUtility.SetDirty(ctrl);
            policeController = ctrl;
            return ctrl;
        }

        static AnimatorController CreateFugitiveAnimator()
        {
            // O Fugitivo.fbx traz apenas uma pose estática; os esqueletos Mixamo têm os mesmos ossos,
            // então a corrida do policial é reaproveitada (cópia), com o quadril na altura do fugitivo.
            float HipsRestY(string fbx)
            {
                var b = EditorCurveBinding.FloatCurve("mixamorig:Hips", typeof(Transform), "m_LocalPosition.y");
                var c = AnimationUtility.GetEditorCurve(SourceClip(fbx), b);
                return c != null ? c.Evaluate(0f) : 0f;
            }
            float offset = HipsRestY("Assets/Modelos/Fugitivo/Fugitivo.fbx") - HipsRestY("Assets/Modelos/Policial/Policial.fbx");
            var run = CopyClip("Assets/Modelos/Policial/Animacoes/PolicialCorrendo.fbx", "Fugitivo_Correndo", true, false, offset);
            string path = AnimDir + "/Fugitivo.controller";
            var ctrl = GetCleanController(path);
            var sm = ctrl.layers[0].stateMachine;
            var s = sm.AddState("Correndo");
            s.motion = run;
            sm.defaultState = s;
            EditorUtility.SetDirty(ctrl);
            fugitiveController = ctrl;
            return ctrl;
        }

        // =====================================================================
        // Prefabs de jogo (envolvem os prefabs originais)
        // =====================================================================
        static GameObject InstantiateOriginal(string path, Transform parent)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(src, parent);
            return go;
        }

        static void SetLayerRecursive(GameObject go, int layer)
        {
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        }

        static GameObject SavePrefab(GameObject root, string name)
        {
            string path = GameplayPrefabDir + "/" + name + ".prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        static GameObject CreatePolicePrefab(AnimatorController ctrl, GameConfig config)
        {
            var root = new GameObject("PolicialJogador");
            var cc = root.AddComponent<CharacterController>();
            cc.height = config.standHeight; cc.radius = config.capsuleRadius; cc.center = new Vector3(0, config.standHeight * 0.5f, 0);
            cc.skinWidth = 0.03f; cc.stepOffset = 0.25f; cc.slopeLimit = 50f; cc.minMoveDistance = 0f;
            var pivot = new GameObject("Visual").transform;
            pivot.SetParent(root.transform, false);
            pivot.localRotation = Quaternion.Euler(0, 90, 0);
            var model = InstantiateOriginal(PolicialPrefab, pivot);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one * CharacterScale;
            var anim = model.GetComponent<Animator>(); if (!anim) anim = model.AddComponent<Animator>();
            anim.runtimeAnimatorController = ctrl;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var pc = root.AddComponent<PoliceController>();
            pc.config = config;
            pc.visualPivot = pivot;
            pc.animator = anim;
            pc.groundMask = 1 << GroundLayer;
            SetLayerRecursive(root, PlayerLayer);
            return SavePrefab(root, "PolicialJogador");
        }

        static GameObject CreateFugitivePrefab(AnimatorController ctrl, GameConfig config)
        {
            var root = new GameObject("FugitivoIA");
            var pivot = new GameObject("Visual").transform;
            pivot.SetParent(root.transform, false);
            pivot.localRotation = Quaternion.Euler(0, -90, 0);
            var model = InstantiateOriginal(FugitivoPrefab, pivot);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one * CharacterScale;
            var anim = model.GetComponent<Animator>(); if (!anim) anim = model.AddComponent<Animator>();
            anim.runtimeAnimatorController = ctrl;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var ai = root.AddComponent<FugitiveAI>();
            ai.config = config;
            ai.visualPivot = pivot;
            ai.animator = anim;
            SetLayerRecursive(root, FugitiveLayer);
            return SavePrefab(root, "FugitivoIA");
        }

        /// <summary>Cria um wrapper com o prefab original escalado, base apoiada em y=0 e collider original como trigger.</summary>
        static Obstacle CreateObstacle(string name, string src, ObstacleType type, float targetHeightFromCollider, float modelYaw)
        {
            var root = new GameObject(name);
            var pivot = new GameObject("Visual").transform;
            pivot.SetParent(root.transform, false);
            var holder = new GameObject("Modelo").transform;
            holder.SetParent(pivot, false);
            holder.localRotation = Quaternion.Euler(0, modelYaw, 0);
            var model = InstantiateOriginal(src, holder);
            var col = model.GetComponentInChildren<Collider>();
            // escala pelo tamanho vertical do collider original
            Physics.SyncTransforms();
            float h = col.bounds.size.y;
            float scale = targetHeightFromCollider / h;
            holder.localScale = Vector3.one * scale;
            Physics.SyncTransforms();
            var b = col.bounds;
            Vector3 rendererMin = model.GetComponentsInChildren<Renderer>().Select(r => r.bounds.min).Aggregate(Vector3.Min);
            float bottom = Mathf.Min(b.min.y, rendererMin.y);
            if (type == ObstacleType.Plane) bottom = b.min.y;
            holder.localPosition = new Vector3(-b.center.x, -bottom, -b.center.z);
            col.isTrigger = true;
            var o = root.AddComponent<Obstacle>();
            o.type = type;
            o.hitCollider = col;
            o.visual = type == ObstacleType.Ball ? holder : pivot;
            if (type == ObstacleType.Ball)
            {
                // a bola gira em torno do próprio centro: usa um pivô centralizado
                var center = new GameObject("CentroBola").transform;
                center.SetParent(pivot, false);
                center.localPosition = new Vector3(0, b.size.y * 0.5f, 0);
                holder.SetParent(center, true);
                o.visual = center;
                o.rollRadius = b.size.y * 0.5f;
            }
            if (type == ObstacleType.Plane)
            {
                var hel = model.transform.Find("Helice");
                o.propeller = hel;
            }
            SetLayerRecursive(root, ObstacleLayer);
            var prefab = SavePrefab(root, name);
            return prefab.GetComponent<Obstacle>();
        }

        static Obstacle[] CreateObstaclePrefabs()
        {
            return new[]
            {
                CreateObstacle("Obstaculo_Carrinho", CarrinhoPrefab, ObstacleType.Cart, 0.9f, 0f),
                CreateObstacle("Obstaculo_Bola", BolaPrefab, ObstacleType.Ball, 0.54f, 0f),
                CreateObstacle("Obstaculo_Radio", RadioPrefab, ObstacleType.Radio, 0.62f, 85.5f), // frente do rádio voltada para a câmera
                CreateObstacle("Obstaculo_Aviao", AviaoPrefab, ObstacleType.Plane, 0.34f, 90f),
            };
        }

        static GameObject Prim(PrimitiveType t, string name, Transform parent, Vector3 localPos, Vector3 scale, Material m, bool keepCollider = false)
        {
            var go = GameObject.CreatePrimitive(t);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = m;
            if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        static Collectible[] CreateCollectiblePrefabs()
        {
            var a = new GameObject("Coletavel_Maleta");
            Prim(PrimitiveType.Cube, "Corpo", a.transform, Vector3.zero, new Vector3(0.5f, 0.36f, 0.14f), matBrown);
            Prim(PrimitiveType.Cube, "Alca", a.transform, new Vector3(0, 0.22f, 0), new Vector3(0.18f, 0.08f, 0.05f), matDark);
            a.AddComponent<Collectible>();
            var b = new GameObject("Coletavel_SacoDinheiro");
            Prim(PrimitiveType.Sphere, "Saco", b.transform, Vector3.zero, new Vector3(0.42f, 0.4f, 0.42f), matMoney);
            Prim(PrimitiveType.Cylinder, "Amarra", b.transform, new Vector3(0, 0.23f, 0), new Vector3(0.14f, 0.06f, 0.14f), matMoney);
            var cifrao = new GameObject("Cifrao");
            cifrao.transform.SetParent(b.transform, false);
            cifrao.transform.localPosition = new Vector3(0, 0, -0.22f);
            var tm = cifrao.AddComponent<TextMesh>();
            tm.text = "$"; tm.font = font; tm.fontSize = 64; tm.characterSize = 0.03f; tm.anchor = TextAnchor.MiddleCenter; tm.color = Color.yellow;
            cifrao.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            b.AddComponent<Collectible>();
            return new[] { SavePrefab(a, "Coletavel_Maleta").GetComponent<Collectible>(), SavePrefab(b, "Coletavel_SacoDinheiro").GetComponent<Collectible>() };
        }

        // =====================================================================
        // Cena
        // =====================================================================
        static GameObject Box(string name, Transform parent, Vector3 center, Vector3 size, Material m, bool collider = true, int layer = GroundLayer)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = m;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            go.layer = layer;
            go.isStatic = true;
            return go;
        }

        static GameObject BoxMinMax(string name, Transform parent, Vector3 min, Vector3 max, Material m, bool collider = true)
            => Box(name, parent, (min + max) * 0.5f, max - min, m, collider);

        static void Sign(string text, Transform parent, Vector3 pos, float size, Color color, Material bg = null, Vector2 bgSize = default)
        {
            var go = new GameObject("Placa_" + text.Replace("\n", " "));
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var tm = go.AddComponent<TextMesh>();
            tm.text = text; tm.font = font; tm.fontSize = 96; tm.characterSize = size; tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center; tm.color = color;
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            if (bg)
            {
                var b = Box("Fundo", go.transform, pos + new Vector3(0, 0, 0.06f), new Vector3(bgSize.x, bgSize.y, 0.08f), bg, false, 0);
                b.isStatic = true;
            }
        }

        static float FloorY(int i) => i * FloorSpacing;

        struct EscGeo { public Vector3 low, high; public float run; }

        /// <summary>Instancia a escada rolante original escalada e orientada; retorna as pontas (baixa e alta) em mundo.</summary>
        static EscGeo PlaceEscalator(string name, Transform parent, int side, float lowFloorY, float z, Material overrideMat)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(EscadaPrefab);
            var mesh = src.GetComponent<MeshFilter>().sharedMesh;
            var v = mesh.vertices;
            Vector3 lowV = v.OrderBy(p => p.y).First(), highV = v.OrderBy(p => p.y).Last();
            float cx = mesh.bounds.center.x;
            Vector3 lowLocal = new Vector3(cx, lowV.y, lowV.z), highLocal = new Vector3(cx, highV.y, highV.z);
            float scale = FloorSpacing / (highV.z - lowV.z);

            var wrapper = new GameObject(name).transform;
            wrapper.SetParent(parent, false);
            wrapper.rotation = Quaternion.Euler(0, side > 0 ? -90f : 90f, 0);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(src, wrapper);
            inst.transform.localScale = Vector3.one * scale;
            inst.transform.localPosition = Vector3.zero;
            Vector3 lowW = inst.transform.TransformPoint(lowLocal);
            Vector3 target = new Vector3(side * EscLowX, lowFloorY, z);
            wrapper.position += target - lowW;
            if (overrideMat) inst.GetComponent<MeshRenderer>().sharedMaterial = overrideMat;
            var g = new EscGeo { low = inst.transform.TransformPoint(lowLocal), high = inst.transform.TransformPoint(highLocal) };
            g.run = Mathf.Abs(g.high.x - g.low.x);
            return g;
        }

        static Transform Point(string name, Transform parent, Vector3 pos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.position = pos;
            return t;
        }

        static FloorConnection MakeConnection(string name, Transform parent, int from, int to, bool police, Vector3[] pts)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var c = go.AddComponent<FloorConnection>();
            c.fromFloor = from; c.toFloor = to; c.policeCanUse = police; c.fugitiveCanUse = true;
            c.path = new Transform[pts.Length];
            for (int i = 0; i < pts.Length; i++)
                c.path[i] = Point(i == 0 ? "Entrada" : i == pts.Length - 1 ? "Saida" : "Ponto" + i, go.transform, pts[i]);
            return c;
        }

        static void BuildScene(GameConfig config, DifficultySettings difficulty, GameObject policePrefab, GameObject fugitivePrefab, Obstacle[] obstacles, Collectible[] collectibles, System.Text.StringBuilder log)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ---------------- Iluminação ----------------
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.66f, 0.75f);
            RenderSettings.ambientEquatorColor = new Color(0.5f, 0.48f, 0.45f);
            RenderSettings.ambientGroundColor = new Color(0.25f, 0.22f, 0.2f);
            RenderSettings.skybox = null;
            var sun = new GameObject("Luz Direcional");
            var l = sun.AddComponent<Light>();
            l.type = LightType.Directional; l.intensity = 1.6f; l.shadows = LightShadows.Soft; l.color = new Color(1f, 0.96f, 0.9f);
            sun.transform.rotation = Quaternion.Euler(38f, -25f, 0f);
            sun.AddComponent<UniversalAdditionalLightData>();
            var volGo = new GameObject("Global Volume");
            var vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/SampleSceneProfile.asset");

            // ---------------- Loja ----------------
            var level = new GameObject("Loja").transform;
            var geo = new GameObject("Estrutura").transform; geo.SetParent(level, false);
            var deco = new GameObject("Acabamentos").transform; deco.SetParent(level, false);
            float L = HalfLength + 0.5f;

            // Holes por laje (somente na faixa de fundo; o corredor da frente fica contínuo)
            var holes = new Dictionary<int, List<Vector2>>();
            for (int i = 0; i < FloorCount; i++) holes[i] = new List<Vector2>();
            for (int p = 0; p < PairSide.Length; p++)
            {
                int s = PairSide[p];
                float a = s * (EscLowX - 0.4f), b = s * (EscLowX + 7.0f);
                holes[p + 1].Add(new Vector2(Mathf.Min(a, b), Mathf.Max(a, b)));
            }
            float shaftHalf = 1.45f;
            holes[1].Add(new Vector2(-shaftHalf, shaftHalf));
            holes[2].Add(new Vector2(-shaftHalf, shaftHalf));

            for (int i = 0; i < FloorCount; i++)
            {
                float y = FloorY(i);
                var fl = new GameObject(i == FloorCount - 1 ? "Terraco" : "Andar" + i).transform;
                fl.SetParent(geo, false);
                var floorMat = i == FloorCount - 1 ? matTerrace : (i % 2 == 0 ? matFloor : matFloorAlt);
                float thick = i == 0 ? 0.6f : SlabThickness;
                // faixa frontal contínua (corredor)
                BoxMinMax("Corredor", fl, new Vector3(-L, y - thick, FrontZ), new Vector3(L, y, BackZoneZ), floorMat);
                // faixa de fundo com aberturas reais
                var hs = holes[i].OrderBy(h => h.x).ToList();
                float cur = -L;
                int k = 0;
                foreach (var h in hs)
                {
                    if (h.x > cur) BoxMinMax("Fundo" + k++, fl, new Vector3(cur, y - thick, BackZoneZ), new Vector3(h.x, y, BackZ), floorMat);
                    cur = Mathf.Max(cur, h.y);
                }
                if (cur < L) BoxMinMax("Fundo" + k, fl, new Vector3(cur, y - thick, BackZoneZ), new Vector3(L, y, BackZ), floorMat);
                // acabamento frontal da laje
                Box("Friso", fl, new Vector3(0, y - 0.06f, FrontZ - 0.03f), new Vector3(2 * L, 0.12f, 0.06f), matTrim, false);

                if (i < FloorCount - 1)
                {
                    // parede de fundo do andar
                    var wm = i % 2 == 0 ? matWall : matWallAlt;
                    BoxMinMax("ParedeFundo", fl, new Vector3(-L, y, BackZ), new Vector3(L, y + FloorSpacing - SlabThickness, BackZ + 0.3f), wm);
                    Box("Rodape", fl, new Vector3(0, y + 0.1f, BackZ - 0.02f), new Vector3(2 * L, 0.2f, 0.05f), matDark, false);
                    // paredes laterais
                    BoxMinMax("ParedeEsq", fl, new Vector3(-L - 1f, y, FrontZ), new Vector3(-L, y + FloorSpacing, BackZ + 0.3f), wm);
                    BoxMinMax("ParedeDir", fl, new Vector3(L, y, FrontZ), new Vector3(L + 1f, y + FloorSpacing, BackZ + 0.3f), wm);
                    string[] names = { "TÉRREO", "1º ANDAR", "2º ANDAR" };
                    foreach (float sxp in new[] { -32f, -6f, 7f, 30f })
                        Sign(names[i], deco, new Vector3(sxp, y + 2.5f, BackZ - 0.1f), 0.05f, Color.white, matSignBg, new Vector2(3.4f, 0.8f));
                    // prateleiras decorativas
                    for (int sx = -44; sx <= 44; sx += 8)
                    {
                        if (Mathf.Abs(sx) < 3) continue;
                        bool nearEsc = false;
                        for (int p = 0; p < PairSide.Length; p++)
                            if ((p == i || p + 1 == i) && Mathf.Abs(sx - PairSide[p] * (EscLowX + 3.4f)) < 6f) nearEsc = true;
                        if (nearEsc) continue;
                        Box("Prateleira", deco, new Vector3(sx, y + 0.9f, BackZ - 0.4f), new Vector3(3f, 1.8f, 0.6f), matShelf, false, 0);
                        Box("PrateleiraTabua", deco, new Vector3(sx, y + 1.0f, BackZ - 0.75f), new Vector3(3.05f, 0.06f, 0.1f), matDark, false, 0);
                        Box("Produtos", deco, new Vector3(sx, y + 1.85f, BackZ - 0.45f), new Vector3(2.6f, 0.3f, 0.4f), matTrim, false, 0);
                    }
                }
                else
                {
                    // terraço: mureta de fundo e laterais baixas
                    BoxMinMax("MuretaFundo", fl, new Vector3(-L, y, BackZ), new Vector3(L, y + 0.9f, BackZ + 0.3f), matTerrace);
                    BoxMinMax("MuretaEsq", fl, new Vector3(-L - 1f, y, FrontZ), new Vector3(-L, y + 1.2f, BackZ + 0.3f), matTerrace);
                    BoxMinMax("MuretaDir", fl, new Vector3(L, y, FrontZ), new Vector3(L + 1f, y + 1.2f, BackZ + 0.3f), matTerrace);
                    Sign("TERRAÇO", deco, new Vector3(0f, y + 2.2f, BackZ - 0.1f), 0.05f, Color.white, matSignBg, new Vector2(3.2f, 0.8f));
                }
            }
            Box("Fundacao", deco, new Vector3(0, -2.6f, 2f), new Vector3(2 * L + 4f, 4f, 8f), matDark, false, 0);
            // Chão externo de fundo (visual)
            Box("Fachada", deco, new Vector3(0, FloorY(FloorCount - 1) * 0.5f, BackZ + 1.2f), new Vector3(2 * L + 4f, FloorY(FloorCount - 1) + 1f, 0.5f), matDark, false, 0);

            // ---------------- Escadas rolantes ----------------
            var conns = new List<FloorConnection>();
            var escRoot = new GameObject("EscadasRolantes").transform; escRoot.SetParent(level, false);
            var origEscMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Modelos/Cenarios/Escada Rolante/escadaRolante.mat");
            for (int p = 0; p < PairSide.Length; p++)
            {
                int s = PairSide[p];
                float yLow = FloorY(p), yHigh = FloorY(p + 1);
                var up = PlaceEscalator("EscadaSobe_" + p + "_" + (p + 1), escRoot, s, yLow, EscUpZ, null);
                var down = PlaceEscalator("EscadaDesceFugitivo_" + (p + 1) + "_" + p, escRoot, s, yLow, EscDownZ, matEscDown);
                float run = up.run;
                Vector3 lowU = new Vector3(s * EscLowX, yLow, EscUpZ), highU = new Vector3(s * (EscLowX + run), yHigh, EscUpZ);
                conns.Add(MakeConnection("Conexao_Sobe_" + p + "_" + (p + 1), escRoot, p, p + 1, true, new[]
                {
                    new Vector3(s * (EscLowX - 0.7f), yLow, 0f),
                    lowU,
                    lowU + new Vector3(s * run * 0.14f, 0f, 0f),
                    highU - new Vector3(s * run * 0.14f, 0f, 0f),
                    highU,
                    new Vector3(s * (EscLowX + run + 0.7f), yHigh, 0f),
                }));
                Vector3 lowD = new Vector3(s * EscLowX, yLow, EscDownZ), highD = new Vector3(s * (EscLowX + run), yHigh, EscDownZ);
                conns.Add(MakeConnection("Conexao_DesceFugitivo_" + (p + 1) + "_" + p, escRoot, p + 1, p, false, new[]
                {
                    new Vector3(s * (EscLowX + run + 0.7f), yHigh, 0f),
                    highD,
                    highD - new Vector3(s * run * 0.14f, 0f, 0f),
                    lowD + new Vector3(s * run * 0.14f, 0f, 0f),
                    lowD,
                    new Vector3(s * (EscLowX - 1.4f), yLow, 0f),
                }));
                Sign("SOBE ▲", deco, new Vector3(s * (EscLowX - 0.2f), yLow + 2.6f, EscUpZ + 0.1f), 0.035f, new Color(0.4f, 1f, 1f));
                Sign("SERVIÇO\n(só fugitivo)", deco, new Vector3(s * (EscLowX + run + 0.3f), yHigh + 2.4f, EscDownZ + 0.5f), 0.025f, new Color(1f, 0.6f, 0.2f));
                log.AppendLine("Escada " + p + " run=" + run.ToString("F2") + " low=" + up.low + " high=" + up.high);
            }

            // ---------------- Elevador ----------------
            var elRoot = new GameObject("Elevador").transform; elRoot.SetParent(level, false);
            elRoot.position = new Vector3(0, 0, ElevatorZ);
            // elementos fixos: trilhos e fundo do poço (não se movem)
            var shaft = new GameObject("PocoFixo").transform; shaft.SetParent(elRoot, false);
            float topShaft = FloorY(2) + 3.5f;
            BoxMinMax("FundoPoco", shaft, new Vector3(-shaftHalf, 0, ElevatorZ + 1.25f), new Vector3(shaftHalf, topShaft, ElevatorZ + 1.4f), matShaft, false);
            BoxMinMax("TrilhoEsq", shaft, new Vector3(-shaftHalf, 0, ElevatorZ - 1.2f), new Vector3(-shaftHalf + 0.12f, topShaft, ElevatorZ + 1.25f), matShaft, false);
            BoxMinMax("TrilhoDir", shaft, new Vector3(shaftHalf - 0.12f, 0, ElevatorZ - 1.2f), new Vector3(shaftHalf, topShaft, ElevatorZ + 1.25f), matShaft, false);
            for (int i = 0; i <= 2; i++)
            {
                float y = FloorY(i);
                Box("Moldura" + i, shaft, new Vector3(0, y + 3.25f, ElevatorZ - 1.25f), new Vector3(2 * shaftHalf + 0.2f, 0.2f, 0.1f), matTrim, false, 0);
                Sign("ELEVADOR", deco, new Vector3(0, y + 3.55f, ElevatorZ - 1.3f), 0.022f, Color.yellow);
            }
            var cabin = InstantiateOriginal(ElevadorPrefab, elRoot);
            cabin.name = "Cabine (Elevador)";
            cabin.transform.localScale = Vector3.one * ElevatorScale;
            cabin.transform.rotation = Quaternion.Euler(0, 180, 0);
            cabin.transform.position = new Vector3(0, 0, ElevatorZ);
            var cubeR = cabin.transform.Find("Cube").GetComponent<Renderer>();
            float cabinOffset = cabin.transform.position.y - cubeR.bounds.min.y + 0.02f;
            cabin.transform.position = new Vector3(0, cabinOffset, ElevatorZ);
            var cabinLight = cabin.GetComponentInChildren<Light>();
            if (cabinLight) { cabinLight.intensity = 3f; cabinLight.range = 4f; cabinLight.transform.position = cabin.transform.position + new Vector3(0, 1.0f, 0); cabinLight.shadows = LightShadows.None; }
            var riderPoint = Point("PontoPassageiro", cabin.transform, new Vector3(0, 0.02f, ElevatorZ));
            var elevator = elRoot.gameObject.AddComponent<ElevatorController>();
            elevator.cabin = cabin.transform;
            elevator.doors = new[] { "PortaEsquerda", "PortaDireita" }.SelectMany(n => cabin.transform.Find(n).GetComponentsInChildren<SkinnedMeshRenderer>(true)).ToArray();
            elevator.riderPoint = riderPoint;
            elevator.cabinFloorOffset = cabinOffset;
            elevator.bottomFloor = 0; elevator.topFloor = 2;
            elevator.config = config;
            log.AppendLine("Elevador offset=" + cabinOffset.ToString("F3") + " portas=" + elevator.doors.Length + " cabineBounds=" + cubeR.bounds.size);

            // ---------------- Saída de fuga (terraço) ----------------
            float ty = FloorY(FloorCount - 1);
            var escape = Point("SaidaFuga", level, new Vector3(-HalfLength + 0.8f, ty, 0));
            Prim(PrimitiveType.Cylinder, "Mastro", escape, new Vector3(-0.3f, 1.5f, 1.2f), new Vector3(0.08f, 1.5f, 0.08f), matRail);
            Prim(PrimitiveType.Cube, "Bandeira", escape, new Vector3(0.25f, 2.6f, 1.2f), new Vector3(1.0f, 0.6f, 0.04f), matGreen);
            Prim(PrimitiveType.Cube, "FaixaSaida", escape, new Vector3(0.5f, 0.02f, 0f), new Vector3(2.4f, 0.04f, 1.8f), matGreen);
            Sign("FUGA ◄", deco, new Vector3(-HalfLength + 2.5f, ty + 2.0f, 1.2f), 0.04f, new Color(0.3f, 1f, 0.4f));

            // ---------------- LevelLayout ----------------
            var layout = level.gameObject.AddComponent<LevelLayout>();
            layout.floors = new LevelLayout.FloorInfo[FloorCount];
            string[] fn = { "Térreo", "1º Andar", "2º Andar", "Terraço" };
            for (int i = 0; i < FloorCount; i++)
                layout.floors[i] = new LevelLayout.FloorInfo { name = fn[i], y = FloorY(i), minX = -HalfLength, maxX = HalfLength };
            layout.playZ = 0f;
            layout.escapePoint = escape;
            layout.connections = conns.ToArray();
            layout.elevator = elevator;

            // ---------------- Personagens ----------------
            var actors = new GameObject("Personagens").transform;
            // Policial começa no bloco da ponta esquerda do térreo, longe do elevador central (3 blocos).
            var policeSpawn = Point("SpawnPolicial", actors, new Vector3(-HalfLength + HalfLength / 7f, FloorY(0), 0));
            var fugitiveSpawn = Point("SpawnFugitivo", actors, new Vector3(2f, FloorY(1), 0));
            var police = ((GameObject)PrefabUtility.InstantiatePrefab(policePrefab, actors)).GetComponent<PoliceController>();
            police.transform.position = policeSpawn.position;
            var fugitive = ((GameObject)PrefabUtility.InstantiatePrefab(fugitivePrefab, actors)).GetComponent<FugitiveAI>();
            fugitive.transform.position = fugitiveSpawn.position;
            fugitive.police = police;
            // Garante o controller nas instâncias da cena (evita referência obsoleta após reconstruções).
            police.animator.runtimeAnimatorController = policeController;
            fugitive.animator.runtimeAnimatorController = fugitiveController;

            // ---------------- Câmera ----------------
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = config.cameraOrthoSize;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.36f, 0.5f, 0.68f);
            cam.nearClipPlane = 0.3f; cam.farClipPlane = 60f;
            camGo.AddComponent<AudioListener>();
            var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            var follow = camGo.AddComponent<CameraFollow>();
            follow.target = police;
            follow.config = config;
            camGo.transform.rotation = Quaternion.Euler(follow.pitch, 0, 0);
            camGo.transform.position = new Vector3(-12, config.cameraHeightOffset, 0) - camGo.transform.forward * follow.distance;

            // ---------------- Gerenciadores ----------------
            var managers = new GameObject("Gerenciadores");
            managers.AddComponent<GameInput>();
            var spawner = new GameObject("GeradorObstaculos").AddComponent<ObstacleSpawner>();
            spawner.transform.SetParent(managers.transform, false);
            spawner.cartPrefab = obstacles[0]; spawner.ballPrefab = obstacles[1]; spawner.radioPrefab = obstacles[2]; spawner.planePrefab = obstacles[3];
            spawner.collectiblePrefabs = collectibles;
            spawner.police = police;
            spawner.gameCamera = cam;
            var gm = managers.AddComponent<GameManager>();
            gm.config = config; gm.difficulty = difficulty; gm.police = police; gm.fugitive = fugitive; gm.spawner = spawner;
            gm.cameraFollow = follow; gm.policeSpawn = policeSpawn; gm.fugitiveSpawn = fugitiveSpawn; gm.fugitiveStartFacing = -1f;
            AddGameAudio(managers);

            // ---------------- UI ----------------
            var ui = BuildUI(gm, config, police, fugitive);
            gm.ui = ui;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            var list = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
            log.AppendLine("Cena salva em " + ScenePath);
        }

        // =====================================================================
        // UI
        // =====================================================================
        static Text MakeText(string name, Transform parent, string text, int size, TextAnchor anchor, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 sizeDelta, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(Outline));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = sizeDelta;
            var t = go.GetComponent<Text>();
            t.text = text; t.font = font; t.fontSize = size; t.alignment = anchor; t.color = color;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            var o = go.GetComponent<Outline>(); o.effectColor = new Color(0, 0, 0, 0.8f); o.effectDistance = new Vector2(2, -2);
            return t;
        }

        static Image MakePanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size, Color c)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.color = c;
            return img;
        }

        static Button MakeButton(string name, Transform parent, string label, Vector2 pos)
        {
            var img = MakePanel(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(380, 80), new Color(0.15f, 0.35f, 0.85f, 0.95f));
            var b = img.gameObject.AddComponent<Button>();
            var cb = b.colors; cb.highlightedColor = new Color(0.6f, 0.8f, 1f); cb.selectedColor = new Color(0.6f, 0.8f, 1f); cb.pressedColor = new Color(0.4f, 0.6f, 0.9f); b.colors = cb;
            MakeText("Texto", img.transform, label, 40, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Color.white);
            return b;
        }

        static UIManager BuildUI(GameManager gm, GameConfig config, PoliceController police, FugitiveAI fugitive)
        {
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            var canvasGo = new GameObject("UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            var root = canvasGo.transform;
            var ui = canvasGo.AddComponent<UIManager>();
            ui.game = gm; ui.config = config;

            // HUD
            var hud = new GameObject("HUD", typeof(RectTransform)).transform;
            hud.SetParent(root, false);
            var hudRt = (RectTransform)hud; hudRt.anchorMin = Vector2.zero; hudRt.anchorMax = Vector2.one; hudRt.offsetMin = hudRt.offsetMax = Vector2.zero;
            ui.hudRoot = hud.gameObject;
            var bar = MakePanel("BarraSuperior", hud, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -50), new Vector2(0, 100), new Color(0, 0, 0, 0.35f));
            bar.raycastTarget = false;
            ui.scoreText = MakeText("Pontos", hud, "PONTOS 000000", 36, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(230, -32), new Vector2(420, 50), Color.white);
            ui.roundText = MakeText("Rodada", hud, "RODADA 1", 30, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(230, -74), new Vector2(420, 40), new Color(1f, 0.9f, 0.5f));
            MakeText("TempoRotulo", hud, "TEMPO", 22, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-330, -22), new Vector2(200, 30), new Color(0.8f, 0.8f, 0.8f));
            ui.timerText = MakeText("Cronometro", hud, "50", 58, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-330, -62), new Vector2(200, 64), Color.white);
            ui.reservesLabel = MakeText("ReservasRotulo", hud, "RESERVAS", 22, TextAnchor.MiddleLeft, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-110, -26), new Vector2(200, 30), new Color(0.8f, 0.8f, 0.8f));
            var icons = new List<Image>();
            for (int i = 0; i < Mathf.Max(3, config.maxReserveLives); i++)
            {
                var ic = MakePanel("Reserva" + i, hud, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-180 + i * 44, -66), new Vector2(30, 38), new Color(0.25f, 0.5f, 1f));
                ic.raycastTarget = false;
                var cap = MakePanel("Quepe", ic.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, 2), new Vector2(34, 10), new Color(0.1f, 0.15f, 0.4f));
                cap.raycastTarget = false;
                var badge = MakePanel("Distintivo", ic.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 2), new Vector2(10, 10), new Color(1f, 0.85f, 0.2f));
                badge.raycastTarget = false;
                icons.Add(ic);
            }
            ui.reserveIcons = icons.ToArray();
            ui.messageText = MakeText("Mensagem", hud, "", 64, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 120), new Vector2(1200, 200), new Color(1f, 0.95f, 0.4f));
            ui.penaltyText = MakeText("Penalidade", hud, "", 48, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 60), new Vector2(600, 80), new Color(1f, 0.35f, 0.3f));
            MakeText("Ajuda", hud, "A/D correr  ESPAÇO pular  S abaixar  W entrar no elevador  S sair  ESC pausa", 20, TextAnchor.MiddleCenter, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 22), new Vector2(1400, 30), new Color(1, 1, 1, 0.7f));

            // Minimapa
            var mm = MakePanel("Minimapa", hud, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-250, -125), new Vector2(460, 210), new Color(0f, 0f, 0f, 0.55f));
            mm.raycastTarget = false;
            MakeText("MinimapaTitulo", mm.transform, "MAPA", 18, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -12), new Vector2(80, 20), new Color(1, 1, 1, 0.7f));
            var mmc = mm.gameObject.AddComponent<MinimapController>();
            mmc.area = mm.rectTransform; mmc.police = police; mmc.fugitive = fugitive;

            // Tela inicial
            var title = MakePanel("TelaInicial", root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.02f, 0.04f, 0.12f, 0.82f));
            MakeText("Titulo", title.transform, "KEYSTONE KAPERS", 110, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 270), new Vector2(1600, 140), new Color(1f, 0.85f, 0.2f));
            MakeText("Subtitulo", title.transform, "Perseguição na loja de departamentos — 2.5D", 40, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 180), new Vector2(1600, 60), Color.white);
            MakeText("Instrucoes", title.transform,
                "Capture o fugitivo antes que o tempo acabe ou que ele escape pelo terraço.\n" +
                "Use as escadas rolantes (só sobem) e o elevador central. Pule carrinhos, bolas e rádios (-9s).\n" +
                "Abaixe-se para escapar dos aviões (perde uma vida).\n\n" +
                "A/D ou ←/→ correr   ESPAÇO pular   S/↓ abaixar   W/↑ entrar no elevador   S/↓ sair   ESC pausar",
                28, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(1700, 220), new Color(0.85f, 0.9f, 1f));
            ui.titlePanel = title.gameObject;
            ui.playButton = MakeButton("BotaoJogar", title.transform, "JOGAR", new Vector2(0, -170));

            // Pausa
            var pause = MakePanel("TelaPausa", root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0.6f));
            MakeText("PausaTitulo", pause.transform, "PAUSADO", 90, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 170), new Vector2(1000, 120), Color.white);
            ui.pausePanel = pause.gameObject;
            ui.continueButton = MakeButton("BotaoContinuar", pause.transform, "CONTINUAR", new Vector2(0, 20));
            ui.restartButton = MakeButton("BotaoReiniciar", pause.transform, "REINICIAR", new Vector2(0, -90));
            pause.gameObject.SetActive(false);

            // Game Over
            var go = MakePanel("TelaGameOver", root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.12f, 0, 0, 0.75f));
            MakeText("GameOverTitulo", go.transform, "GAME OVER", 110, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 200), new Vector2(1200, 140), new Color(1f, 0.3f, 0.25f));
            ui.gameOverScoreText = MakeText("PontuacaoFinal", go.transform, "", 48, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 50), new Vector2(1200, 140), Color.white);
            ui.gameOverPanel = go.gameObject;
            ui.playAgainButton = MakeButton("BotaoJogarNovamente", go.transform, "JOGAR NOVAMENTE", new Vector2(0, -120));
            go.gameObject.SetActive(false);

            return ui;
        }
    }
}
