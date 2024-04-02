using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Jobs;
using UnityEngine;

public class DebrisScript : MonoBehaviour
{
    private float m_burstTimer = 0.0f;
    
    public int m_backgroundW = 32;
    public int m_backgroundH = 100;
    
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
        float dt = Time.deltaTime;
        
        m_burstTimer -= dt;
        
        if (Input.GetKeyUp(KeyCode.Alpha1))
        {

            if (BRG_Debris.gDebrisManager != null)
            {
                float rndHue = UnityEngine.Random.Range(0.0f, 1.0f);
                BRG_Debris.gDebrisManager.GenerateBurstOfDebris(transform.position, 1024, rndHue);

            }
        }
    }

    private void LateUpdate()
    {
        BRG_Debris.gDebrisManager.UploadGpuData();
    }
}
