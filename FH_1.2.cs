using System.Collections;
using System.Collections.Generic;
using UnityEngine;
// 【1.1修改】修改内容：删除原 FH_1.0 的 using UnityEngine.UI;
// 原因：当前团结工程没有 UGUI 程序集，Image / Canvas / MaskableGraphic /
// VertexHelper 因此无法编译。1.1 改为 UnityEngine 核心 SpriteRenderer 实现。

/// <summary>
///
/// 【1.2修改】修改内容：修复运行时无声音问题。
/// - 确保游戏相机存在并启用 AudioListener。
/// - 恢复 AudioListener.pause / volume，避免监听器处于暂停或静音状态。
///
/// 【1.2修改】修改内容：修复 GUIStyle 初始化时机问题。
/// - 不再在 Awake() 中访问 GUI.skin。
/// - 改为仅在 OnGUI() 真正需要调试界面时延迟初始化。
///
/// 【1.1修改】修改内容：移除 UnityEngine.UI / UGUI 依赖，
/// 将原来的 Canvas / Image / RectTransform / MaskableGraphic / VertexHelper
/// 改为 UnityEngine 核心 API：Camera + SpriteRenderer + Transform。
///
/// 所有 1.1 修改位置均可搜索：
/// 【1.1修改】
/// 所有 1.2 修改位置均可搜索：
/// 【1.2修改】
/// </summary>
// 【1.1修改】修改内容：主类名保持 FishingHeavenDemo 不变，避免现有场景脚本引用失效。
public class FishingHeavenDemo : MonoBehaviour
{
    public enum GamePhase
    {
        Idle,
        WaitingBite,
        Bite,
        Prompt,
        Response,
        Fever,
        Result
    }

    // ============================================================
    // 1. 素材：全部可选
    // ============================================================

    [Header("=== Optional Sprites ===")]
    [Tooltip("可直接使用完整鱼塘背景；留空则用纯色方块代替。")]
    public Sprite pondBackground;

    [Tooltip("哥布林待机图；留空则用绿色方块。")]
    public Sprite goblinIdleSprite;

    [Tooltip("哥布林拉竿图；留空则继续用待机图/绿色方块。")]
    public Sprite goblinPullSprite;

    [Tooltip("Fever 普通鱼；可留空，自动使用彩色方块。")]
    public Sprite[] feverFishSprites;

    [Tooltip("Fever 最后一击超大鱼；可留空。")]
    public Sprite hugeFishSprite;

    // ============================================================
    // 2. 音效
    // ============================================================

    [Header("=== Rhythm Audio ===")]
    public AudioClip drumLow;
    public AudioClip drumHigh;
    public AudioClip drumAccent;

    [Header("=== Judge Audio ===")]
    public AudioClip perfectClip;
    public AudioClip goodClip;
    public AudioClip missClip;

    [Header("=== Fishing / Fever Audio ===")]
    public AudioClip biteClip;
    public AudioClip energyGainClip;
    public AudioClip energyFullClip;
    public AudioClip fishFaceHitClip;
    public AudioClip bigSplashClip;


    // ============================================================
    // 3. 节奏 / osu式缩圈
    // ============================================================

    [Header("=== Rhythm Feel ===")]
    [Range(0.70f, 1.60f)]
    public float approachTime = 1.20f;

    [Range(0.05f, 0.16f)]
    public float perfectWindow = 0.10f;

    [Range(0.12f, 0.30f)]
    public float goodWindow = 0.22f;

    // 【1.1修改】修改内容：保留 FH_1.0 的缩圈透明度与淡入参数，
    // 但实现从 UI.Image.color 改成 SpriteRenderer.color。
    [Header("=== 缩圈观感 ===")]
    [Tooltip("外层 Approach Circle 的初始倍率")]
    [Range(2.0f, 5.0f)]
    public float approachStartScale = 4.0f;

    [Tooltip("外圈最大不透明度")]
    [Range(0.4f, 1.0f)]
    public float approachMaxAlpha = 0.90f;

    [Tooltip("外圈淡入所需时间。只影响可见度，不改变缩圈命中时刻")]
    [Range(0.15f, 0.9f)]
    public float approachFadeInTime = 0.55f;

    [Header("=== Energy ===")]
    [Range(1, 50)]
    public int perfectEnergy = 20;

    [Range(1, 50)]
    public int goodEnergy = 12;

    [Header("=== Fever ===")]
    [Range(8, 40)]
    public int feverFishCount = 20;

    public float feverFirstInterval = 0.23f;
    public float feverLastInterval = 0.065f;

    // ============================================================
    // 4. 性能
    // ============================================================

    [Header("=== Performance ===")]
    [Range(32, 96)]
    public int bubblePoolSize = 64;

    [Range(12, 32)]
    public int fishPoolSize = 20;

    [Tooltip("只用于开发调试。正式版本关闭。")]
    public bool showDebugOverlay = false;

    [Header("=== Runtime State ===")]
    [SerializeField] private GamePhase phase = GamePhase.Idle;
    [SerializeField] private float energy;
    [SerializeField] private int combo;

    // ============================================================
    // 5. 固定配置
    // ============================================================

    private const int TARGET_POOL_SIZE = 8;
    private const int MAX_RHYTHM_BEATS = 4;

    private static readonly Color[] FishColors =
    {
        new Color(0.45f, 0.72f, 0.95f, 1f),
        new Color(0.96f, 0.62f, 0.22f, 1f),
        new Color(0.72f, 0.42f, 0.88f, 1f),
        new Color(0.95f, 0.82f, 0.30f, 1f),
        new Color(0.48f, 0.85f, 0.74f, 1f)
    };

    // 【1.1修改】修改内容：保留 FH_1.0 的固定等待对象缓存优化，
    // 避免这些固定时间反复 new WaitForSeconds。
    private static readonly WaitForSeconds WAIT_CAST =
        new WaitForSeconds(0.65f);
    private static readonly WaitForSeconds WAIT_BITE =
        new WaitForSeconds(0.70f);
    private static readonly WaitForSeconds WAIT_AFTER_PROMPT =
        new WaitForSeconds(0.32f);
    private static readonly WaitForSeconds WAIT_BETWEEN_ROUNDS =
        new WaitForSeconds(0.52f);
    private static readonly WaitForSeconds WAIT_BEFORE_BIG_FISH =
        new WaitForSeconds(0.36f);
    private static readonly WaitForSeconds WAIT_AFTER_BIG_FISH =
        new WaitForSeconds(0.70f);

    // ============================================================
    // 6. 核心引用
    // ============================================================

