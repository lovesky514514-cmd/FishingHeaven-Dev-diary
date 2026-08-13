using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 【注释说明】
/// // 这是单行注释：适合解释下一行代码做什么。
/// /* ... */ 是多行注释：适合临时说明一整段逻辑。
/// /// 是 XML 文档注释：适合类、方法、公开字段的说明。
/// </summary>
public class FishingHeavenDemo : MonoBehaviour
{
    // ============================================================
    // 1. 游戏状态
    // ============================================================

    public enum GamePhase
    {
        Idle,       // 待机
        WaitingBite,// 等鱼咬钩
        Bite,       // 咬钩提示
        Prompt,     // 只听鼓点
        Response,   // 玩家点击缩圈
        Fever,      // 鱼群糊脸
        Result      // 本轮结束
    }

    // ============================================================
    // 2. Inspector：美术资源
    // ============================================================

    [Header("=== 美术资源（可选） ===")]
    [Tooltip("鱼塘背景。留空时会自动生成简单天空+水面占位。")]
    public Sprite pondBackground;

    [Tooltip("哥布林待机图。")]
    public Sprite goblinIdleSprite;

    [Tooltip("哥布林拉竿/用力图。没有可以留空。")]
    public Sprite goblinPullSprite;

    [Tooltip("Fever 时飞向哥布林脸上的鱼。")]
    public Sprite[] feverFishSprites;

    [Tooltip("Fever 最后一击的超大鱼。")]
    public Sprite hugeFishSprite;

    // ============================================================
    // 3. Inspector：音效
    // ============================================================

    [Header("=== 音效：鼓点 ===")]
    public AudioClip drumLow;
    public AudioClip drumHigh;
    public AudioClip drumAccent;

    [Header("=== 音效：判定 ===")]
    public AudioClip perfectClip;
    public AudioClip goodClip;
    public AudioClip missClip;

    [Header("=== 音效：钓鱼 / 能量 / Fever ===")]
    public AudioClip biteClip;
    public AudioClip energyGainClip;
    public AudioClip energyFullClip;
    public AudioClip fishFaceHitClip;
    public AudioClip bigSplashClip;

    // ============================================================
    // 4. Inspector：手感参数
    // ============================================================

    [Header("=== 节奏手感 ===")]
    [Tooltip("判定圈从大缩到目标大小所需秒数。越大越容易看清")]
    [Range(0.45f, 1.5f)]
    public float approachTime = 1.20f;

    [Tooltip("Perfect 判定窗口。0.10 = ±100ms")]
    [Range(0.05f, 0.16f)]
    public float perfectWindow = 0.10f;

    [Tooltip("Good 判定窗口。Demo 故意放宽，方便进入爽点")]
    [Range(0.12f, 0.30f)]
    public float goodWindow = 0.22f;

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

    [Header("=== 能量 ===")]
    [Range(1, 50)]
    public int perfectEnergy = 20;

    [Range(1, 50)]
    public int goodEnergy = 12;

    [Header("=== Fever ===")]
    [Range(8, 40)]
    public int feverFishCount = 20;

    [Tooltip("Fever 开头每条鱼之间的间隔")]
    public float feverFirstInterval = 0.23f;

    [Tooltip("Fever 后半段最短间隔。越小越疯狂")]
    public float feverLastInterval = 0.065f;

    // ============================================================
    // 5. Inspector：性能参数
    // ============================================================

    [Header("=== 性能 / 对象池 ===")]
    [Tooltip("泡泡预创建数量。64 对当前 Demo 足够；机器较弱可降到 48")]
    [Range(32, 96)]
    public int bubblePoolSize = 64;

    [Tooltip("Fever 鱼预创建数量。并不是一共只能出现这么多，而是同屏上限")]
    [Range(12, 32)]
    public int fishPoolSize = 20;

    [Tooltip("是否显示左上角开发调试信息")]
    public bool showDebugOverlay = false;

    [Header("=== 调试状态（只读观察） ===")]
    [SerializeField] private GamePhase phase = GamePhase.Idle;
    [SerializeField] private float energy = 0f;
    [SerializeField] private int combo = 0;

    // ============================================================
    // 6. 常量 / 缓存
    // ============================================================

    private const float REF_W = 1920f;
    private const float REF_H = 1080f;

    private const int TARGET_POOL_SIZE = 8;
    private const int FLASH_POOL_SIZE = 4;

    // 【性能】静态颜色表：只创建一次，不要在 RandomFishColor() 里反复 new 数组。
    private static readonly Color[] FallbackFishColors =
    {
        new Color(0.78f, 0.86f, 0.94f, 1f),
        new Color(0.96f, 0.65f, 0.24f, 1f),
        new Color(0.47f, 0.72f, 0.94f, 1f),
        new Color(0.80f, 0.45f, 0.82f, 1f),
        new Color(0.94f, 0.84f, 0.40f, 1f)
    };

    // 【性能】固定等待时间缓存，不要每一轮都 new WaitForSeconds。
    private static readonly WaitForSeconds WAIT_CAST = new WaitForSeconds(0.65f);
    private static readonly WaitForSeconds WAIT_BITE = new WaitForSeconds(0.70f);
    private static readonly WaitForSeconds WAIT_AFTER_PROMPT = new WaitForSeconds(0.32f);
    private static readonly WaitForSeconds WAIT_BETWEEN_ROUNDS = new WaitForSeconds(0.52f);
    private static readonly WaitForSeconds WAIT_BEFORE_BIG_FISH = new WaitForSeconds(0.36f);
    private static readonly WaitForSeconds WAIT_AFTER_BIG_FISH = new WaitForSeconds(0.70f);

    private Canvas canvas;
    private RectTransform canvasRT;
    private RectTransform stageRT;
    private Image goblinImage;
    private RectTransform goblinRT;
    private RectTransform energyFillRT;
    private Image energyFillImage;

    private AudioSource audioSource;
    // 4个独立音源：当前节奏模板最多4拍，可以一次性用DSP时间排程。
    private AudioSource[] rhythmSources;

    // 运行时占位 Sprite：只在 Awake 创建一次。
    private Sprite whiteSprite;
    private Sprite ringSprite;
    private Sprite hitDiscSprite;
    private Sprite bubbleSprite;

    private bool gameRoutineRunning;
    private bool playedEnergyFull;

