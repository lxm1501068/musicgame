using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneSwitcher : MonoBehaviour
{
    // 在按钮点击事件中调用此方法
    public void SwitchToSampleScene()
    {
        SceneManager.LoadScene("SampleScene");
    }
}