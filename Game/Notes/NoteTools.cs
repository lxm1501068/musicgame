using System;
using UnityEngine;
using System.IO;
using System.Collections.Generic;

[Serializable]
public enum JudgeResult { None, Perfect, Good, Bad, Miss }

[Serializable]
public class MoveFrame
{
    public float time;
    public float x;
    public float y;
}

[Serializable]
public class MoveFrameList
{
    public List<MoveFrame> frames;
}

public class ShiftCommand
{
    public float startTime;
    public float speed;
    public Vector2 direction;
    public float endTime;
    public NoteData note;

    public ShiftCommand(NoteData note, Command cmd)
    {
        this.note = note;
        this.startTime = cmd.timeA;
        this.endTime = cmd.timeB;

        Vector2 startPos = new Vector2(cmd.x1, cmd.y1);
        Vector2 targetPos = new Vector2(cmd.x2, cmd.y2);
        Vector2 moveDistance = targetPos - startPos;
        this.direction = moveDistance.normalized;

        float moveDuration = endTime - startTime;
        if (moveDuration <= 0) moveDuration = 0.01f;
        this.speed = moveDistance.magnitude / moveDuration;
    }

    public void UpdateNotePosition(float currentTime, float deltaTime)
    {
        if (note == null || currentTime < startTime || currentTime > endTime) return;

        Vector2 frameMove = direction * speed * deltaTime;
        note.x += frameMove.x;
        note.y += frameMove.y;

        if (currentTime + deltaTime >= endTime)
        {
            float remainTime = endTime - currentTime;
            note.x += direction.x * speed * remainTime;
            note.y += direction.y * speed * remainTime;
        }
    }
}

public class DropToCommand : ShiftCommand
{
    public float perfectThreshold = 0.1f;
    public float goodThreshold = 0.2f;
    public float badThreshold = 0.3f;
    public int KeyIndex;
    public JudgeResult judgeResult = JudgeResult.None;

    public DropToCommand(NoteData note, Command cmd, int keyIndex) : base(note, cmd)
    {
        this.KeyIndex = keyIndex;
    }

    public void Judge(float currentTime, int keyIndex)
    {
        if (note == null || judgeResult != JudgeResult.None || currentTime < startTime) return;

        float timeDiff = currentTime - endTime;
        bool isKeyPressed = InputManager.Instance != null && InputManager.Instance.IsGroupPressed(keyIndex);

        if (isKeyPressed)
        {
            float absDiff = Mathf.Abs(timeDiff);
            if (absDiff <= perfectThreshold) judgeResult = JudgeResult.Perfect;
            else if (absDiff <= goodThreshold) judgeResult = JudgeResult.Good;
            else if (timeDiff >= -badThreshold && timeDiff < -goodThreshold) judgeResult = JudgeResult.Bad;
        }
        else if (timeDiff > goodThreshold)
        {
            judgeResult = JudgeResult.Miss;
        }
    }
}

public class MoveCommand
{
    private static Dictionary<string, List<MoveFrame>> _jsonCache = new Dictionary<string, List<MoveFrame>>();

    public float startTime;
    public NoteData note;
    private List<MoveFrame> _frames;

    public MoveCommand(NoteData note, Command cmd)
    {
        this.note = note;
        this.startTime = cmd.timeA;
        this._frames = GetOrLoadFrames(cmd.json_filename);
    }

    private List<MoveFrame> GetOrLoadFrames(string jsonPath)
    {
        if (string.IsNullOrEmpty(jsonPath)) return null;
        if (_jsonCache.TryGetValue(jsonPath, out var cachedFrames)) return cachedFrames;

        try
        {
            string fullPath = Path.Combine(Application.streamingAssetsPath, jsonPath);
            if (!File.Exists(fullPath)) return null;

            string jsonContent = File.ReadAllText(fullPath);
            MoveFrameList frameList = JsonUtility.FromJson<MoveFrameList>(jsonContent);
            if (frameList != null && frameList.frames != null)
            {
                frameList.frames.Sort((a, b) => a.time.CompareTo(b.time));
                _jsonCache[jsonPath] = frameList.frames;
                return frameList.frames;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"解析JSON失败 ({jsonPath}): {e.Message}");
        }
        return null;
    }

    public void UpdateNotePosition(float currentTime)
    {
        if (note == null || _frames == null || _frames.Count == 0 || currentTime < startTime) return;

        MoveFrame prevFrame = null;
        MoveFrame nextFrame = null;

        for (int i = 0; i < _frames.Count; i++)
        {
            if (_frames[i].time <= currentTime) prevFrame = _frames[i];
            else { nextFrame = _frames[i]; break; }
        }

        if (prevFrame == null)
        {
            note.x = _frames[0].x;
            note.y = _frames[0].y;
        }
        else if (nextFrame == null)
        {
            note.x = prevFrame.x;
            note.y = prevFrame.y;
        }
        else
        {
            float progress = (currentTime - prevFrame.time) / (nextFrame.time - prevFrame.time);
            note.x = Mathf.Lerp(prevFrame.x, nextFrame.x, progress);
            note.y = Mathf.Lerp(prevFrame.y, nextFrame.y, progress);
        }
    }

    public static void ClearCache() => _jsonCache.Clear();
}

public class NoteTools : MonoBehaviour
{
    public static NoteTools Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else Destroy(gameObject);
    }
}
