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
            // 如果该音符有关联的按键，记录一下
            int associatedKeyId = selectedCommand.key_name;
            
            // 从 ChartData 中移除该音符的所有指令
            var cmdsToRemove = ChartData.Instance.commands.Where(c => c.num == selectedCommand.num).ToList();
            foreach (var cmd in cmdsToRemove)
            {
                ChartData.Instance.commands.Remove(cmd);
            }
            
            infoText.text = $"已删除音符 #{selectedCommand.num}";
            
            // 如果没有其他音符使用该按键，可以考虑提示用户
            if (associatedKeyId > 0 && !ChartData.Instance.commands.Any(c => c.key_name == associatedKeyId))
            {
                // Debug.Log($"注意：Key ID {associatedKeyId} 现在没有被任何音符使用");
            }
        }
        else if (selectedType == SelectedType.Key && selectedKeyData != null)
        {
            // 删除按键时，将所有关联该按键的音符 key_name 重置为 0
            int oldKeyId = selectedKeyData.keyName;
            int updatedCount = 0;
            foreach (var cmd in ChartData.Instance.commands)
            {
                if (cmd.key_name == oldKeyId)
                {
                    cmd.key_name = 0;
                    updatedCount++;
                }
            }

            ChartData.Instance.keyDatas.Remove(selectedKeyData);
            
            if (updatedCount > 0)
            {
                infoText.text = $"已删除按键 ID {oldKeyId}，并重置了 {updatedCount} 个关联音符";
            }
            else
            {
                infoText.text = $"已删除按键 ID {oldKeyId}";
            }
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
