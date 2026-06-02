using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 皮肤资源创建助手（仅在编辑器中使用）
/// </summary>
public class SkinAssetCreator
{
#if UNITY_EDITOR
    [MenuItem("Assets/Create/Note Skin")]
    public static void CreateNoteSkin()
    {
        // 确保 Resources/Skins 文件夹存在
        string folderPath = "Assets/Resources/Skins";
        if (!System.IO.Directory.Exists(folderPath))
        {
            System.IO.Directory.CreateDirectory(folderPath);
            AssetDatabase.Refresh();
            Debug.Log($"[SkinAssetCreator] 已创建文件夹: {folderPath}");
        }
        
        // 创建新的 NoteSkin ScriptableObject
        NoteSkin skin = ScriptableObject.CreateInstance<NoteSkin>();
        skin.skinName = "NewSkin";
        
        // 生成唯一文件名
        string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{folderPath}/NewSkin.asset");
        
        // 保存资源
        AssetDatabase.CreateAsset(skin, assetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        // 选中新创建的资源
        Selection.activeObject = skin;
        
        Debug.Log($"[SkinAssetCreator] 已创建皮肤资源: {assetPath}");
    }
    
    [MenuItem("Assets/Create/Note Skin", true)]
    public static bool ValidateCreateNoteSkin()
    {
        // 始终可用
        return true;
    }
#endif
}