    private string statusLine = "SPACE: START FISHING";
    private string judgeLine = "";
    private string debugLine = "Judge:     Combo: 0";

    // 【性能】OnGUI 的样式只初始化一次。
    private GUIStyle titleStyle;
    private GUIStyle smallStyle;

    private readonly List<RhythmPattern> patterns = new List<RhythmPattern>(4);

    // ============================================================
    // 7. 对象池数据结构
    // ============================================================

    /// <summary>
    /// 一个预创建的 UI Image。
    /// 游玩中只 SetActive(true/false)，不 Destroy。
    /// </summary>
    private sealed class PooledImage
    {
        public GameObject go;
        public RectTransform rt;
        public Image image;
        public bool inUse;
    }

    private sealed class HitTarget
    {
        // 固定内圈：真正点击区域。
        public PooledImage hit;

        // 外层缩圈：只负责告诉玩家“什么时候点”。
        public PooledImage approach;

        public double spawnDsp;
        public double targetDsp;
        public bool spawned;
        public bool approachHidden;
        public bool resolved;
        public bool active;
    }

    private struct FishFx
    {
        public bool active;
        public bool impacted;
        public PooledImage item;
        public Vector2 start;
        public Vector2 control;
        public Vector2 end;
        public float elapsed;
        public float duration;
        public float hold;
        public float startAngle;
        public float endAngle;
    }

    private struct FlashFx
    {
        public bool active;
        public PooledImage item;
        public float elapsed;
        public float life;
        public float startScale;
        public float endScale;
    }

    private HitTarget[] targetPool;

    // 【V3性能】整屏泡泡只用一个 Graphic 批量绘制。
    private FishingBubbleBatchGraphic bubbleBatch;

    private PooledImage[] fishPool;
    private FishFx[] fishFx;

    private PooledImage[] flashPool;
    private FlashFx[] flashFx;

    // 超大鱼只需要一个对象，单独复用。
    private PooledImage hugeFishItem;

    // ============================================================
    // 8. 节奏模板
    // ============================================================

    private sealed class RhythmPattern
    {
        public readonly string name;
        public readonly float[] offsets;
        public readonly int[] tones; // 0=低鼓，1=高鼓，2=重音

        public RhythmPattern(string n, float[] o, int[] t)
        {
            name = n;
            offsets = o;
            tones = t;
        }
    }

    // ============================================================
    // 9. 生命周期
    // ============================================================

    private void Awake()
    {
        // 音频源只创建一次。
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;

        // 这些小纹理仅用于“没放素材也能运行”的占位和特效。
        whiteSprite = CreateSolidSprite(8, 8, Color.white);
        ringSprite = CreateRingSprite(64, 5);
        hitDiscSprite = CreateCircleSprite(64);
        bubbleSprite = CreateBubbleSprite(32);

        // 鼓点用独立 AudioSource 提前排进 DSP 时间轴。
        rhythmSources = new AudioSource[4];
        for (int i = 0; i < rhythmSources.Length; i++)
        {
            rhythmSources[i] = gameObject.AddComponent<AudioSource>();
            rhythmSources[i].playOnAwake = false;
            rhythmSources[i].loop = false;
            rhythmSources[i].spatialBlend = 0f;
        }

        BuildPatterns();
        BuildRuntimeUI();

        // 【性能核心】所有高频对象在进入游戏前一次性创建。
        PrewarmPools();

        InitDebugStyles();
        UpdateEnergyBar();

        phase = GamePhase.Idle;
    }

    private void Update()
    {
        // 这些更新函数只遍历固定长度数组，不产生运行时对象垃圾。
        UpdateHitTargets();

        // 单个批量组件更新全部泡泡，不再遍历几十个 Image GameObject。
        if (bubbleBatch != null)
            bubbleBatch.Tick(Time.deltaTime);

        UpdateFishEffects();
        UpdateFlashEffects();

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
        // 【内存】释放脚本自己生成的小纹理 / Sprite。
        DestroyGeneratedSprite(whiteSprite);
        DestroyGeneratedSprite(ringSprite);
        DestroyGeneratedSprite(hitDiscSprite);
        DestroyGeneratedSprite(bubbleSprite);
    }

    // ============================================================
    // 10. 调试信息
    // ============================================================

    private void InitDebugStyles()
    {
        titleStyle = new GUIStyle(GUI.skin.label);
        titleStyle.fontSize = 28;
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.normal.textColor = Color.white;

        smallStyle = new GUIStyle(GUI.skin.label);
        smallStyle.fontSize = 18;
        smallStyle.normal.textColor = Color.white;

        RefreshDebugLine();
    }

    private void OnGUI()
    {
        // 发布时把 showDebugOverlay 关掉，可以避免 OnGUI 这部分额外开销。
        if (!showDebugOverlay)
            return;

        GUI.Box(new Rect(24, 22, 520, 118), "");
        GUI.Label(new Rect(42, 34, 480, 38), "FISHING HEAVEN - DEMO", titleStyle);
        GUI.Label(new Rect(42, 72, 470, 28), statusLine, smallStyle);
        GUI.Label(new Rect(42, 102, 470, 28), debugLine, smallStyle);

        if (phase == GamePhase.Response)
        {
            GUI.Label(
                new Rect(42, 136, 500, 28),
                "LEFT CLICK the shrinking circles",
                smallStyle
            );
        }
    }

    private void RefreshDebugLine()
    {
        // 只在判定 / Combo 变化时创建一次字符串，不在每帧拼接。
        debugLine = "Judge: " + judgeLine + "    Combo: " + combo;
    }

    // ============================================================
    // 11. 核心游戏流程
    // ============================================================

    private IEnumerator FishingLoop()
    {
        gameRoutineRunning = true;
        playedEnergyFull = false;
        energy = 0f;
        combo = 0;
        judgeLine = "";
        RefreshDebugLine();
        UpdateEnergyBar();

        SetGoblinSprite(goblinIdleSprite);

        phase = GamePhase.WaitingBite;
        statusLine = "Casting... waiting for a bite";
        yield return WAIT_CAST;

        phase = GamePhase.Bite;
        statusLine = "BITE!";
        PlaySfx(biteClip, 1f);
        yield return WAIT_BITE;

        while (energy < 100f)
        {
            RhythmPattern pattern = patterns[Random.Range(0, patterns.Count)];

            yield return StartCoroutine(PlayPrompt(pattern));

            if (energy >= 100f)
                break;

            yield return WAIT_AFTER_PROMPT;
            yield return StartCoroutine(PlayResponse(pattern));

            if (energy < 100f)
                yield return WAIT_BETWEEN_ROUNDS;
        }

        ClearAllTargets();

        phase = GamePhase.Fever;
        statusLine = "FEVER TIME!!";
        SetGoblinSprite(goblinIdleSprite);

        yield return StartCoroutine(PlayFever());

        phase = GamePhase.Result;
        statusLine = "BIG FISH FINISH!  SPACE: FISH AGAIN";
        judgeLine = "";
        RefreshDebugLine();
        gameRoutineRunning = false;
    }

