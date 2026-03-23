using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;

[RequireComponent(typeof(SpriteRenderer))]
public class Mtap : MonoBehaviour
{
    [Header("核心关联")]
    public NoteData noteData;

    [Header("Tap精灵配置")]
    public Sprite tapDefaultSprite;
    public Sprite tapPerfectSprite;
    public Sprite tapGoodSprite;
    public Sprite tapBadSprite;
    public Sprite tapMissSprite;

    [Header("判定规则")]
    public float maxTapInterval = 0.2f;
    public float destroyDelay = 0.2f;

    [Header("UI引用")]
    public TMP_Text countText;

    private DropToCommand dropToCommand;
    private List<ShiftCommand> shiftCommands = new List<ShiftCommand>();
    private List<MoveCommand> moveCommands = new List<MoveCommand>();

    private SpriteRenderer spriteRenderer;
    private int totalTaps;
    private int remainingTaps;
    private bool hasCompletedJudge = false;
    private bool isFirstTapJudged = false;
    private float lastTapTime = 0f;
    private JudgeResult finalResult = JudgeResult.None;
    private bool isMissed = false;

    private bool isCommandsInitialized = false;
    private Coroutine destroyCoroutine;
    private float firstCommandStartTime;

    void Awake()
    {
        // 尝试自动获取同级 NoteData 组件
        if (noteData == null)
        {
            noteData = GetComponent<NoteData>();
        }

        if (noteData == null)
        {
            Debug.LogError($"[{gameObject.name}] Mtap组件：NoteData未赋值且无法自动获取！");
            enabled = false;
            return;
        }

        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        }
        
        // 尝试自动获取或创建文本组件
        InitCountText();