    // 【1.1修改】修改内容：新增正交 Camera 世界渲染，替代 FH_1.0 的 Canvas UI 舞台。
private Camera gameCamera;

    private AudioSource sfxSource;
    private AudioSource[] rhythmSources;

    private Sprite squareSprite;
    private Sprite circleSprite;
    private Sprite ringSprite;

    private Transform worldRoot;

    // 【1.1修改】修改内容：哥布林显示由 Image 改为 SpriteRenderer。
private SpriteRenderer goblinRenderer;
    private Vector3 goblinBasePosition = new Vector3(5.8f, -0.35f, 0f);

    // 【1.1修改】修改内容：能量条由 RectTransform + Image 改为 SpriteRenderer 色块。
private SpriteRenderer energyFrameRenderer;
    private SpriteRenderer energyFillRenderer;
    private const float ENERGY_WIDTH = 6.3f;
    private const float ENERGY_HEIGHT = 0.28f;

    private bool gameRoutineRunning;
    private bool playedEnergyFull;

    private string statusLine = "SPACE: START FISHING";
    private string judgeLine = "";

    private GUIStyle titleStyle;
    private GUIStyle smallStyle;

    // ============================================================
    // 7. Rhythm data
    // ============================================================

    private sealed class RhythmPattern
    {
        public readonly string name;
        public readonly float[] offsets;
        public readonly int[] tones;

        public RhythmPattern(string name, float[] offsets, int[] tones)
        {
            this.name = name;
            this.offsets = offsets;
            this.tones = tones;
        }
    }

    private RhythmPattern[] patterns;

    // ============================================================
    // 8. Target pool
    // ============================================================

    // 【1.1修改】修改内容：判定圈数据改为 GameObject + SpriteRenderer，
    // 不再保存 PooledImage / RectTransform / Image。
    private sealed class HitTarget
    {
        public GameObject root;
        public SpriteRenderer hit;
        public SpriteRenderer approach;

        public double spawnDsp;
        public double targetDsp;

        public bool active;
        public bool spawned;
        public bool approachHidden;
        public bool resolved;
    }

    private HitTarget[] targets;

    // ============================================================
    // 9. Bubble pool
    // ============================================================

    private struct BubbleFx
    {
        public bool active;
        public Transform tr;
        public SpriteRenderer sr;
        public Vector2 velocity;
        public float age;
        public float life;
        public float baseSize;
    }

    private BubbleFx[] bubbles;

    // ============================================================
    // 10. Fever fish pool
    // ============================================================

    private struct FishFx
    {
        public bool active;
        public bool impacted;

        public Transform tr;
        public SpriteRenderer sr;

        public Vector2 start;
        public Vector2 control;
        public Vector2 end;

        public float age;
        public float duration;
        public float hold;

        public float startAngle;
        public float endAngle;
    }

    private FishFx[] fishPool;

    // 最后一条巨鱼只保留一个。
    private Transform hugeFishTransform;
    private SpriteRenderer hugeFishRenderer;

    // ============================================================
    // 11. 生命周期
    // ============================================================

    // 【1.1修改】修改内容：Awake 不再 BuildRuntimeUI()，
    // 改为 SetupCamera() + BuildWorld() + SpriteRenderer 对象池。
    private void Awake()
    {
        SetupCamera();
        BuildGeneratedSprites();
        BuildAudio();
        BuildPatterns();
        BuildWorld();
        PrewarmPools();

        // 【1.2修改】修改内容：移除 Awake() 中的 InitDebugStyles()。
        // GUI.skin 属于 IMGUI 上下文，放在 Awake() 中访问存在运行时调用时机问题。
        // 调试样式改为在 OnGUI() 中首次需要时再初始化。
        UpdateEnergyBar();
        phase = GamePhase.Idle;
    }

    private void Update()
    {
        UpdateTargets();
        UpdateBubbles();
        UpdateFeverFish();

        if (phase == GamePhase.Response && Input.GetMouseButtonDown(0))
        {
            TryHitTarget();
        }

        if ((phase == GamePhase.Idle || phase == GamePhase.Result) &&
            Input.GetKeyDown(KeyCode.Space) &&
            !gameRoutineRunning)
        {
            StartCoroutine(FishingLoop());
        }
    }

    private void OnDestroy()
    {
        DestroyGeneratedSprite(squareSprite);
        DestroyGeneratedSprite(circleSprite);
        DestroyGeneratedSprite(ringSprite);
    }

    // ============================================================
    // 12. Camera
    // ============================================================

    // 【1.1修改】修改内容：新增正交相机初始化；场景没有 MainCamera 时自动创建。
    private void SetupCamera()
    {
        gameCamera = Camera.main;

        if (gameCamera == null)
        {
            GameObject camGO = new GameObject("FH_Camera");
            camGO.tag = "MainCamera";

            gameCamera = camGO.AddComponent<Camera>();
        }

        gameCamera.orthographic = true;
        gameCamera.orthographicSize = 5.4f;
        gameCamera.transform.position = new Vector3(0f, 0f, -10f);
        gameCamera.clearFlags = CameraClearFlags.SolidColor;
        gameCamera.backgroundColor = new Color(0.57f, 0.78f, 0.88f, 1f);

        // 【1.2修改】修改内容：修复游戏运行时没有声音。
        // FH 1.1 的 FH_Camera 是通过 AddComponent<Camera>() 动态创建的，
        // 动态添加 Camera 不会像编辑器创建 Camera GameObject 那样自动带 AudioListener。
        // 没有 AudioListener 时，AudioSource / PlayOneShot / PlayScheduled 即使正常执行也听不到声音。
        AudioListener listener =
            FindObjectOfType<AudioListener>();

        if (listener == null)
        {
            listener =
                gameCamera.gameObject.AddComponent<
                    AudioListener
                >();
        }

        // 【1.2修改】修改内容：确保找到/创建的监听器实际启用，
        // 并恢复全局监听器音量与暂停状态。
        listener.enabled = true;
        AudioListener.pause = false;
        AudioListener.volume = 1f;
    }

    // ============================================================
    // 13. 世界搭建
    // ============================================================

    // 【1.1修改】修改内容：新增纯世界坐标场景搭建，替代 FH_1.0 的 Runtime Canvas。
    private void BuildWorld()
    {
        worldRoot = new GameObject("FH_WORLD").transform;

        BuildBackground();
        BuildGoblin();
        BuildEnergyBar();
    }

