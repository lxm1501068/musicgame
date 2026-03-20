using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro; // 添加TextMeshPro命名空间

public class GroupUI : MonoBehaviour
{
    public Button button;           // 点击选择该组的按钮
    public TextMeshProUGUI groupNameText;       // 显示组名（如“组1”）, 改为TextMeshPro
    public TextMeshProUGUI keysText;            // 显示按键列表, 改为TextMeshPro

    public List<string> keys = new List<string>();  // 当前组的按键
    private int groupIndex;

    public void SetGroupIndex(int index)
    {
        groupIndex = index;
        groupNameText.text = "Group " + (index);
        Debug.Log(groupNameText.text);
    }

    public void SetKeys(List<string> keys)
    {
        this.keys = keys;
        UpdateKeysText();
    }

    public void UpdateKeysText()
    {
        keysText.text = string.Join("  ", keys);
    }

    public void SetHighlight(bool highlight)
    {
        // 简单高亮：改变按钮图片颜色（假设按钮有Image组件）
        Image img = button.targetGraphic as Image;
        if (img != null)
        {
            img.color = highlight ? Color.yellow : Color.white;
        }
    }
}