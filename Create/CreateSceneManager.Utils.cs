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
    // ---------- 辅助方法 ----------
    private void SpawnExistingObjects()
    {
        // 加载音符
        foreach (var cmd in ChartData.Instance.commands)
        {
            SpawnNoteObject(cmd);
        }
        // 加载按键
        foreach (var keyData in ChartData.Instance.keyDatas)
        {
            SpawnKeyObject(keyData);
        }
    }

    private void DeleteSelected()
    {
        if (selectedObject == null) return;

        if (selectedType == SelectedType.Note && selectedCommand != null)
        {
            ChartData.Instance.commands.Remove(selectedCommand);
            infoText.text = $"已删除音符，编号 {selectedCommand.num}";
        }
        else if (selectedType == SelectedType.Key && selectedKeyData != null)
        {
            // 新增：删除按键时，将所有关联该按键的音符 key_name 重置为 0
            int oldKeyId = selectedKeyData.keyName;
            foreach (var cmd in ChartData.Instance.commands)
            {
                if (cmd.key_name == oldKeyId)
                {
                    cmd.key_name = 0;
                }
            }

            ChartData.Instance.keyDatas.Remove(selectedKeyData);
            infoText.text = $"已删除按键，ID {selectedKeyData.keyName}";
        }

        Destroy(selectedObject);
        Deselect();
    }

    int GenerateNoteNumber()
    {
        if (ChartData.Instance.commands.Count == 0) return 0;
        return ChartData.Instance.commands.Max(c => c.num) + 1;
    }

    int GetNoteTypeIndex(NoteType type)
    {
        switch (type)
        {
            case NoteType.Tap: return 0;
            case NoteType.Hold: return 1;
            case NoteType.MTap: return 2;
            case NoteType.Flick: return 3;
            case NoteType.Key: return 4;
            case NoteType.Drag: return 5;
            default: return 0;
        }
    }
}