    // 【1.1修改】修改内容：背景改为 SpriteRenderer；
    // 没有可用背景素材时自动使用天空/水面/码头纯色方块占位。
    private void BuildBackground()
    {
        if (pondBackground != null)
        {
            SpriteRenderer bg = CreateRenderer(
                "PondBackground",
                pondBackground,
                Color.white,
                -100
            );

            bg.transform.position = new Vector3(0f, 0f, 3f);

            Bounds b = pondBackground.bounds;

            if (b.size.x > 0.001f && b.size.y > 0.001f)
            {
                float worldH = gameCamera.orthographicSize * 2f;
                float worldW = worldH * gameCamera.aspect;

                float sx = worldW / b.size.x;
                float sy = worldH / b.size.y;

                bg.transform.localScale =
                    new Vector3(sx, sy, 1f);
            }

            return;
        }

        // 没背景就用最简单的色块。
        SpriteRenderer sky = CreateRenderer(
            "Placeholder_Sky",
            squareSprite,
            new Color(0.62f, 0.82f, 0.92f, 1f),
            -100
        );

        sky.transform.position = new Vector3(0f, 2.5f, 3f);
        sky.transform.localScale = new Vector3(20f, 5f, 1f);

        SpriteRenderer water = CreateRenderer(
            "Placeholder_Water",
            squareSprite,
            new Color(0.18f, 0.52f, 0.66f, 1f),
            -90
        );

        water.transform.position = new Vector3(0f, -2.6f, 2.8f);
        water.transform.localScale = new Vector3(20f, 5.2f, 1f);

        SpriteRenderer dock = CreateRenderer(
            "Placeholder_Dock",
            squareSprite,
            new Color(0.32f, 0.18f, 0.08f, 1f),
            -50
        );

        dock.transform.position = new Vector3(-4.8f, -1.35f, 1f);
        dock.transform.localScale = new Vector3(6.4f, 0.52f, 1f);
    }

    // 【1.1修改】修改内容：哥布林改为 SpriteRenderer；无素材时自动使用绿色方块。
    private void BuildGoblin()
    {
        Sprite sprite =
            goblinIdleSprite != null
            ? goblinIdleSprite
            : squareSprite;

        Color color =
            goblinIdleSprite != null
            ? Color.white
            : new Color(0.22f, 0.64f, 0.20f, 1f);

        goblinRenderer = CreateRenderer(
            "Goblin",
            sprite,
            color,
            10
        );

        goblinRenderer.transform.position = goblinBasePosition;

        if (goblinIdleSprite == null)
        {
            goblinRenderer.transform.localScale =
                new Vector3(2.0f, 3.2f, 1f);
        }
        else
        {
            FitSpriteHeight(goblinRenderer, 4.8f);
        }
    }

    // 【1.1修改】修改内容：能量条改为两个 SpriteRenderer 矩形，不再依赖 UnityEngine.UI。
    private void BuildEnergyBar()
    {
        energyFrameRenderer = CreateRenderer(
            "EnergyFrame",
            squareSprite,
            new Color(0.08f, 0.09f, 0.10f, 0.92f),
            100
        );

        energyFrameRenderer.transform.position =
            new Vector3(0f, 4.55f, 0f);

        energyFrameRenderer.transform.localScale =
            new Vector3(ENERGY_WIDTH + 0.18f, ENERGY_HEIGHT + 0.14f, 1f);

        energyFillRenderer = CreateRenderer(
            "EnergyFill",
            squareSprite,
            new Color(0.95f, 0.68f, 0.18f, 1f),
            101
        );

        UpdateEnergyBar();
    }

    // ============================================================
    // 14. Game flow
    // ============================================================

    private IEnumerator FishingLoop()
    {
        gameRoutineRunning = true;
        playedEnergyFull = false;

        energy = 0f;
        combo = 0;
        judgeLine = "";
        UpdateEnergyBar();

        SetGoblinPull(false);

        phase = GamePhase.WaitingBite;
        statusLine = "Casting...";

        yield return WAIT_CAST;

        phase = GamePhase.Bite;
        statusLine = "BITE!";
        PlaySfx(biteClip, 1f);

        yield return WAIT_BITE;

        while (energy < 100f)
        {
            RhythmPattern pattern =
                patterns[Random.Range(0, patterns.Length)];

            yield return StartCoroutine(PlayPrompt(pattern));

            if (energy >= 100f)
                break;

            yield return WAIT_AFTER_PROMPT;

            yield return StartCoroutine(PlayResponse(pattern));

            if (energy < 100f)
                yield return WAIT_BETWEEN_ROUNDS;
        }

        ClearTargets();

        phase = GamePhase.Fever;
        statusLine = "FEVER TIME!";
        judgeLine = "";

        SetGoblinPull(false);

        yield return StartCoroutine(PlayFever());

        phase = GamePhase.Result;
        statusLine = "BIG FISH FINISH!  SPACE: AGAIN";
        judgeLine = "";
        gameRoutineRunning = false;
    }

    // ============================================================
    // 15. 鼓点提示：DSP排程
    // ============================================================

    // 【1.1修改】修改内容：保留 FH_1.0 的 DSP PlayScheduled 鼓点排程，
    // 本次只处理渲染/UI依赖，不改变节奏核心。
    private IEnumerator PlayPrompt(RhythmPattern pattern)
    {
        phase = GamePhase.Prompt;
        statusLine = "LISTEN: " + pattern.name;
        judgeLine = "";

        double start = AudioSettings.dspTime + 0.20;

        int count = Mathf.Min(
            pattern.offsets.Length,
            rhythmSources.Length
        );

        for (int i = 0; i < count; i++)
        {
            AudioClip clip = GetToneClip(pattern.tones[i]);

            if (clip == null)
                continue;

            AudioSource source = rhythmSources[i];

            source.Stop();
            source.clip = clip;
            source.volume = GetToneVolume(pattern.tones[i]);

            source.PlayScheduled(
                start + pattern.offsets[i]
            );
        }

        double end =
            start +
            pattern.offsets[pattern.offsets.Length - 1] +
            0.23;

        while (AudioSettings.dspTime < end)
            yield return null;
    }

    // ============================================================
    // 16. 玩家复现节奏
    // ============================================================

