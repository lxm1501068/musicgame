using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(SpriteRenderer))]
public class Dtap : MonoBehaviour
{
    [Header("核心关联")]
    public NoteData noteData;

    [Header("Tap精灵配置（Tap1/Tap2共用）")]
    public Sprite tapDefaultSprite;
    public Sprite tapPerfectSprite;
    public Sprite tapGoodSprite;
    public Sprite tapBadSprite;
    public Sprite tapMissSprite;

    [Header("位置偏移")]
    public Vector2 tap1Offset = new Vector2(-0.5f, 0);
    public Vector2 tap2Offset = new Vector2(0.5f, 0);

    [Header("判定规则")]
    public float secondJudgeWindow = 0.3f;
    public float destroyDelay = 0.2f;

    private DropToCommand dropToCommand;
    private List<ShiftCommand> shiftCommands = new List<ShiftCommand>();
    private List<MoveCommand> moveCommands = new List<MoveCommand>();

    private GameObject tap1Obj;
    private GameObject tap2Obj;
    private SpriteRenderer tap1Renderer;
    private SpriteRenderer tap2Renderer;

    private bool hasCompletedJudge = false;
    private bool isTap1Judged = false;
    private bool isTap2Judged = false;
    private float tap1JudgeTime = 0f;
    private JudgeResult tap1Result = JudgeResult.None;
    private JudgeResult tap2Result = JudgeResult.None;

    private bool isCommandsInitialized = false;
    private Coroutine destroyCoroutine;

    private float firstCommandStartTime;

    void Awake()
    {        if (noteData == null)
        {
            Debug.LogError($"[{gameObject.name}] Dtap组件：NoteData未赋值！");
            enabled = false;
            return;
        }

        CreateTapNoteObjects();
        ResetTapSprites();

        // 初始隐藏两个子Tap
        if (tap1Renderer != null) tap1Renderer.enabled = false;
        if (tap2Renderer != null) tap2Renderer.enabled = false;
    }

    void Update()
    {
        if (GameManager.Instance == null) return;
        
        float currentTime = GameManager.Instance.CurrentPlayTime;
        if (currentTime == -1) return;

        if (!isCommandsInitialized)
        {            InitCommands();
            transform.position = new Vector2(noteData.x, noteData.y);
            isCommandsInitialized = true;
        }

        if (currentTime < firstCommandStartTime)
        {            // 未到显示时间，确保两个子Tap隐藏，并跳过后续所有逻辑
            if (tap1Renderer != null) tap1Renderer.enabled = false;
            if (tap2Renderer != null) tap2Renderer.enabled = false;
            return;
        }
        else
        {
            // 到达显示时间，确保子Tap显示
            if (tap1Renderer != null && !tap1Renderer.enabled) tap1Renderer.enabled = true;
            if (tap2Renderer != null && !tap2Renderer.enabled) tap2Renderer.enabled = true;
        }

        if (hasCompletedJudge)
        {
            SyncPosition();
            return;
        }

        ExecuteShiftCommands(currentTime);
        ExecuteMoveCommands(currentTime);

        if (!isTap1Judged)
        {
            ExecuteDropToJudge(currentTime);
            if (dropToCommand != null && dropToCommand.judgeResult != JudgeResult.None)
            {
                tap1Result = dropToCommand.judgeResult;
                isTap1Judged = true;
                tap1JudgeTime = currentTime;
            }
        }
        else if (isTap1Judged && !isTap2Judged)
        {
            CheckTap2Judge(currentTime);
        }

        if (isTap1Judged && isTap2Judged && !hasCompletedJudge)
        {
            SwitchJudgeSprites();
            hasCompletedJudge = true;
            destroyCoroutine = StartCoroutine(DelayDestroyNote());
        }

        SyncPosition();
    }

    #region 指令体系（与之前相同，略作保留，但确保无遗漏）
    private void InitCommands()
    {        if (noteData.commands == null || noteData.commands.Count == 0)
        {
            Debug.Log($"[{noteData.NoteIndex}] Dtap组件：NoteData无关联的Command！");
            return;
        }

        firstCommandStartTime = noteData.commands[0].timeA;

        Command cmd = noteData.commands[0];
        dropToCommand = new DropToCommand(noteData, cmd, cmd.key_name);

        if (cmd.x2 != 0 || cmd.y2 != 0)
        {
            shiftCommands.Add(new ShiftCommand(noteData, cmd));
        }

        if (!string.IsNullOrEmpty(cmd.json_filename))
        {
            moveCommands.Add(new MoveCommand(noteData, cmd));
        }

        if (dropToCommand == null)
        {
            Debug.LogError($"[{gameObject.name}] Dtap组件：未解析到DropTo指令！");
            enabled = false;
        }
    }

    private void ExecuteShiftCommands(float currentTime)
    {
        foreach (var shiftCmd in shiftCommands)
            shiftCmd.UpdateNotePosition(currentTime, Time.deltaTime);
    }