    /// <summary>
    /// 播放“先听鼓点”。
    /// 这里用 AudioSettings.dspTime 保持节奏计时稳定。
    /// </summary>
    private IEnumerator PlayPrompt(RhythmPattern pattern)
    {
        phase = GamePhase.Prompt;
        statusLine = "LISTEN: " + pattern.name;
        judgeLine = "";
        RefreshDebugLine();

        // 【节奏优化】
        // 一次性把整段鼓点安排到 DSP 时间轴，而不是每一拍都 while 等待后再 PlayOneShot。
        // 这样在偶发掉帧时，声音依然会按音频时钟落在正确位置。
        double start = AudioSettings.dspTime + 0.22;

        int count = Mathf.Min(pattern.offsets.Length, rhythmSources.Length);

        for (int i = 0; i < count; i++)
        {
            AudioClip clip = GetToneClip(pattern.tones[i]);

            if (clip == null)
                continue;

            AudioSource source = rhythmSources[i];
            source.Stop();
            source.clip = clip;
            source.volume = GetToneVolume(pattern.tones[i]);
            source.PlayScheduled(start + pattern.offsets[i]);
        }

        double end = start + pattern.offsets[pattern.offsets.Length - 1] + 0.25;

        // 这里只等整段结束一次；声音本身已经由 DSP 排程。
        while (AudioSettings.dspTime < end)
            yield return null;
    }

