using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 音符皮肤数据类（ScriptableObject）
/// </summary>
[CreateAssetMenu(fileName = "NewNoteSkin", menuName = "Note Skin", order = 1)]
public class NoteSkin : ScriptableObject
{
    [Header("皮肤基本信息")]
    public string skinName = "Default";
    
    [Header("颜色设置")]
    public Color tapColor = Color.white;
    public Color holdColor = Color.cyan;
    public Color dragColor = Color.magenta;
    public Color flickColor = Color.yellow;
    public Color mtapColor = Color.green;
}