    private void ExecuteMoveCommands(float currentTime)
    {
        foreach (var moveCmd in moveCommands)
            moveCmd.UpdateNotePosition(currentTime);
    }

    private void ExecuteDropToJudge(float currentTime)
    {
        if (dropToCommand != null && noteData.commands.Count > 0)
        {
            Command cmd = noteData.commands[0];
            dropToCommand.Judge(currentTime, cmd.key_name);
        }
    }

    private void SyncPosition()
    {
        if (noteData == null) return;
        transform.position = new Vector2(noteData.x, noteData.y);
        if (tap1Obj != null) tap1Obj.transform.localPosition = tap1Offset;
        if (tap2Obj != null) tap2Obj.transform.localPosition = tap2Offset;
    }
    #endregion

    #region 双Tap视觉与判定
    private void CreateTapNoteObjects()
    {
        tap1Obj = new GameObject($"Dtap_Tap1_Key{noteData.KeyIndex}");
        tap1Obj.transform.SetParent(transform);
        tap1Obj.transform.localPosition = tap1Offset;
        tap1Renderer = tap1Obj.AddComponent<SpriteRenderer>();
        tap1Renderer.sortingOrder = 1;

        tap2Obj = new GameObject($"Dtap_Tap2_Key{noteData.KeyIndex}");
        tap2Obj.transform.SetParent(transform);
        tap2Obj.transform.localPosition = tap2Offset;
        tap2Renderer = tap2Obj.AddComponent<SpriteRenderer>();
        tap2Renderer.sortingOrder = 1;

        ValidateSpriteConfig();
    }

    private void ValidateSpriteConfig()
    {
        if (tapDefaultSprite == null) Debug.LogWarning($"[{gameObject.name}] Tap默认精灵未配置！");
        if (tapPerfectSprite == null) Debug.LogWarning($"[{gameObject.name}] Tap Perfect精灵未配置！");
        if (tapGoodSprite == null) Debug.LogWarning($"[{gameObject.name}] Tap Good精灵未配置！");
        if (tapBadSprite == null) Debug.LogWarning($"[{gameObject.name}] Tap Bad精灵未配置！");
        if (tapMissSprite == null) Debug.LogWarning($"[{gameObject.name}] Tap Miss精灵未配置！");
    }

    private void ResetTapSprites()
    {
        if(tap1Renderer != null) tap1Renderer.sprite = tapDefaultSprite;
        if(tap2Renderer != null) tap2Renderer.sprite = tapDefaultSprite;
    }

    private void CheckTap2Judge(float currentTime)
    {
        float timeSinceTap1 = currentTime - tap1JudgeTime;

        if (timeSinceTap1 > secondJudgeWindow)
        {
            tap2Result = JudgeResult.Miss;
            isTap2Judged = true;
            return;
        }

        if (noteData.commands.Count > 0)
        {
            Command cmd = noteData.commands[0];
            bool isKeyPressed = InputManager.Instance.IsGroupPressed(cmd.key_name);
            if (isKeyPressed)
            {
                tap2Result = JudgeResult.Perfect;
                isTap2Judged = true;
            }
        }
    }

    private void SwitchJudgeSprites()
    {
        Sprite targetSprite = tap1Result switch
        {
            JudgeResult.Perfect => tapPerfectSprite,
            JudgeResult.Good => tapGoodSprite,
            JudgeResult.Bad => tapBadSprite,
            JudgeResult.Miss => tapMissSprite,
            _ => tapDefaultSprite
        };

        if(tap1Renderer != null) tap1Renderer.sprite = targetSprite;
        if(tap2Renderer != null) tap2Renderer.sprite = targetSprite;

        if (tap1Renderer?.sprite == null)
        {
            Debug.LogError($"[{gameObject.name}] Dtap组件：{tap1Result}对应的精灵未赋值！");
            ResetTapSprites();
        }
    }
    #endregion

    private IEnumerator DelayDestroyNote()
    {
        yield return new WaitForSeconds(destroyDelay);
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (destroyCoroutine != null) StopCoroutine(destroyCoroutine);
        StopAllCoroutines();
    }

    public void ResetDtapState()
    {
        if (destroyCoroutine != null)
        {
            StopCoroutine(destroyCoroutine);
            destroyCoroutine = null;
        }
        hasCompletedJudge = false;
        isTap1Judged = false;
        isTap2Judged = false;
        tap1JudgeTime = 0f;
        tap1Result = JudgeResult.None;
        tap2Result = JudgeResult.None;
        isCommandsInitialized = false;

        if (dropToCommand != null)
            dropToCommand.judgeResult = JudgeResult.None;

        ResetTapSprites();
        transform.position = new Vector2(noteData.x, noteData.y);
        gameObject.SetActive(true);
    }
}