    /// <summary>
    /// 根据刚才的鼓点生成同节奏缩圈。
    /// </summary>
    private IEnumerator PlayResponse(RhythmPattern pattern)
    {
        phase = GamePhase.Response;
        statusLine = "FIGHT! Repeat the rhythm";
        judgeLine = "";
        RefreshDebugLine();

        ClearAllTargets();

        double responseBase = AudioSettings.dspTime + approachTime + 0.18;

        for (int i = 0; i < pattern.offsets.Length; i++)
        {
            CreateHitTarget(responseBase + pattern.offsets[i]);
        }

        while (phase == GamePhase.Response && energy < 100f)
        {
            bool allResolved = true;

            for (int i = 0; i < targetPool.Length; i++)
            {
                if (targetPool[i].active && !targetPool[i].resolved)
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
            ClearAllTargets();
    }

    // ============================================================
    // 12. 判定圈
    // ============================================================

    /// <summary>
    /// 创建一个判定目标。
    ///
    /// 结构不是“一个圈自己缩小”，而是：
    /// 1) 内层 Hit Circle：固定大小，真正点击它；
    /// 2) 外层 Approach Circle：从约4倍缩到1倍；
    /// 3) 两圈在 targetTime 精确重合；
    /// 4) targetTime 一到，外圈立即消失，内圈仍保留到 Good/Miss 窗口结束。
    /// </summary>
    private void CreateHitTarget(double targetTime)
    {
        HitTarget target = AcquireTarget();

        if (target == null)
            return;

        float x = Random.Range(-720f, 250f);
        float y = Random.Range(-270f, 255f);
        Vector2 pos = new Vector2(x, y);

        // 固定内圈。
        target.hit.rt.anchoredPosition = pos;
        target.hit.rt.sizeDelta = new Vector2(128f, 128f);
        target.hit.rt.localScale = Vector3.one;
        target.hit.image.sprite = hitDiscSprite;
        target.hit.image.color = new Color(0.24f, 0.74f, 0.94f, 0f);

        // 外层缩圈。
        target.approach.rt.anchoredPosition = pos;
        target.approach.rt.sizeDelta = new Vector2(128f, 128f);
        target.approach.rt.localScale = Vector3.one * approachStartScale;
        target.approach.image.sprite = ringSprite;
        target.approach.image.color = new Color(0.82f, 0.97f, 1f, 0f);

        target.targetDsp = targetTime;
        target.spawnDsp = targetTime - approachTime;
        target.spawned = false;
        target.approachHidden = false;
        target.resolved = false;
        target.active = true;

        target.hit.go.SetActive(false);
        target.approach.go.SetActive(false);
    }

    private void UpdateHitTargets()
    {
        if (targetPool == null)
            return;

        double now = AudioSettings.dspTime;

        for (int i = 0; i < targetPool.Length; i++)
        {
            HitTarget t = targetPool[i];

            if (!t.active || t.resolved)
                continue;

            // 到预出现时刻才显示，平时池对象全部休眠。
            if (!t.spawned && now >= t.spawnDsp)
            {
                t.spawned = true;
                t.hit.go.SetActive(true);
                t.approach.go.SetActive(true);
            }

            if (!t.spawned)
                continue;

            // 0 = 刚出现，1 = 正确点击时刻。
            float progress = Mathf.Clamp01(
                (float)((now - t.spawnDsp) / (t.targetDsp - t.spawnDsp))
            );

            // 读谱：外圈线性 4x -> 1x。
            float approachScale = Mathf.Lerp(
                approachStartScale,
                1f,
                progress
            );

            t.approach.rt.localScale =
                Vector3.one * approachScale;

            // 淡入只影响透明度，不改变缩圈时间。
            float visibleSeconds = (float)(now - t.spawnDsp);
            float fade = Mathf.Clamp01(
                visibleSeconds / Mathf.Max(0.01f, approachFadeInTime)
            );

            Color hitColor = t.hit.image.color;
            hitColor.a = Mathf.Lerp(0f, 0.80f, fade);
            t.hit.image.color = hitColor;

            if (!t.approachHidden)
            {
                Color approachColor = t.approach.image.color;
                approachColor.a = approachMaxAlpha * fade;
                t.approach.image.color = approachColor;
            }

            // 关键观感优化：
            // 外圈在“正确时刻”到达内圈并消失，而不是停在那里继续提示。
            if (!t.approachHidden && now >= t.targetDsp)
            {
                t.approachHidden = true;
                t.approach.go.SetActive(false);
            }

            // 内圈继续保留到 Good 窗口结束，超时才 Miss。
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

    private void TryHitTarget()
    {
        HitTarget best = null;
        double bestAbsDelta = 999.0;
        double now = AudioSettings.dspTime;

        for (int i = 0; i < targetPool.Length; i++)
        {
            HitTarget t = targetPool[i];

            if (!t.active || t.resolved || !t.spawned)
                continue;

            if (!RectTransformUtility.RectangleContainsScreenPoint(
                    t.hit.rt,
                    Input.mousePosition,
                    null))
            {
                continue;
            }

            double absDelta = System.Math.Abs(now - t.targetDsp);

            if (absDelta < bestAbsDelta)
            {
                bestAbsDelta = absDelta;
                best = t;
            }
        }

        if (best == null)
            return;

        // 太早点击不惩罚玩家，只提示 WAIT。
        if (now < best.targetDsp - goodWindow)
        {
            judgeLine = "WAIT...";
            RefreshDebugLine();
            return;
        }

        double delta = System.Math.Abs(now - best.targetDsp);

        if (delta <= perfectWindow)
            ResolveTarget(best, JudgeResult.Perfect);
        else if (delta <= goodWindow)
            ResolveTarget(best, JudgeResult.Good);
    }

    private void ResolveTarget(HitTarget target, JudgeResult result)
    {
        if (target == null || !target.active || target.resolved)
            return;

        target.resolved = true;

        Vector2 fxPos = target.hit.rt.anchoredPosition;

        if (result == JudgeResult.Perfect)
        {
            combo++;
            judgeLine = "PERFECT!";
            energy = Mathf.Min(100f, energy + perfectEnergy);

            PlaySfx(perfectClip, 1f);
            PlaySfx(energyGainClip, 0.45f);

            SpawnFlash(fxPos, true);
            SpawnBubbleBurst(fxPos, 15);

            SetGoblinSprite(
                goblinPullSprite != null ? goblinPullSprite : goblinIdleSprite
            );
        }
        else if (result == JudgeResult.Good)
        {
            combo++;
            judgeLine = "GOOD";
            energy = Mathf.Min(100f, energy + goodEnergy);

            PlaySfx(goodClip, 0.9f);
            PlaySfx(energyGainClip, 0.35f);

            SpawnFlash(fxPos, false);
            SpawnBubbleBurst(fxPos, 9);

            SetGoblinSprite(
                goblinPullSprite != null ? goblinPullSprite : goblinIdleSprite
            );
        }
        else
        {
            combo = 0;
            judgeLine = "MISS";
            PlaySfx(missClip, 0.85f);
        }

        RefreshDebugLine();
        UpdateEnergyBar();

        if (energy >= 100f && !playedEnergyFull)
        {
            playedEnergyFull = true;
            PlaySfx(energyFullClip, 1f);
            judgeLine = "ENERGY FULL!";
            RefreshDebugLine();
        }

        // 【性能】不是 Destroy，而是归还对象池。
        ReleaseTarget(target);
    }

    // ============================================================
    // 13. Fever：鱼群自动糊脸
    // ============================================================

    private IEnumerator PlayFever()
    {
        energy = 100f;
        UpdateEnergyBar(true);

        judgeLine = "FISH RUSH!!";
        RefreshDebugLine();

        Vector2 facePos = GetGoblinFacePosition();

        for (int i = 0; i < feverFishCount; i++)
        {
            float p = feverFishCount <= 1
                ? 1f
                : (float)i / (feverFishCount - 1);

            float interval = Mathf.Lerp(
                feverFirstInterval,
                feverLastInterval,
                p
            );

            Vector2 start = new Vector2(
                Random.Range(-820f, 280f),
                Random.Range(-500f, -330f)
            );

            SpawnFeverFish(
                GetRandomFeverFishSprite(),
                start,
                facePos + Random.insideUnitCircle * 46f,
                Random.Range(80f, 145f),
                Random.Range(0.36f, 0.52f)
            );

            // 【性能】动态间隔不 new WaitForSeconds，直接在当前协程里等待。
            float waitUntil = Time.time + interval;
            while (Time.time < waitUntil)
                yield return null;
        }

        statusLine = "...";
        judgeLine = "";
        RefreshDebugLine();

        yield return WAIT_BEFORE_BIG_FISH;

        statusLine = "FINAL FISH!";
        yield return StartCoroutine(HugeFishFinisher(facePos));
    }

    /// <summary>
    /// 最后一条超大鱼。
    /// 这个对象也是预创建并复用，不在 Fever 时临时 New。
    /// </summary>
    private IEnumerator HugeFishFinisher(Vector2 facePos)
    {
        PooledImage fish = hugeFishItem;

        fish.inUse = true;
        fish.go.SetActive(true);
        fish.image.sprite = hugeFishSprite != null ? hugeFishSprite : whiteSprite;
        fish.image.color = hugeFishSprite != null
            ? Color.white
            : new Color(0.98f, 0.68f, 0.18f, 1f);
        fish.image.preserveAspect = true;

        fish.rt.sizeDelta = new Vector2(480f, 310f);

        Vector2 start = new Vector2(-520f, -520f);
        Vector2 control = new Vector2(80f, 430f);
        Vector2 end = facePos;

        float duration = 0.70f;
        float elapsed = 0f;

        fish.rt.anchoredPosition = start;
        fish.rt.localScale = Vector3.one * 0.45f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            Vector2 a = Vector2.Lerp(start, control, t);
            Vector2 b = Vector2.Lerp(control, end, t);

            fish.rt.anchoredPosition = Vector2.Lerp(a, b, t);
            fish.rt.localScale = Vector3.one * Mathf.Lerp(0.45f, 1.35f, t);
            fish.rt.localRotation = Quaternion.Euler(
                0f,
                0f,
                Mathf.Lerp(-22f, 16f, t)
            );

            yield return null;
        }

        PlaySfx(bigSplashClip, 1f);
        PlaySfx(fishFaceHitClip, 1f);

        SpawnBubbleBurst(facePos, 30);
        SpawnFlash(facePos, true);

        yield return StartCoroutine(ScreenShake(0.30f, 20f));

        judgeLine = "BOOM!!";
        RefreshDebugLine();

        yield return WAIT_AFTER_BIG_FISH;

        ReleasePooledImage(fish);
    }

    // ============================================================
    // 14. Fever 普通鱼：统一 Update，不再每条鱼一个协程
    // ============================================================

    private void SpawnFeverFish(
        Sprite sprite,
        Vector2 start,
        Vector2 end,
        float size,
        float duration)
    {
        int slot = FindFreeFishSlot();

        // 池满时跳过一条鱼，不临时扩容，避免突然产生 GC/内存尖峰。
        if (slot < 0)
            return;

        PooledImage item = fishPool[slot];

        item.inUse = true;
        item.go.SetActive(true);
        item.image.sprite = sprite != null ? sprite : whiteSprite;
        item.image.color = sprite != null ? Color.white : RandomFishColor();
        item.image.preserveAspect = true;
        item.rt.sizeDelta = new Vector2(size * 1.45f, size);
        item.rt.anchoredPosition = start;
        item.rt.localScale = Vector3.one;

        Vector2 control = Vector2.Lerp(start, end, 0.5f);
        control.y += Random.Range(230f, 390f);

        FishFx state = new FishFx
        {
            active = true,
            impacted = false,
            item = item,
            start = start,
            control = control,
            end = end,
            elapsed = 0f,
            duration = duration,
            hold = 0.08f,
            startAngle = -25f,
            endAngle = 25f
        };

        fishFx[slot] = state;
    }

    private void UpdateFishEffects()
    {
        if (fishFx == null)
            return;

        float dt = Time.deltaTime;

        for (int i = 0; i < fishFx.Length; i++)
        {
            FishFx fx = fishFx[i];

            if (!fx.active)
                continue;

            fx.elapsed += dt;

            if (fx.elapsed <= fx.duration)
            {
                float t = Mathf.Clamp01(fx.elapsed / fx.duration);

                Vector2 a = Vector2.Lerp(fx.start, fx.control, t);
                Vector2 b = Vector2.Lerp(fx.control, fx.end, t);

                fx.item.rt.anchoredPosition = Vector2.Lerp(a, b, t);
                fx.item.rt.localRotation = Quaternion.Euler(
                    0f,
                    0f,
                    Mathf.Lerp(fx.startAngle, fx.endAngle, t)
                );
            }
            else
            {
                if (!fx.impacted)
                {
                    fx.impacted = true;
                    PlaySfx(fishFaceHitClip, 0.72f);

                    // 4 个就足够形成连续 Fever 水花，同时控制同屏对象量。
                    SpawnBubbleBurst(fx.end, 4);
                }

                float holdT = (fx.elapsed - fx.duration) / fx.hold;

                if (holdT < 1f)
                {
                    float s = 1f + Mathf.Sin(holdT * Mathf.PI) * 0.20f;
                    fx.item.rt.localScale = Vector3.one * s;
                }
                else
                {
                    ReleasePooledImage(fx.item);
                    fx.active = false;
                }
            }

            fishFx[i] = fx;
        }
    }

    // ============================================================
    // 15. 泡泡特效：单 Graphic 批量绘制
    // ============================================================

    private void SpawnBubbleBurst(Vector2 center, int count)
    {
        if (bubbleBatch == null)
            return;

        bubbleBatch.Emit(center, count);
    }

    // ============================================================
    // 16. 命中闪光：固定池 + Update
    // ============================================================

    private void SpawnFlash(Vector2 pos, bool perfect)
    {
        int slot = FindFreeFlashSlot();

        if (slot < 0)
            return;

        PooledImage item = flashPool[slot];
        float size = perfect ? 170f : 125f;

        item.inUse = true;
        item.go.SetActive(true);
        item.image.sprite = bubbleSprite;
        item.image.color = perfect
            ? new Color(1f, 0.86f, 0.28f, 0.95f)
            : new Color(0.62f, 0.94f, 1f, 0.90f);

        item.rt.sizeDelta = new Vector2(size, size);
        item.rt.anchoredPosition = pos;
        item.rt.localScale = Vector3.one * 0.45f;

        flashFx[slot] = new FlashFx
        {
            active = true,
            item = item,
            elapsed = 0f,
            life = perfect ? 0.20f : 0.15f,
            startScale = 0.45f,
            endScale = 1.55f
        };
    }

    private void UpdateFlashEffects()
    {
        if (flashFx == null)
            return;

        float dt = Time.deltaTime;

        for (int i = 0; i < flashFx.Length; i++)
        {
            FlashFx fx = flashFx[i];

            if (!fx.active)
                continue;

            fx.elapsed += dt;

            if (fx.elapsed >= fx.life)
            {
                ReleasePooledImage(fx.item);
                fx.active = false;
                flashFx[i] = fx;
                continue;
            }

            float t = fx.elapsed / fx.life;

            fx.item.rt.localScale = Vector3.one *
                Mathf.Lerp(fx.startScale, fx.endScale, t);

            Color c = fx.item.image.color;
            c.a = 1f - t;
            fx.item.image.color = c;

            flashFx[i] = fx;
        }
    }

    // ============================================================
    // 17. 屏幕震动
    // ============================================================

    private IEnumerator ScreenShake(float duration, float strength)
    {
        Vector2 original = stageRT.anchoredPosition;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            stageRT.anchoredPosition =
                original + Random.insideUnitCircle * strength;
            yield return null;
        }

        stageRT.anchoredPosition = original;
    }

    // ============================================================
    // 18. UI 创建（只在 Awake 执行一次）
    // ============================================================

    private void BuildRuntimeUI()
    {
        GameObject canvasGO = new GameObject(
            "FishingHeaven_RuntimeCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster)
        );

        canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(REF_W, REF_H);
        scaler.screenMatchMode =
            CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasRT = canvasGO.GetComponent<RectTransform>();

        GameObject stageGO = new GameObject(
            "Stage",
            typeof(RectTransform)
        );

        stageGO.transform.SetParent(canvasGO.transform, false);
        stageRT = stageGO.GetComponent<RectTransform>();
        StretchFull(stageRT);

        if (pondBackground != null)
        {
            GameObject bg = CreateImageObject(
                "PondBackground",
                stageRT,
                pondBackground,
                Color.white,
                Vector2.zero
            );

            RectTransform bgRT = bg.GetComponent<RectTransform>();
            StretchFull(bgRT);
            bg.GetComponent<Image>().preserveAspect = false;
        }
        else
        {
            GameObject sky = CreateImageObject(
                "Sky",
                stageRT,
                whiteSprite,
                new Color(0.60f, 0.82f, 0.92f, 1f),
                new Vector2(REF_W, 520f)
            );

            RectTransform skyRT = sky.GetComponent<RectTransform>();
            skyRT.anchorMin = new Vector2(0.5f, 1f);
            skyRT.anchorMax = new Vector2(0.5f, 1f);
            skyRT.pivot = new Vector2(0.5f, 1f);
            skyRT.anchoredPosition = Vector2.zero;

            GameObject water = CreateImageObject(
                "Water",
                stageRT,
                whiteSprite,
                new Color(0.20f, 0.58f, 0.70f, 1f),
                new Vector2(REF_W, 600f)
            );

            RectTransform waterRT = water.GetComponent<RectTransform>();
            waterRT.anchorMin = new Vector2(0.5f, 0f);
            waterRT.anchorMax = new Vector2(0.5f, 0f);
            waterRT.pivot = new Vector2(0.5f, 0f);
            waterRT.anchoredPosition = Vector2.zero;
        }

        GameObject goblinGO = CreateImageObject(
            "Goblin",
            stageRT,
            goblinIdleSprite != null ? goblinIdleSprite : whiteSprite,
            goblinIdleSprite != null
                ? Color.white
                : new Color(0.34f, 0.66f, 0.23f, 1f),
            new Vector2(430f, 570f)
        );

        goblinImage = goblinGO.GetComponent<Image>();
        goblinImage.preserveAspect = true;

        goblinRT = goblinGO.GetComponent<RectTransform>();
        goblinRT.anchoredPosition = new Vector2(640f, -90f);

        GameObject energyFrame = CreateImageObject(
            "EnergyFrame",
            canvasRT,
            whiteSprite,
            new Color(0.08f, 0.10f, 0.12f, 0.94f),
            new Vector2(650f, 46f)
        );

        RectTransform frameRT = energyFrame.GetComponent<RectTransform>();
        frameRT.anchorMin = new Vector2(0.5f, 1f);
        frameRT.anchorMax = new Vector2(0.5f, 1f);
        frameRT.pivot = new Vector2(0.5f, 1f);
        frameRT.anchoredPosition = new Vector2(0f, -45f);

        GameObject fill = CreateImageObject(
            "EnergyFill",
            frameRT,
            whiteSprite,
            new Color(0.95f, 0.64f, 0.18f, 1f),
            new Vector2(0f, 30f)
        );

        energyFillImage = fill.GetComponent<Image>();
        energyFillRT = fill.GetComponent<RectTransform>();

        energyFillRT.anchorMin = new Vector2(0f, 0.5f);
        energyFillRT.anchorMax = new Vector2(0f, 0.5f);
        energyFillRT.pivot = new Vector2(0f, 0.5f);
        energyFillRT.anchoredPosition = new Vector2(10f, 0f);
    }

    // ============================================================
    // 19. 对象池初始化
    // ============================================================

    private void PrewarmPools()
    {
        // 判定目标：每个槽位拥有“一张固定内圈 + 一张Approach外圈”。
        // 当前节奏最多4拍，8组非常宽裕。
        targetPool = new HitTarget[TARGET_POOL_SIZE];

        for (int i = 0; i < TARGET_POOL_SIZE; i++)
        {
            PooledImage hit = CreatePooledImage(
                "HitCirclePool_" + i,
                stageRT,
                hitDiscSprite,
                new Vector2(128f, 128f)
            );

            PooledImage approach = CreatePooledImage(
                "ApproachCirclePool_" + i,
                stageRT,
                ringSprite,
                new Vector2(128f, 128f)
            );

            targetPool[i] = new HitTarget
            {
                hit = hit,
                approach = approach,
                active = false,
                spawned = false,
                approachHidden = false,
                resolved = true
            };
        }

        // 【性能核心】
        // 只创建一个 Graphic，一张Mesh最多画 bubblePoolSize 个泡泡。
        GameObject bubbleGO = new GameObject(
            "BubbleBatch",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(FishingBubbleBatchGraphic)
        );

        bubbleGO.transform.SetParent(stageRT, false);

        RectTransform bubbleRT = bubbleGO.GetComponent<RectTransform>();
        StretchFull(bubbleRT);

        bubbleBatch = bubbleGO.GetComponent<FishingBubbleBatchGraphic>();
        bubbleBatch.raycastTarget = false;
        bubbleBatch.Initialize(bubbleSprite, bubblePoolSize);

        // Fever 鱼依旧使用对象池，因为每条鱼需要不同 Sprite 和单独抛物线。
        fishPool = new PooledImage[fishPoolSize];
        fishFx = new FishFx[fishPoolSize];

        for (int i = 0; i < fishPoolSize; i++)
        {
            fishPool[i] = CreatePooledImage(
                "FeverFishPool_" + i,
                stageRT,
                whiteSprite,
                new Vector2(150f, 100f)
            );

            fishPool[i].image.preserveAspect = true;
        }

        // 命中闪光池。
        flashPool = new PooledImage[FLASH_POOL_SIZE];
        flashFx = new FlashFx[FLASH_POOL_SIZE];

        for (int i = 0; i < FLASH_POOL_SIZE; i++)
        {
            flashPool[i] = CreatePooledImage(
                "FlashPool_" + i,
                stageRT,
                bubbleSprite,
                new Vector2(150f, 150f)
            );
        }

        // 最终超大鱼只保留一个。
        hugeFishItem = CreatePooledImage(
            "HugeFishPool",
            stageRT,
            hugeFishSprite != null ? hugeFishSprite : whiteSprite,
            new Vector2(480f, 310f)
        );

        hugeFishItem.image.preserveAspect = true;
    }

    private PooledImage CreatePooledImage(
        string objectName,
        Transform parent,
        Sprite sprite,
        Vector2 size)
    {
        GameObject go = CreateImageObject(
            objectName,
            parent,
            sprite,
            Color.white,
            size
        );

        PooledImage item = new PooledImage
        {
            go = go,
            rt = go.GetComponent<RectTransform>(),
            image = go.GetComponent<Image>(),
            inUse = false
        };

        go.SetActive(false);
        return item;
    }

    private void ReleasePooledImage(PooledImage item)
    {
        if (item == null)
            return;

        item.inUse = false;
        item.rt.localScale = Vector3.one;
        item.rt.localRotation = Quaternion.identity;
        item.go.SetActive(false);
    }

    // ============================================================
    // 20. 对象池查询
    // ============================================================

    private HitTarget AcquireTarget()
    {
        for (int i = 0; i < targetPool.Length; i++)
        {
            if (!targetPool[i].active)
            {
                HitTarget target = targetPool[i];

                target.active = true;
                target.resolved = false;
                target.spawned = false;
                target.approachHidden = false;

                target.hit.inUse = true;
                target.approach.inUse = true;

                return target;
            }
        }

        return null;
    }

    private void ReleaseTarget(HitTarget target)
    {
        if (target == null)
            return;

        target.active = false;
        target.spawned = false;
        target.approachHidden = false;
        target.resolved = true;

        ReleasePooledImage(target.hit);
        ReleasePooledImage(target.approach);
    }

    private int FindFreeFishSlot()
    {
        for (int i = 0; i < fishFx.Length; i++)
        {
            if (!fishFx[i].active)
                return i;
        }

        return -1;
    }

    private int FindFreeFlashSlot()
    {
        for (int i = 0; i < flashFx.Length; i++)
        {
            if (!flashFx[i].active)
                return i;
        }

        return -1;
    }

    private void ClearAllTargets()
    {
        if (targetPool == null)
            return;

        for (int i = 0; i < targetPool.Length; i++)
        {
            if (targetPool[i].active)
                ReleaseTarget(targetPool[i]);
        }
    }

    // ============================================================
    // 21. 能量 / 角色
    // ============================================================

    private void UpdateEnergyBar(bool feverGlow = false)
    {
        if (energyFillRT == null)
            return;

        float width = 630f * Mathf.Clamp01(energy / 100f);
        energyFillRT.sizeDelta = new Vector2(width, 30f);

        if (energyFillImage == null)
            return;

        if (feverGlow || energy >= 100f)
        {
            energyFillImage.color = new Color(1f, 0.28f, 0.22f, 1f);
        }
        else if (energy >= 80f)
        {
            energyFillImage.color = new Color(1f, 0.47f, 0.12f, 1f);
        }
        else
        {
            energyFillImage.color = new Color(0.95f, 0.70f, 0.20f, 1f);
        }
    }

    private void SetGoblinSprite(Sprite sprite)
    {
        if (goblinImage == null || sprite == null)
            return;

        goblinImage.sprite = sprite;
        goblinImage.color = Color.white;
    }

    private Vector2 GetGoblinFacePosition()
    {
        if (goblinRT == null)
            return new Vector2(640f, 120f);

        return goblinRT.anchoredPosition + new Vector2(5f, 165f);
    }

    // ============================================================
    // 22. 音效
    // ============================================================

    private void PlayTone(int tone)
    {
        PlaySfx(GetToneClip(tone), GetToneVolume(tone));
    }

    private AudioClip GetToneClip(int tone)
    {
        if (tone == 1)
            return drumHigh;

        if (tone == 2)
            return drumAccent;

        return drumLow;
    }

    private float GetToneVolume(int tone)
    {
        return tone == 2 ? 1f : 0.95f;
    }

    private void PlaySfx(AudioClip clip, float volume)
    {
        if (clip == null || audioSource == null)
            return;

        audioSource.PlayOneShot(clip, volume);
    }

    // ============================================================
    // 23. 节奏数据
    // ============================================================

    private void BuildPatterns()
    {
        patterns.Clear();

        // 第一版故意慢，让玩家容易进入 Fever。
        patterns.Add(new RhythmPattern(
            "STEADY",
            new float[] { 0f, 0.82f, 1.64f },
            new int[]   { 0, 0, 1 }
        ));

        patterns.Add(new RhythmPattern(
            "DOUBLE",
            new float[] { 0f, 0.82f, 1.28f, 2.10f },
            new int[]   { 0, 0, 1, 2 }
        ));

        patterns.Add(new RhythmPattern(
            "PAUSE",
            new float[] { 0f, 0.64f, 1.52f, 2.34f },
            new int[]   { 0, 1, 0, 2 }
        ));

        patterns.Add(new RhythmPattern(
            "BOUNCE",
            new float[] { 0f, 0.72f, 1.42f, 1.82f },
            new int[]   { 0, 1, 0, 1 }
        ));
    }

    // ============================================================
    // 24. 资源 / 工具函数
    // ============================================================

    private Sprite GetRandomFeverFishSprite()
    {
        if (feverFishSprites == null || feverFishSprites.Length == 0)
            return null;

        int tries = feverFishSprites.Length;

        while (tries-- > 0)
        {
            Sprite sprite = feverFishSprites[
                Random.Range(0, feverFishSprites.Length)
            ];

            if (sprite != null)
                return sprite;
        }

        return null;
    }

    private Color RandomFishColor()
    {
        // 【性能】这里只索引静态数组，不再产生新数组。
        return FallbackFishColors[
            Random.Range(0, FallbackFishColors.Length)
        ];
    }

    /// <summary>
    /// 只用于 Awake / 初始化阶段。
    /// 高频特效不要调用这个函数，要走对象池。
    /// </summary>
    private GameObject CreateImageObject(
        string objectName,
        Transform parent,
        Sprite sprite,
        Color color,
        Vector2 size)
    {
        GameObject go = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );

        go.transform.SetParent(parent, false);

        Image img = go.GetComponent<Image>();
        img.sprite = sprite != null ? sprite : whiteSprite;
        img.color = color;
        img.raycastTarget = false;

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;

        return go;
    }

    private void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
    }

