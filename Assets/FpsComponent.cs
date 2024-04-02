using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Jobs.LowLevel.Unsafe;
using UnityEngine;

public class FpsComponent : MonoBehaviour
{
    private float DeltaTime;
    public TextMeshProUGUI text;
    public TextMeshProUGUI workerCountText;
    // Start is called before the first frame update
    void Start()
    {
#if !UNITY_EDITOR
        Application.targetFrameRate = 60;
#endif
    }

    // Update is called once per frame
    void Update()
    {
        
        DeltaTime += (Time.unscaledDeltaTime - DeltaTime) * 0.1f;
        float fps = 1.0f / DeltaTime;
        
        workerCountText.text = $"JobWorkerCount:{JobsUtility.JobWorkerCount} ,fps:{Mathf.Round(fps)}";
        
        
    }
}
