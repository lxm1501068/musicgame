using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.EventSystems;
using System.Globalization;
using TMPro;
using System;

public partial class CreateSceneManager
{
    // ---------- 放置方法 ----------
    void PlaceNote(Vector3 worldPos)
    {
        // 从小节输入框获取小节序数
        int measureIndex = 0;
        if (measureInputField != null)
        {
            int.TryParse(measureInputField.text, out measureIndex);
        }
        
        // 从节拍滑块获取节拍位置
        float beatPosition = 0f;
        if (beatSlider != null)
        {
            beatPosition = beatSlider.value;
            
            // 自动吸附到最近的 1/4 拍或 1/3 拍
            beatPosition = SnapBeatPosition(beatPosition);
        }
        
        // 计算实际时间
        float chartTime = CalculateTimeFromMeasureAndBeat(measureIndex, beatPosition);

        Command newCmd = new Command
        {
            type = currentNoteType,
            num = GenerateNoteNumber(),
            timeB = chartTime,             // 使用计算后的时间作为判定时间 (timeB)
            timeA = chartTime - 1f,        // 默认开始时间 (timeA) 为判定时间前 1s
            x1 = worldPos.x,
            y1 = worldPos.y,
            x2 = worldPos.x,
            y2 = worldPos.y,
            is_show = true,
            isNoteFirstTimeOccured = true,
            commandName = (currentNoteType == NoteType.Tap || currentNoteType == NoteType.Drag) ? "" : "drop_to",
            hold_duration = (currentNoteType == NoteType.Hold) ? 1f : 0f,
            key_name = 0 // 默认无关联按键
        };

        ChartData.Instance.AddNoteData(newCmd);
        ChartData.Instance.SortCommandsByTime();

        SpawnNoteObject(newCmd);

        infoText.text = $"已放置 {currentNoteType} 音符，编号 {newCmd.num}，时间 {chartTime:F2}s";
    }

    void SpawnNoteObject(Command cmd)
    {
        Vector3 worldPos = new Vector3(cmd.x1, cmd.y1, planeZ);
        GameObject noteObj = Instantiate(notePrefab, worldPos, Quaternion.identity);
        
        // 获取已有的 NoteObject 组件（prefab 上应该已经有）
        NoteObject noteComp = noteObj.GetComponent<NoteObject>();
        if (noteComp == null)
        {
            // 如果 prefab 上没有，才添加
            noteComp = noteObj.AddComponent<NoteObject>();
        }
        noteComp.command = cmd;

        SpriteRenderer sr = noteObj.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            int idx = GetNoteTypeIndex(cmd.type);
            if (idx >= 0 && idx < noteSprites.Length) sr.sprite = noteSprites[idx];
        }
    }

    void PlaceKeyObject(Vector3 worldPos)
    {
        int keyId = 0; // 默认起始 ID
        if (ChartData.Instance.keyDatas.Count > 0)
        {
            keyId = ChartData.Instance.keyDatas.Max(k => k.keyName) + 1;
        }

        if (ChartData.Instance.keyDatas.Any(k => k.keyName == keyId))
        {
            infoText.text = $"Key ID {keyId} 已存在，请勿重复放置";
            return;
        }

        KeyData newKey = new KeyData(keyId, worldPos.x, worldPos.y, 1);
        ChartData.Instance.keyDatas.Add(newKey);

        SpawnKeyObject(newKey);

        infoText.text = $"已放置按键 Key ID {keyId}，位置 ({worldPos.x:F2}, {worldPos.y:F2})";
    }

    void SpawnKeyObject(KeyData keyData)
    {
        Vector3 worldPos = new Vector3(keyData.x, keyData.y, planeZ);
        GameObject keyObj = Instantiate(keyPrefab, worldPos, Quaternion.identity);
        
        // 获取已有的 KeyObject 组件（prefab 上应该已经有）
        KeyObject keyComp = keyObj.GetComponent<KeyObject>();
        if (keyComp == null)
        {
            // 如果 prefab 上没有，才添加
            keyComp = keyObj.AddComponent<KeyObject>();
        }
        keyComp.keyData = keyData;

        SpriteRenderer sr = keyObj.GetComponent<SpriteRenderer>();
        if (sr != null && keyData.keyName >= 0 && keyData.keyName < keySprites.Length)
            sr.sprite = keySprites[keyData.keyName];
    }

    // ---------- 放置模式控制 ----------
    public void EnterPlaceMode()
    {
        if (isPlacing) return;
        isPlacing = true;

        if (followPrefab != null)
        {
            followObject = Instantiate(followPrefab);
            UpdateFollowObjectSprite();
        }
    }

    public void ExitPlaceMode()
    {
        if (!isPlacing) return;
        isPlacing = false;
        if (followObject != null) Destroy(followObject);

        // 新增：同步下拉框状态，重置为 "(empty)"
        if (noteTypeDropdown != null && noteTypeDropdown.value != 0)
        {
            noteTypeDropdown.value = 0;
        }
    }

    void UpdateFollowObjectSprite()
    {
        if (followObject == null) return;
        SpriteRenderer sr = followObject.GetComponent<SpriteRenderer>();
        if (sr == null) return;

        if (currentMode == PlaceMode.Note)
        {
            // 如果按住 Ctrl 且当前类型是 Key，预览显示 KeyObject 精灵，否则预览显示音符精灵
            if (currentNoteType == NoteType.Key && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))
            {
                sr.sprite = keySprites.Length > 0 ? keySprites[0] : null;
            }
            else
            {
                int idx = GetNoteTypeIndex(currentNoteType);
                sr.sprite = (idx >= 0 && idx < noteSprites.Length) ? noteSprites[idx] : null;
            }
        }
    }

    // 可以由外部 UI 按钮调用
    public void SetNoteType(int typeIndex)
    {
        // 直接设置下拉框的值，由 OnDropdownTypeSelected 触发后续逻辑
        if (noteTypeDropdown != null)
        {
            noteTypeDropdown.value = typeIndex + 1;
        }
    }
}