    private IEnumerator PlayResponse(RhythmPattern pattern)
    {
        phase = GamePhase.Response;
        statusLine = "FIGHT!";

        ClearTargets();

        double responseBase =
            AudioSettings.dspTime +
            approachTime +
            0.15;

        for (int i = 0; i < pattern.offsets.Length; i++)
        {
            CreateTarget(
                responseBase + pattern.offsets[i]
            );
        }

        while (phase == GamePhase.Response && energy < 100f)
        {
            bool allResolved = true;

            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i].active &&
                    !targets[i].resolved)
                {
                    allResolved = false;
                    break;
                }
            }

            if (allResolved)
                break;

            yield return null;
        }

        if (energy >= 100f)
            ClearTargets();
    }

    // ============================================================
    // 17. osu式判定圈
    // ============================================================

    // 【1.1修改】修改内容：osu!式“固定 Hit Circle + 外层 Approach Circle”逻辑保留，
    // 位置/缩放从 RectTransform 改为 Transform，显示从 Image 改为 SpriteRenderer。
    private void CreateTarget(double targetTime)
    {
        HitTarget t = AcquireTarget();

        if (t == null)
            return;

        Vector2 pos = new Vector2(
            Random.Range(-7.2f, 2.6f),
            Random.Range(-2.55f, 2.45f)
        );

        t.root.transform.position =
            new Vector3(pos.x, pos.y, 0f);

        // 固定 Hit Circle
        t.hit.transform.localScale =
            Vector3.one * 1.02f;

        // 【1.1修改】修改内容：目标初始透明度改为 0，
        // 使用 FH_1.0 的 approachFadeInTime / approachMaxAlpha 做淡入。
        t.hit.color =
            new Color(0.18f, 0.72f, 0.96f, 0f);

        // 外层 Approach Circle
        t.approach.transform.localScale =
            Vector3.one * approachStartScale;

        t.approach.color =
            new Color(0.85f, 0.97f, 1f, 0f);

        t.targetDsp = targetTime;
        t.spawnDsp = targetTime - approachTime;

        t.spawned = false;
        t.approachHidden = false;
        t.resolved = false;
        t.active = true;

        t.root.SetActive(false);
    }

    // 【1.1修改】修改内容：缩圈动画改为 SpriteRenderer/Transform 世界坐标实现，
    // 并继续使用 FH_1.0 的 approachMaxAlpha / approachFadeInTime。
    private void UpdateTargets()
    {
        if (targets == null)
            return;

        double now = AudioSettings.dspTime;

        for (int i = 0; i < targets.Length; i++)
        {
            HitTarget t = targets[i];

            if (!t.active || t.resolved)
                continue;

            if (!t.spawned && now >= t.spawnDsp)
            {
                t.spawned = true;
                t.root.SetActive(true);
                t.approach.enabled = true;
            }

            if (!t.spawned)
                continue;

            float progress = Mathf.Clamp01(
                (float)((now - t.spawnDsp) /
                (t.targetDsp - t.spawnDsp))
            );

            // Approach Circle 4x -> 1x
            float scale = Mathf.Lerp(
                approachStartScale,
                1f,
                progress
            );

            t.approach.transform.localScale =
                Vector3.one * scale;

            // 【1.1修改】修改内容：保留 FH_1.0 的 Hit/Approach 淡入观感，
            // 只是把 Image.color 改为 SpriteRenderer.color。
            float visibleSeconds =
                (float)(now - t.spawnDsp);

            float fade =
                Mathf.Clamp01(
                    visibleSeconds /
                    Mathf.Max(0.01f, approachFadeInTime)
                );

            Color hitColor = t.hit.color;
            hitColor.a = Mathf.Lerp(0f, 0.80f, fade);
            t.hit.color = hitColor;

            if (!t.approachHidden)
            {
                Color approachColor = t.approach.color;
                approachColor.a = approachMaxAlpha * fade;
                t.approach.color = approachColor;
            }

            // 到准确命中时刻，外圈消失。
            if (!t.approachHidden &&
                now >= t.targetDsp)
            {
                t.approachHidden = true;
                t.approach.enabled = false;
            }

            // Good窗口结束以后才算Miss。
            if (now > t.targetDsp + goodWindow)
            {
                ResolveTarget(t, JudgeResult.Miss);
            }
        }
    }

    private enum JudgeResult
    {
        Perfect,
        Good,
        Miss
    }

    // 【1.1修改】修改内容：点击检测不再使用 RectTransformUtility，
    // 改为 ScreenToWorldPoint + Vector2.Distance 圆形判定，不需要 UI Raycast/Collider。
    private void TryHitTarget()
    {
        Vector3 mouseWorld =
            gameCamera.ScreenToWorldPoint(
                Input.mousePosition
            );

        Vector2 mouse =
            new Vector2(mouseWorld.x, mouseWorld.y);

        double now = AudioSettings.dspTime;

        HitTarget best = null;
        double bestTimeDelta = 999.0;

        for (int i = 0; i < targets.Length; i++)
        {
            HitTarget t = targets[i];

            if (!t.active ||
                !t.spawned ||
                t.resolved)
                continue;

            Vector2 center =
                new Vector2(
                    t.root.transform.position.x,
                    t.root.transform.position.y
                );

            // 纯数学点击检测，不需要Collider。
            float distance =
                Vector2.Distance(mouse, center);

            if (distance > 0.66f)
                continue;

            double delta =
                System.Math.Abs(
                    now - t.targetDsp
                );

            if (delta < bestTimeDelta)
            {
                bestTimeDelta = delta;
                best = t;
            }
        }

        if (best == null)
            return;

        // 太早点不扣分。
        if (now < best.targetDsp - goodWindow)
        {
            judgeLine = "WAIT...";
            return;
        }

        double abs =
            System.Math.Abs(
                now - best.targetDsp
            );

        if (abs <= perfectWindow)
        {
            ResolveTarget(
                best,
                JudgeResult.Perfect
            );
        }
        else if (abs <= goodWindow)
        {
            ResolveTarget(
                best,
                JudgeResult.Good
            );
        }
    }

    private void ResolveTarget(
        HitTarget target,
        JudgeResult result)
    {
        if (target == null ||
            !target.active ||
            target.resolved)
            return;

        target.resolved = true;

        Vector2 pos =
            new Vector2(
                target.root.transform.position.x,
                target.root.transform.position.y
            );

        if (result == JudgeResult.Perfect)
        {
            combo++;
            judgeLine = "PERFECT!";

            energy =
                Mathf.Min(
                    100f,
                    energy + perfectEnergy
                );

            PlaySfx(perfectClip, 1f);
            PlaySfx(energyGainClip, 0.42f);

            SpawnBubbleBurst(pos, 14);
            SetGoblinPull(true);
        }
        else if (result == JudgeResult.Good)
        {
            combo++;
            judgeLine = "GOOD";

            energy =
                Mathf.Min(
                    100f,
                    energy + goodEnergy
                );

            PlaySfx(goodClip, 0.9f);
            PlaySfx(energyGainClip, 0.34f);

            SpawnBubbleBurst(pos, 8);
            SetGoblinPull(true);
        }
        else
        {
            combo = 0;
            judgeLine = "MISS";

            PlaySfx(missClip, 0.82f);
        }

        UpdateEnergyBar();

        if (energy >= 100f &&
            !playedEnergyFull)
        {
            playedEnergyFull = true;
            judgeLine = "ENERGY FULL!";

            PlaySfx(energyFullClip, 1f);
        }

        ReleaseTarget(target);
    }

    // ============================================================
    // 18. Fever
    // ============================================================

    // 【1.1修改】修改内容：Fever 玩法保持“鱼群连续撞脸 -> 超大鱼最后一击”，
    // 仅将显示层改为 SpriteRenderer 对象池。
    private IEnumerator PlayFever()
    {
        energy = 100f;
        UpdateEnergyBar();

        Vector2 face =
            GetGoblinFacePosition();

        for (int i = 0;
             i < feverFishCount;
             i++)
        {
            float p =
                feverFishCount <= 1
                ? 1f
                : (float)i /
                  (feverFishCount - 1);

            float interval =
                Mathf.Lerp(
                    feverFirstInterval,
                    feverLastInterval,
                    p
                );

            Vector2 start =
                new Vector2(
                    Random.Range(-8.0f, 2.3f),
                    Random.Range(-5.0f, -3.4f)
                );

            SpawnFeverFish(
                GetRandomFishSprite(),
                start,
                face +
                Random.insideUnitCircle * 0.42f,
                Random.Range(0.8f, 1.35f),
                Random.Range(0.34f, 0.50f)
            );

            float until =
                Time.time + interval;

            while (Time.time < until)
                yield return null;
        }

        statusLine = "...";
        judgeLine = "";

        yield return WAIT_BEFORE_BIG_FISH;

        statusLine = "FINAL FISH!";

        yield return StartCoroutine(
            HugeFishFinisher(face)
        );

    }

    // 【1.1修改】修改内容：最终大鱼由 PooledImage/Image 改为单个复用 SpriteRenderer，
    // 抛物线、旋转、放大、撞脸、震屏逻辑保留。
    private IEnumerator HugeFishFinisher(
        Vector2 face)
    {
        hugeFishTransform.gameObject.SetActive(true);

        hugeFishRenderer.sprite =
            hugeFishSprite != null
            ? hugeFishSprite
            : squareSprite;

        hugeFishRenderer.color =
            hugeFishSprite != null
            ? Color.white
            : new Color(
                0.98f,
                0.65f,
                0.15f,
                1f
            );

        Vector2 start =
            new Vector2(-5.5f, -5.0f);

        Vector2 control =
            new Vector2(0.5f, 4.1f);

        float duration = 0.72f;
        float age = 0f;

        while (age < duration)
        {
            age += Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    age / duration
                );

            Vector2 a =
                Vector2.Lerp(
                    start,
                    control,
                    t
                );

            Vector2 b =
                Vector2.Lerp(
                    control,
                    face,
                    t
                );

            Vector2 p =
                Vector2.Lerp(
                    a,
                    b,
                    t
                );

            hugeFishTransform.position =
                new Vector3(
                    p.x,
                    p.y,
                    0f
                );

            float scale =
                Mathf.Lerp(
                    0.7f,
                    3.0f,
                    t
                );

            hugeFishTransform.localScale =
                new Vector3(
                    scale * 1.65f,
                    scale,
                    1f
                );

            hugeFishTransform.rotation =
                Quaternion.Euler(
                    0f,
                    0f,
                    Mathf.Lerp(
                        -24f,
                        17f,
                        t
                    )
                );

            yield return null;
        }

        PlaySfx(bigSplashClip, 1f);
        PlaySfx(fishFaceHitClip, 1f);

        SpawnBubbleBurst(face, 30);

        yield return StartCoroutine(
            ScreenShake(
                0.30f,
                0.18f
            )
        );

        judgeLine = "BOOM!!";

        yield return WAIT_AFTER_BIG_FISH;

        hugeFishTransform.gameObject.SetActive(false);
    }

    // ============================================================
    // 19. Fever普通鱼对象池
    // ============================================================

    // 【1.1修改】修改内容：Fever 普通鱼对象池改为 SpriteRenderer；
    // 鱼素材不可用时自动用彩色矩形代替，不阻断 Demo。
    private void SpawnFeverFish(
        Sprite sprite,
        Vector2 start,
        Vector2 end,
        float size,
        float duration)
    {
        int slot = FindFreeFish();

        if (slot < 0)
            return;

        FishFx fx = fishPool[slot];

        fx.active = true;
        fx.impacted = false;

        fx.start = start;
        fx.end = end;

        fx.control =
            Vector2.Lerp(
                start,
                end,
                0.5f
            );

        fx.control.y +=
            Random.Range(2.2f, 3.8f);

        fx.age = 0f;
        fx.duration = duration;
        fx.hold = 0.08f;

        fx.startAngle = -25f;
        fx.endAngle = 25f;

        fx.sr.sprite =
            sprite != null
            ? sprite
            : squareSprite;

        fx.sr.color =
            sprite != null
            ? Color.white
            : FishColors[
                Random.Range(
                    0,
                    FishColors.Length
                )
              ];

        fx.tr.position =
            new Vector3(
                start.x,
                start.y,
                0f
            );

        if (sprite == null)
        {
            fx.tr.localScale =
                new Vector3(
                    size * 1.5f,
                    size * 0.70f,
                    1f
                );
        }
        else
        {
            fx.tr.localScale =
                Vector3.one * size;
        }

        fx.tr.gameObject.SetActive(true);

        fishPool[slot] = fx;
    }

    private void UpdateFeverFish()
    {
        if (fishPool == null)
            return;

        float dt = Time.deltaTime;

        for (int i = 0;
             i < fishPool.Length;
             i++)
        {
            FishFx fx = fishPool[i];

            if (!fx.active)
                continue;

            fx.age += dt;

            if (fx.age <= fx.duration)
            {
                float t =
                    Mathf.Clamp01(
                        fx.age /
                        fx.duration
                    );

                Vector2 a =
                    Vector2.Lerp(
                        fx.start,
                        fx.control,
                        t
                    );

                Vector2 b =
                    Vector2.Lerp(
                        fx.control,
                        fx.end,
                        t
                    );

                Vector2 p =
                    Vector2.Lerp(
                        a,
                        b,
                        t
                    );

                fx.tr.position =
                    new Vector3(
                        p.x,
                        p.y,
                        0f
                    );

                fx.tr.rotation =
                    Quaternion.Euler(
                        0f,
                        0f,
                        Mathf.Lerp(
                            fx.startAngle,
                            fx.endAngle,
                            t
                        )
                    );
            }
            else
            {
                if (!fx.impacted)
                {
                    fx.impacted = true;

                    PlaySfx(
                        fishFaceHitClip,
                        0.72f
                    );

                    SpawnBubbleBurst(
                        fx.end,
                        4
                    );
                }

                float holdT =
                    (fx.age -
                     fx.duration) /
                    fx.hold;

                if (holdT >= 1f)
                {
                    fx.active = false;
                    fx.tr.gameObject.SetActive(false);
                }
            }

            fishPool[i] = fx;
        }
    }

    // ============================================================
    // 20. 泡泡对象池
    // ============================================================

    // 【1.1修改】修改内容：删除 FishingBubbleBatchGraphic / MaskableGraphic / VertexHelper 依赖，
    // 泡泡改为固定 SpriteRenderer 对象池，解决当前工程缺少 UGUI 的编译错误。
    private void SpawnBubbleBurst(
        Vector2 center,
        int count)
    {
        for (int n = 0;
             n < count;
             n++)
        {
            int slot = FindFreeBubble();

            if (slot < 0)
                return;

            BubbleFx fx = bubbles[slot];

            Vector2 dir =
                Random.insideUnitCircle;

            if (dir.sqrMagnitude < 0.001f)
                dir = Vector2.up;
            else
                dir.Normalize();

            fx.active = true;

            fx.velocity =
                dir *
                Random.Range(
                    1.6f,
                    4.3f
                );

            fx.age = 0f;

            fx.life =
                Random.Range(
                    0.34f,
                    0.62f
                );

            fx.baseSize =
                Random.Range(
                    0.16f,
                    0.42f
                );

            fx.tr.position =
                new Vector3(
                    center.x,
                    center.y,
                    -0.1f
                );

            fx.tr.localScale =
                Vector3.one *
                fx.baseSize;

            fx.sr.color =
                new Color(
                    0.75f,
                    0.95f,
                    1f,
                    0.90f
                );

            fx.tr.gameObject.SetActive(true);

            bubbles[slot] = fx;
        }
    }

    private void UpdateBubbles()
    {
        if (bubbles == null)
            return;

        float dt = Time.deltaTime;

        for (int i = 0;
             i < bubbles.Length;
             i++)
        {
            BubbleFx fx = bubbles[i];

            if (!fx.active)
                continue;

            fx.age += dt;

            if (fx.age >= fx.life)
            {
                fx.active = false;
                fx.tr.gameObject.SetActive(false);
                bubbles[i] = fx;
                continue;
            }

            fx.velocity +=
                Vector2.down *
                1.1f *
                dt;

            fx.tr.position +=
                new Vector3(
                    fx.velocity.x,
                    fx.velocity.y,
                    0f
                ) * dt;

            float t =
                fx.age /
                fx.life;

            float scale =
                fx.baseSize *
                Mathf.Lerp(
                    0.65f,
                    1.35f,
                    t
                );

            fx.tr.localScale =
                Vector3.one *
                scale;

            Color c = fx.sr.color;

            c.a =
                Mathf.Lerp(
                    0.90f,
                    0f,
                    t
                );

            fx.sr.color = c;

            bubbles[i] = fx;
        }
    }

    // ============================================================
    // 21. 屏幕震动
    // ============================================================

    // 【1.1修改】修改内容：屏幕震动对象从 stageRT 改为 Camera.transform。
    private IEnumerator ScreenShake(
        float duration,
        float strength)
    {
        Vector3 original =
            gameCamera.transform.position;

        float age = 0f;

        while (age < duration)
        {
            age += Time.deltaTime;

            Vector2 offset =
                Random.insideUnitCircle *
                strength;

            gameCamera.transform.position =
                original +
                new Vector3(
                    offset.x,
                    offset.y,
                    0f
                );

            yield return null;
        }

        gameCamera.transform.position =
            original;
    }

    // ============================================================
    // 22. 对象池预创建
    // ============================================================

    // 【1.1修改】修改内容：保留对象池思想，但池元素全部从 UI Image 改成 SpriteRenderer。
    private void PrewarmPools()
    {
        targets =
            new HitTarget[
                TARGET_POOL_SIZE
            ];

        for (int i = 0;
             i < targets.Length;
             i++)
        {
            GameObject root =
                new GameObject(
                    "HitTarget_" + i
                );

            root.transform.SetParent(
                worldRoot,
                false
            );

            SpriteRenderer hit =
                CreateChildRenderer(
                    root.transform,
                    "Hit",
                    circleSprite,
                    new Color(
                        0.18f,
                        0.72f,
                        0.96f,
                        0.82f
                    ),
                    40
                );

            SpriteRenderer approach =
                CreateChildRenderer(
                    root.transform,
                    "Approach",
                    ringSprite,
                    new Color(
                        0.85f,
                        0.97f,
                        1f,
                        0.90f
                    ),
                    41
                );

            targets[i] =
                new HitTarget
                {
                    root = root,
                    hit = hit,
                    approach = approach,
                    active = false,
                    resolved = true
                };

            root.SetActive(false);
        }

        bubbles =
            new BubbleFx[
                bubblePoolSize
            ];

        for (int i = 0;
             i < bubbles.Length;
             i++)
        {
            SpriteRenderer sr =
                CreateRenderer(
                    "Bubble_" + i,
                    circleSprite,
                    Color.white,
                    60
                );

            sr.transform.SetParent(
                worldRoot,
                true
            );

            sr.gameObject.SetActive(false);

            bubbles[i] =
                new BubbleFx
                {
                    tr = sr.transform,
                    sr = sr,
                    active = false
                };
        }

        fishPool =
            new FishFx[
                fishPoolSize
            ];

        for (int i = 0;
             i < fishPool.Length;
             i++)
        {
            SpriteRenderer sr =
                CreateRenderer(
                    "FeverFish_" + i,
                    squareSprite,
                    Color.white,
                    55
                );

            sr.transform.SetParent(
                worldRoot,
                true
            );

            sr.gameObject.SetActive(false);

            fishPool[i] =
                new FishFx
                {
                    tr = sr.transform,
                    sr = sr,
                    active = false
                };
        }

        hugeFishRenderer =
            CreateRenderer(
                "HugeFish",
                squareSprite,
                new Color(
                    0.98f,
                    0.65f,
                    0.15f,
                    1f
                ),
                70
            );

        hugeFishTransform =
            hugeFishRenderer.transform;

        hugeFishTransform.SetParent(
            worldRoot,
            true
        );

        hugeFishTransform.gameObject.SetActive(false);
    }

    // ============================================================
    // 23. 对象池工具
    // ============================================================

    private HitTarget AcquireTarget()
    {
        for (int i = 0;
             i < targets.Length;
             i++)
        {
            if (!targets[i].active)
            {
                targets[i].active = true;
                targets[i].resolved = false;
                targets[i].spawned = false;
                targets[i].approachHidden = false;

                return targets[i];
            }
        }

        return null;
    }

    private void ReleaseTarget(
        HitTarget t)
    {
        if (t == null)
            return;

        t.active = false;
        t.spawned = false;
        t.resolved = true;
        t.approachHidden = false;

        t.root.SetActive(false);
    }

    private void ClearTargets()
    {
        if (targets == null)
            return;

        for (int i = 0;
             i < targets.Length;
             i++)
        {
            if (targets[i].active)
                ReleaseTarget(
                    targets[i]
                );
        }
    }

    private int FindFreeBubble()
    {
        for (int i = 0;
             i < bubbles.Length;
             i++)
        {
            if (!bubbles[i].active)
                return i;
        }

        return -1;
    }

    private int FindFreeFish()
    {
        for (int i = 0;
             i < fishPool.Length;
             i++)
        {
            if (!fishPool[i].active)
                return i;
        }

        return -1;
    }

    // ============================================================
    // 24. 能量条
    // ============================================================

    // 【1.1修改】修改内容：能量条宽度不再修改 RectTransform.sizeDelta，
    // 改为 Transform.localScale，并保持左端固定。
    private void UpdateEnergyBar()
    {
        if (energyFillRenderer == null)
            return;

        float p =
            Mathf.Clamp01(
                energy / 100f
            );

        float width =
            ENERGY_WIDTH * p;

        float left =
            -ENERGY_WIDTH * 0.5f;

        energyFillRenderer.transform.position =
            new Vector3(
                left +
                width * 0.5f,
                4.55f,
                -0.1f
            );

        energyFillRenderer.transform.localScale =
            new Vector3(
                Mathf.Max(
                    0.001f,
                    width
                ),
                ENERGY_HEIGHT,
                1f
            );

        if (p >= 1f)
        {
            energyFillRenderer.color =
                new Color(
                    1f,
                    0.25f,
                    0.18f,
                    1f
                );
        }
        else if (p >= 0.8f)
        {
            energyFillRenderer.color =
                new Color(
                    1f,
                    0.45f,
                    0.10f,
                    1f
                );
        }
        else
        {
            energyFillRenderer.color =
                new Color(
                    0.95f,
                    0.68f,
                    0.18f,
                    1f
                );
        }
    }

    // ============================================================
    // 25. 哥布林
    // ============================================================

    // 【1.1修改】修改内容：哥布林动作切换由 Image.sprite 改为 SpriteRenderer.sprite。
    private void SetGoblinPull(
        bool pulling)
    {
        if (goblinRenderer == null)
            return;

        if (pulling &&
            goblinPullSprite != null)
        {
            goblinRenderer.sprite =
                goblinPullSprite;

            goblinRenderer.color =
                Color.white;

            FitSpriteHeight(
                goblinRenderer,
                4.8f
            );
        }
        else if (
            goblinIdleSprite != null)
        {
            goblinRenderer.sprite =
                goblinIdleSprite;

            goblinRenderer.color =
                Color.white;

            FitSpriteHeight(
                goblinRenderer,
                4.8f
            );
        }
    }

    private Vector2 GetGoblinFacePosition()
    {
        return new Vector2(
            goblinBasePosition.x,
            goblinBasePosition.y + 1.55f
        );
    }

    // ============================================================
    // 26. Audio
    // ============================================================

    private void BuildAudio()
    {
        sfxSource =
            gameObject.AddComponent<
                AudioSource
            >();

        sfxSource.playOnAwake = false;
        sfxSource.loop = false;
        sfxSource.spatialBlend = 0f;

        // 【1.2修改】修改内容：明确主音效 AudioSource 为可播放状态。
        // 保持 2D 音频，不静音，基础音量保持 1。
        sfxSource.mute = false;
        sfxSource.volume = 1f;

        rhythmSources =
            new AudioSource[
                MAX_RHYTHM_BEATS
            ];

        for (int i = 0;
             i < rhythmSources.Length;
             i++)
        {
            AudioSource source =
                gameObject.AddComponent<
                    AudioSource
                >();

            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;

            // 【1.2修改】修改内容：明确每个 DSP 鼓点 AudioSource 为可播放状态。
            // PlayScheduled 仍保留原 1.1 时序逻辑，只补全音频状态。
            source.mute = false;
            source.volume = 1f;

            rhythmSources[i] = source;
        }
    }

    private void PlaySfx(
        AudioClip clip,
        float volume)
    {
        if (clip == null ||
            sfxSource == null)
            return;

        sfxSource.PlayOneShot(
            clip,
            volume
        );
    }

    private AudioClip GetToneClip(
        int tone)
    {
        if (tone == 1)
            return drumHigh;

        if (tone == 2)
            return drumAccent;

        return drumLow;
    }

    private float GetToneVolume(
        int tone)
    {
        return tone == 2
            ? 1f
            : 0.95f;
    }

    // ============================================================
    // 27. Rhythm patterns
    // ============================================================

    private void BuildPatterns()
    {
        patterns =
            new RhythmPattern[]
            {
                new RhythmPattern(
                    "STEADY",
                    new float[]
                    {
                        0f,
                        0.82f,
                        1.64f
                    },
                    new int[]
                    {
                        0,
                        0,
                        1
                    }
                ),

                new RhythmPattern(
                    "DOUBLE",
                    new float[]
                    {
                        0f,
                        0.82f,
                        1.28f,
                        2.10f
                    },
                    new int[]
                    {
                        0,
                        0,
                        1,
                        2
                    }
                ),

                new RhythmPattern(
                    "PAUSE",
                    new float[]
                    {
                        0f,
                        0.64f,
                        1.52f,
                        2.34f
                    },
                    new int[]
                    {
                        0,
                        1,
                        0,
                        2
                    }
                ),

                new RhythmPattern(
                    "BOUNCE",
                    new float[]
                    {
                        0f,
                        0.72f,
                        1.42f,
                        1.82f
                    },
                    new int[]
                    {
                        0,
                        1,
                        0,
                        1
                    }
                )
            };
    }

    private Sprite GetRandomFishSprite()
    {
        if (feverFishSprites == null ||
            feverFishSprites.Length == 0)
            return null;

        int tries =
            feverFishSprites.Length;

        while (tries-- > 0)
        {
            Sprite s =
                feverFishSprites[
                    Random.Range(
                        0,
                        feverFishSprites.Length
                    )
                ];

            if (s != null)
                return s;
        }

        return null;
    }

    // ============================================================
    // 28. Sprite / Renderer helper
    // ============================================================

    // 【1.1修改】修改内容：新增 SpriteRenderer 创建工具，替代 FH_1.0 的 CreateImageObject()。
    private SpriteRenderer CreateRenderer(
        string name,
        Sprite sprite,
        Color color,
        int sortingOrder)
    {
        GameObject go =
            new GameObject(name);

        SpriteRenderer sr =
            go.AddComponent<
                SpriteRenderer
            >();

        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = sortingOrder;

        if (worldRoot != null)
        {
            go.transform.SetParent(
                worldRoot,
                false
            );
        }

        return sr;
    }

    private SpriteRenderer CreateChildRenderer(
        Transform parent,
        string name,
        Sprite sprite,
        Color color,
        int sortingOrder)
    {
        GameObject go =
            new GameObject(name);

        go.transform.SetParent(
            parent,
            false
        );

        SpriteRenderer sr =
            go.AddComponent<
                SpriteRenderer
            >();

        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = sortingOrder;

        return sr;
    }

    private void FitSpriteHeight(
        SpriteRenderer sr,
        float wantedHeight)
    {
        if (sr == null ||
            sr.sprite == null)
            return;

        float sourceHeight =
            sr.sprite.bounds.size.y;

        if (sourceHeight <= 0.0001f)
            return;

        float s =
            wantedHeight /
            sourceHeight;

        sr.transform.localScale =
            new Vector3(
                s,
                s,
                1f
            );
    }

    // ============================================================
    // 29. Generated sprites
    // ============================================================

    // 【1.1修改】修改内容：基础方块/实心圆/空心圆继续由程序生成，
    // 所以素材暂时不可用时 Demo 仍然能运行。
    private void BuildGeneratedSprites()
    {
        squareSprite =
            CreateSquareSprite();

        circleSprite =
            CreateCircleSprite(
                64,
                false
            );

        ringSprite =
            CreateCircleSprite(
                64,
                true
            );
    }

    private Sprite CreateSquareSprite()
    {
        Texture2D tex =
            new Texture2D(
                1,
                1,
                TextureFormat.RGBA32,
                false
            );

        tex.filterMode =
            FilterMode.Point;

        tex.SetPixel(
            0,
            0,
            Color.white
        );

        tex.Apply(
            false,
            true
        );

        return Sprite.Create(
            tex,
            new Rect(
                0,
                0,
                1,
                1
            ),
            new Vector2(
                0.5f,
                0.5f
            ),
            1f
        );
    }

    private Sprite CreateCircleSprite(
        int size,
        bool ring)
    {
        Texture2D tex =
            new Texture2D(
                size,
                size,
                TextureFormat.RGBA32,
                false
            );

        tex.filterMode =
            FilterMode.Point;

        tex.wrapMode =
            TextureWrapMode.Clamp;

        Color[] pixels =
            new Color[
                size * size
            ];

        float center =
            (size - 1) * 0.5f;

        float outer =
            size * 0.46f;

        float inner =
            outer - 4f;

        float outer2 =
            outer * outer;

        float inner2 =
            inner * inner;

        for (int y = 0;
             y < size;
             y++)
        {
            for (int x = 0;
                 x < size;
                 x++)
            {
                float dx =
                    x - center;

                float dy =
                    y - center;

                float d2 =
                    dx * dx +
                    dy * dy;

                bool visible;

                if (ring)
                {
                    visible =
                        d2 <= outer2 &&
                        d2 >= inner2;
                }
                else
                {
                    visible =
                        d2 <= outer2;
                }

                pixels[
                    y * size + x
                ] =
                    visible
                    ? Color.white
                    : Color.clear;
            }
        }

        tex.SetPixels(pixels);

        tex.Apply(
            false,
            true
        );

        return Sprite.Create(
            tex,
            new Rect(
                0,
                0,
                size,
                size
            ),
            new Vector2(
                0.5f,
                0.5f
            ),
            size
        );
    }

    private void DestroyGeneratedSprite(
        Sprite sprite)
    {
        if (sprite == null)
            return;

        Texture2D tex =
            sprite.texture;

        Destroy(sprite);

        if (tex != null)
            Destroy(tex);
    }

    // ============================================================
    // 30. Debug overlay
    // ============================================================

    // 【1.2修改】修改内容：此函数本体继续复用，
    // 但调用位置从 Awake() 移到了 OnGUI()，确保 GUI.skin 在正确上下文中访问。
    private void InitDebugStyles()
    {
        titleStyle =
            new GUIStyle(
                GUI.skin.label
            );

        titleStyle.fontSize = 28;
        titleStyle.fontStyle =
            FontStyle.Bold;

        titleStyle.normal.textColor =
            Color.white;

        smallStyle =
            new GUIStyle(
                GUI.skin.label
            );

        smallStyle.fontSize = 18;

        smallStyle.normal.textColor =
            Color.white;
    }

    // 【1.1修改】修改内容：仅保留 OnGUI 作为开发调试文字；
    // OnGUI 属于 UnityEngine 核心，不依赖 UnityEngine.UI。
    private void OnGUI()
    {
        if (!showDebugOverlay)
            return;

        // 【1.2修改】修改内容：GUIStyle 改为在 OnGUI() 中延迟初始化。
        // 只有调试层实际开启时才访问 GUI.skin，避免 Awake() 阶段的 IMGUI 调用时机问题。
        if (titleStyle == null ||
            smallStyle == null)
        {
            InitDebugStyles();
        }

        GUI.Box(
            new Rect(
                20,
                20,
                540,
                126
            ),
            ""
        );

        GUI.Label(
            new Rect(
                38,
                31,
                500,
                35
            ),
            "FISHING HEAVEN - FH 1.1",
            titleStyle
        );

        GUI.Label(
            new Rect(
                38,
                70,
                500,
                27
            ),
            statusLine,
            smallStyle
        );

        GUI.Label(
            new Rect(
                38,
                98,
                500,
                27
            ),
            "Judge: " +
            judgeLine +
            "    Combo: " +
            combo +
            "    Energy: " +
            Mathf.RoundToInt(energy) +
            "%",
            smallStyle
        );
    }
}