    private Sprite CreateSolidSprite(int w, int h, Color color)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color[] pixels = new Color[w * h];

        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = color;

        tex.SetPixels(pixels);
        tex.Apply(false, true);

        return Sprite.Create(
            tex,
            new Rect(0, 0, w, h),
            new Vector2(0.5f, 0.5f),
            100f
        );
    }

    private Sprite CreateCircleSprite(int size)
    {
        Texture2D tex =
            new Texture2D(size, size, TextureFormat.RGBA32, false);

        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color[] pixels = new Color[size * size];

        float center = (size - 1) * 0.5f;
        float radius = size * 0.46f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float d2 = dx * dx + dy * dy;

                pixels[y * size + x] =
                    d2 <= radius * radius
                    ? Color.white
                    : Color.clear;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply(false, true);

        return Sprite.Create(
            tex,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            100f
        );
    }

    private Sprite CreateRingSprite(int size, int thickness)
    {
        Texture2D tex =
            new Texture2D(size, size, TextureFormat.RGBA32, false);

        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color[] pixels = new Color[size * size];

        float center = (size - 1) * 0.5f;
        float outer = size * 0.46f;
        float inner = outer - thickness;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float d = Mathf.Sqrt(dx * dx + dy * dy);

                bool inRing = d <= outer && d >= inner;
                pixels[y * size + x] =
                    inRing ? Color.white : Color.clear;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply(false, true);

        return Sprite.Create(
            tex,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            100f
        );
    }

    private Sprite CreateBubbleSprite(int size)
    {
        Texture2D tex =
            new Texture2D(size, size, TextureFormat.RGBA32, false);

        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color[] pixels = new Color[size * size];

        float center = (size - 1) * 0.5f;
        float radius = size * 0.43f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float d = Mathf.Sqrt(dx * dx + dy * dy);

                if (d <= radius)
                {
                    float edge = Mathf.InverseLerp(
                        radius,
                        radius - 4f,
                        d
                    );

                    float alpha = Mathf.Lerp(0.32f, 0.78f, edge);

                    pixels[y * size + x] =
                        new Color(1f, 1f, 1f, alpha);
                }
                else
                {
                    pixels[y * size + x] = Color.clear;
                }
            }
        }

        tex.SetPixels(pixels);
        tex.Apply(false, true);

        return Sprite.Create(
            tex,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            100f
        );
    }

    private void DestroyGeneratedSprite(Sprite sprite)
    {
        if (sprite == null)
            return;

        Texture2D tex = sprite.texture;

        Destroy(sprite);

        if (tex != null)
            Destroy(tex);
    }
}

