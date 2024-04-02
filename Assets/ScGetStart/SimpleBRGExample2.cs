using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

public class SimpleBRGExample2 : MonoBehaviour
{
    public Mesh mesh;
    public Material material;

    public Container Container;


    // 一些辅助常量，用于使计算更方便。
    private const int kSizeOfMatrix = sizeof(float) * 4 * 4;                            //64    matrix4*4
    private const int kSizeOfPackedMatrix = sizeof(float) * 4 * 3;                      //48    压缩到 matrix4*3
    private const int kSizeOfFloat4 = sizeof(float) * 4;                                //16    color
    private const int kBytesPerInstance = (kSizeOfPackedMatrix * 2) + kSizeOfFloat4;    //112   2个4*4矩阵+1条颜色 objectToWorld + worldToObject + color
    private const int kExtraBytes = kSizeOfMatrix * 2;                                  //128
    private const int kNumInstances = 1;


    public float scaleX;
    public float scaleY;
    public float scaleZ;

    
    public float X;
    public float Y;
    public float Z;

    private void Start()
    {
        Container = new Container();

        Container.Init(mesh, material, kNumInstances, kBytesPerInstance);


        Container.UploadGpuData(kNumInstances);

    }

    private void LateUpdate()
    {
        
        int totalGpuBufferSize;
        int alignedWindowSize;
        NativeArray<float4> _sysmemBuffer = Container.GetSysmemBuffer(out totalGpuBufferSize, out alignedWindowSize);

        var _maxInstancePerWindow = alignedWindowSize / kBytesPerInstance;
        var _windowSizeInFloat4 = alignedWindowSize / 16;


        for (int index = 0; index < kNumInstances; index++)
        {
            int i;
            int windowId = System.Math.DivRem(index, _maxInstancePerWindow, out i);
            
            int windowOffsetInFloat4 = windowId * _windowSizeInFloat4;
            
            // compute the new current frame matrix
            _sysmemBuffer[(windowOffsetInFloat4 + i * 3 + 0)] = new float4(scaleX, 0, 0, 0);
            _sysmemBuffer[(windowOffsetInFloat4 + i * 3 + 1)] = new float4(scaleY, 0, 0, 0);
            _sysmemBuffer[(windowOffsetInFloat4 + i * 3 + 2)] = new float4(scaleZ, X, Y, Z);
            
            _sysmemBuffer[(windowOffsetInFloat4 + _maxInstancePerWindow * 3 * 1 + i * 3 + 0)] = new float4(1, 0, 0, 0);
            _sysmemBuffer[(windowOffsetInFloat4 + _maxInstancePerWindow * 3 * 1 + i * 3 + 1)] = new float4(1, 0, 0, 0);
            _sysmemBuffer[(windowOffsetInFloat4 + _maxInstancePerWindow * 3 * 1 + i * 3 + 2)] = new float4(1, 0, 0, 0);

            float4 color = new float4(1, 1, 1, 1);
   
            
            _sysmemBuffer[windowOffsetInFloat4 + _maxInstancePerWindow * 3 * 2 + i] = color;
        }
        
        
        Container.UploadGpuData(kNumInstances);
        
    }

    private void OnDestroy()
    {
        if (Container != null)
        {
            Container.Shutdown();
        }
            
    }

}
