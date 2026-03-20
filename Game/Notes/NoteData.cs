using System;
using UnityEngine;
using System.Collections.Generic;

// 核心音符数据模型（挂载到音符GameObject的核心组件）
[Serializable]
public class NoteData : MonoBehaviour
{
    // 基础标识
    public int NoteIndex;          // 音符全局序号
    public int KeyIndex;           // 绑定的按键序号（判定用）
    // 位置信息（指令类直接操作）
    public float x;               // 动态X坐标
    public float y;               // 动态Y坐标
    // 显示控制
    public bool isVisible = true; // 是否显示音符
    // 指令集合（存储当前音符绑定的所有指令）
    public List<Command> commands;// 指令列表（需确保Command类已定义）
}