/// <summary>
/// 《钓鱼天国》泡泡批量绘制器。
///
/// 这样在 Perfect / Fever 满屏泡泡时：
/// - 不创建 GameObject；
/// - 不 Destroy；
/// - 不为每颗泡泡启动协程；
/// - Canvas 中只有一个泡泡绘制节点。
/// </summary>
public sealed class FishingBubbleBatchGraphic : MaskableGraphic
{
    private struct BubbleParticle
    {
        public bool active;
        public Vector2 position;
        public Vector2 velocity;
        public float size;
        public float age;
        public float life;
    }

    private BubbleParticle[] particles;
    private Sprite bubbleSprite;

    public override Texture mainTexture
    {
        get
        {
            return bubbleSprite != null && bubbleSprite.texture != null
                ? bubbleSprite.texture
                : Texture2D.whiteTexture;
        }
    }

    public void Initialize(Sprite sprite, int capacity)
    {
        bubbleSprite = sprite;
        particles = new BubbleParticle[Mathf.Max(8, capacity)];
        raycastTarget = false;
        SetVerticesDirty();
        SetMaterialDirty();
    }

    public void Emit(Vector2 center, int count)
    {
        if (particles == null)
            return;

        for (int n = 0; n < count; n++)
        {
            int slot = FindFreeSlot();

            // 池满时直接少画几颗，不临时扩容，不制造GC尖峰。
            if (slot < 0)
                break;

            Vector2 dir = Random.insideUnitCircle;

            if (dir.sqrMagnitude < 0.01f)
                dir = Vector2.up;
            else
                dir.Normalize();

            particles[slot] = new BubbleParticle
            {
                active = true,
                position = center,
                velocity = dir * Random.Range(160f, 430f),
                size = Random.Range(18f, 46f),
                age = 0f,
                life = Random.Range(0.35f, 0.62f)
            };
        }

        SetVerticesDirty();
    }