        ResetMtapSprites();
    }

    private void InitCountText()
    {
        if (countText != null) return;

        // 1. 先尝试在子物体中寻找
        countText = GetComponentInChildren<TMP_Text>();

        // 2. 如果没找到，则自动创建一个
        if (countText == null)
        {
            GameObject textObj = new GameObject("Mtap_CountText");
            textObj.transform.SetParent(transform);
            textObj.transform.localPosition = new Vector3(0, 0, -0.1f); // 稍微偏前防止遮挡
            
            TextMeshPro tmp = textObj.AddComponent<TextMeshPro>();
            tmp.fontSize = 6;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;

            // 核心修复：设置渲染层级，确保在音符图片之上
            var meshRenderer = textObj.GetComponent<MeshRenderer>();
            if (meshRenderer != null && spriteRenderer != null)
            {
                meshRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
                meshRenderer.sortingOrder = spriteRenderer.sortingOrder + 1;
            }

            countText = tmp;
        }
    }

    void Update()
    {
        if (GameManager.Instance == null) return;
        
        float currentTime = GameManager.Instance.CurrentPlayTime;
        if (currentTime == -1) return;

        if (!isCommandsInitialized)
        {
            InitCommands();
            transform.position = new Vector2(noteData.x, noteData.y);
            isCommandsInitialized = true;
            
            // 初始化点击次数
            totalTaps = noteData.commands.Count > 0 && noteData.commands[0].hold_duration > 0 
                ? (int)noteData.commands[0].hold_duration 
                : 2; // 默认2次（兼容原Dtap）
            remainingTaps = totalTaps;
            UpdateCountText();
        }

        if (currentTime < firstCommandStartTime)
        {
            if (spriteRenderer != null) spriteRenderer.enabled = false;
            if (countText != null) countText.enabled = false;
            return;
        }
        else
        {
            if (spriteRenderer != null && !spriteRenderer.enabled) spriteRenderer.enabled = true;
            if (countText != null && !countText.enabled) countText.enabled = true;
        }

        if (hasCompletedJudge)
        {
            SyncPosition();
            return;
        }

        ExecuteShiftCommands(currentTime);
        ExecuteMoveCommands(currentTime);

        if (!isFirstTapJudged)
        {
            ExecuteDropToJudge(currentTime);
            if (dropToCommand != null && dropToCommand.judgeResult != JudgeResult.None)
            {
                isFirstTapJudged = true;
                lastTapTime = currentTime;
                finalResult = dropToCommand.judgeResult;
                
                if (finalResult == JudgeResult.Miss)
                {
                    isMissed = true;
                    CompleteJudge();
                }
                else
                {
                    remainingTaps--;
                    UpdateCountText();
                    if (remainingTaps <= 0)
                    {
                        CompleteJudge();
                    }
                }
            }
        }
        else
        {
            // 后续点击判定
            CheckSubsequentTaps(currentTime);
        }

        SyncPosition();
    }

    private void InitCommands()
    {
        if (noteData.commands == null || noteData.commands.Count == 0)
        {
            Debug.Log($"[{noteData.NoteIndex}] Mtap组件：NoteData无关联的Command！");
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
            Debug.LogError($"[{gameObject.name}] Mtap组件：未解析到DropTo指令！");
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

    private void CheckSubsequentTaps(float currentTime)
    {
        if (isMissed) return;

        float interval = currentTime - lastTapTime;

        // 如果超过间隔未点击，判定为 Miss
        if (interval > maxTapInterval)
        {
            isMissed = true;
            finalResult = JudgeResult.Miss;
            CompleteJudge();
            return;
        }

        // 检测按键
        if (noteData.commands.Count > 0)
        {
            Command cmd = noteData.commands[0];
            if (InputManager.Instance.IsGroupPressed(cmd.key_name))
            {
                lastTapTime = currentTime;
                remainingTaps--;
                UpdateCountText();
                
                // 后续点击只要按到就是 Perfect
                if (remainingTaps <= 0)
                {
                    CompleteJudge();
                }
            }
        }
    }

    private void CompleteJudge()
    {
        hasCompletedJudge = true;
        SwitchJudgeSprites();
        UpdateCountText();

        // 更新 UI 显示
        if (JudgeResultDisplay.Instance != null) JudgeResultDisplay.Instance.ShowJudgeResult(finalResult);
        if (ScoreDisplay.Instance != null) ScoreDisplay.Instance.AddScoreByJudge(finalResult);
        
        if (ComboDisplay.Instance != null)
        {
            if (finalResult == JudgeResult.Perfect || finalResult == JudgeResult.Good)
                ComboDisplay.Instance.AddCombo();
            else if (finalResult == JudgeResult.Bad || finalResult == JudgeResult.Miss)
                ComboDisplay.Instance.ResetCombo();
        }

        destroyCoroutine = StartCoroutine(DelayDestroyNote());
    }

    private void UpdateCountText()
    {
        if (countText != null)
        {
            countText.text = remainingTaps > 0 ? remainingTaps.ToString() : "";
        }
    }

    private void SyncPosition()
    {
        if (noteData == null) return;
        transform.position = new Vector2(noteData.x, noteData.y);
    }

    private void ResetMtapSprites()
    {
        if(spriteRenderer != null) spriteRenderer.sprite = tapDefaultSprite;
    }

    private void SwitchJudgeSprites()
    {
        if (spriteRenderer == null) return;

        spriteRenderer.sprite = finalResult switch
        {
            JudgeResult.Perfect => tapPerfectSprite,
            JudgeResult.Good => tapGoodSprite,
            JudgeResult.Bad => tapBadSprite,
            JudgeResult.Miss => tapMissSprite,
            _ => tapDefaultSprite
        };
    }

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

    public void ResetMtapState()
    {
        if (destroyCoroutine != null)
        {
            StopCoroutine(destroyCoroutine);
            destroyCoroutine = null;
        }
        hasCompletedJudge = false;
        isFirstTapJudged = false;
        lastTapTime = 0f;
        isMissed = false;
        finalResult = JudgeResult.None;
        isCommandsInitialized = false;

        if (dropToCommand != null)
            dropToCommand.judgeResult = JudgeResult.None;

        ResetMtapSprites();
        transform.position = new Vector2(noteData.x, noteData.y);
        gameObject.SetActive(true);
    }
}