    public void Tick(float dt)
    {
        if (particles == null)
            return;

        bool changed = false;

        for (int i = 0; i < particles.Length; i++)
        {
            BubbleParticle p = particles[i];

            if (!p.active)
                continue;

            changed = true;
            p.age += dt;

            if (p.age >= p.life)
            {
                p.active = false;
                particles[i] = p;
                continue;
            }

            p.velocity += Vector2.down * 110f * dt;
            p.position += p.velocity * dt;

            particles[i] = p;
        }

        if (changed)
            SetVerticesDirty();
    }

    private int FindFreeSlot()
    {
        for (int i = 0; i < particles.Length; i++)
        {
            if (!particles[i].active)
                return i;
        }

        return -1;
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        if (particles == null || bubbleSprite == null)
            return;

        Texture tex = bubbleSprite.texture;

        if (tex == null)
            return;

        Rect tr = bubbleSprite.textureRect;

        float u0 = tr.xMin / tex.width;
        float v0 = tr.yMin / tex.height;
        float u1 = tr.xMax / tex.width;
        float v1 = tr.yMax / tex.height;

        int vertexBase = 0;

        for (int i = 0; i < particles.Length; i++)
        {
            BubbleParticle p = particles[i];

            if (!p.active)
                continue;

            float t = Mathf.Clamp01(p.age / p.life);
            float currentSize = p.size * Mathf.Lerp(0.65f, 1.35f, t);
            float half = currentSize * 0.5f;

            byte alpha = (byte)Mathf.RoundToInt(
                Mathf.Lerp(235f, 0f, t)
            );

            Color32 color = new Color32(194, 242, 255, alpha);

            UIVertex vert = UIVertex.simpleVert;
            vert.color = color;

            vert.position = new Vector3(
                p.position.x - half,
                p.position.y - half,
                0f
            );
            vert.uv0 = new Vector2(u0, v0);
            vh.AddVert(vert);

            vert.position = new Vector3(
                p.position.x - half,
                p.position.y + half,
                0f
            );
            vert.uv0 = new Vector2(u0, v1);
            vh.AddVert(vert);

            vert.position = new Vector3(
                p.position.x + half,
                p.position.y + half,
                0f
            );
            vert.uv0 = new Vector2(u1, v1);
            vh.AddVert(vert);

            vert.position = new Vector3(
                p.position.x + half,
                p.position.y - half,
                0f
            );
            vert.uv0 = new Vector2(u1, v0);
            vh.AddVert(vert);

            vh.AddTriangle(vertexBase, vertexBase + 1, vertexBase + 2);
            vh.AddTriangle(vertexBase, vertexBase + 2, vertexBase + 3);

            vertexBase += 4;
        }
    }
}

