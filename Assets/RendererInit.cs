// using System.Collections;
// using System.Collections.Generic;
// using Unity.Burst;
// using Unity.Collections;
// using Unity.Jobs;
// using Unity.Mathematics;
// using UnityEngine;
//
// public class RendererInit
// {
//     public Mesh mesh;
//     public Material material;
//
//     public Container Container;
//
//
//     // 一些辅助常量，用于使计算更方便。
//     private const int kSizeOfMatrix = sizeof(float) * 4 * 4;                            //64    matrix4*4
//     private const int kSizeOfPackedMatrix = sizeof(float) * 4 * 3;                      //48    压缩到 matrix4*3
//     private const int kSizeOfFloat4 = sizeof(float) * 4;                                //16    color
//     private const int kBytesPerInstance = (kSizeOfPackedMatrix * 2) + kSizeOfFloat4;    //112   2个4*4矩阵+1条颜色 objectToWorld + worldToObject + color
//     private const int kExtraBytes = kSizeOfMatrix * 2;                                  //128
//     public int kNumInstances = 1;
//
//
//     public void Init()
//     {
//         Container = new Container();
//
//         Container.Init(mesh, material, kNumInstances, kBytesPerInstance);
//
//         Container.UploadGpuData(kNumInstances);
//
//     }
//
//     
//     [BurstCompile]
//     public struct RendererNodeJob : IJobParallelFor
//     {
//         public NativeArray<RendererNode> Nodes;
//         public void Execute(int index)
//         {
//             var note = Nodes[index];
//             Debug.Log($"index:{index} {note.position}");
//         }
//     }
//     
//     public void Update()
//     {
//         
//     }
//     
//     public void LateUpdate()
//     {
//         Container.UploadGpuData(kNumInstances);
//     }
//
//     [BurstCompile]
//     private struct PhysicsUpdateJob : IJobFor
//     {
//         [NativeDisableParallelForRestriction] [WriteOnly]
//         public NativeArray<float4> _sysmemBuffer;
//         
//         public void Execute(int index)
//         {
//             
//             int totalGpuBufferSize;
//             int alignedWindowSize;
//             NativeArray<float4> _sysmemBuffer = Container.GetSysmemBuffer(out totalGpuBufferSize, out alignedWindowSize);
//
//             var _maxInstancePerWindow = alignedWindowSize / kBytesPerInstance;
//             var _windowSizeInFloat4 = alignedWindowSize / 16;
//
//             
//             int i;
//             int windowId = System.Math.DivRem(index, _maxInstancePerWindow, out i);
//             
//             int windowOffsetInFloat4 = windowId * _windowSizeInFloat4;
//             
//             Vector3 bpos = transform.localPosition;
//             
//             Matrix4x4 unityMatrix = transform.localToWorldMatrix;
//             float3x3 rot = new float3x3(
//                 unityMatrix.m00, unityMatrix.m01, unityMatrix.m02,
//                 unityMatrix.m10, unityMatrix.m11, unityMatrix.m12,
//                 unityMatrix.m20, unityMatrix.m21, unityMatrix.m22
//             );
//
//             // compute the new current frame matrix
//             _sysmemBuffer[(windowOffsetInFloat4 + i * 3 + 0)] = new float4(rot.c0.x, rot.c0.y, rot.c0.z, rot.c1.x);
//             _sysmemBuffer[(windowOffsetInFloat4 + i * 3 + 1)] = new float4(rot.c1.y, rot.c1.z, rot.c2.x, rot.c2.y);
//             _sysmemBuffer[(windowOffsetInFloat4 + i * 3 + 2)] = new float4(rot.c2.z, bpos.x, bpos.y, bpos.z);
//
//             // compute the new inverse matrix
//             _sysmemBuffer[(windowOffsetInFloat4 + _maxInstancePerWindow * 3 * 1 + i * 3 + 0)] = new float4(rot.c0.x, rot.c1.x, rot.c2.x, rot.c0.y);
//             _sysmemBuffer[(windowOffsetInFloat4 + _maxInstancePerWindow * 3 * 1 + i * 3 + 1)] = new float4(rot.c1.y, rot.c2.y, rot.c0.z, rot.c1.z);
//             _sysmemBuffer[(windowOffsetInFloat4 + _maxInstancePerWindow * 3 * 1 + i * 3 + 2)] = new float4(rot.c2.z, -bpos.x, -bpos.y, -bpos.z);
//
//             
//             float4 color = new float4(1, 1, 1, 1);
//    
//             
//             _sysmemBuffer[windowOffsetInFloat4 + _maxInstancePerWindow * 3 * 2 + i] = color;
//             
//         }
//     }
//     
//     private void updateGpuSysmemBuffer(int index, in GfxItem item)
//     {
//         int i;
//         int windowId = System.Math.DivRem(index, _maxInstancePerWindow, out i);
//
//         int windowOffsetInFloat4 = windowId * _windowSizeInFloat4;
//         Vector3 bpos = item.pos;
//         float3x3 rot = item.mat;
//
//         // compute the new current frame matrix
//         _sysmemBuffer[(windowOffsetInFloat4 + i * 3 + 0)] = new float4(rot.c0.x, rot.c0.y, rot.c0.z, rot.c1.x);
//         _sysmemBuffer[(windowOffsetInFloat4 + i * 3 + 1)] = new float4(rot.c1.y, rot.c1.z, rot.c2.x, rot.c2.y);
//         _sysmemBuffer[(windowOffsetInFloat4 + i * 3 + 2)] = new float4(rot.c2.z, bpos.x, bpos.y, bpos.z);
//
//         // compute the new inverse matrix
//         _sysmemBuffer[(windowOffsetInFloat4 + _maxInstancePerWindow * 3 * 1 + i * 3 + 0)] = new float4(rot.c0.x, rot.c1.x, rot.c2.x, rot.c0.y);
//         _sysmemBuffer[(windowOffsetInFloat4 + _maxInstancePerWindow * 3 * 1 + i * 3 + 1)] = new float4(rot.c1.y, rot.c2.y, rot.c0.z, rot.c1.z);
//         _sysmemBuffer[(windowOffsetInFloat4 + _maxInstancePerWindow * 3 * 1 + i * 3 + 2)] = new float4(rot.c2.z, -bpos.x, -bpos.y, -bpos.z);
//
//         // update colors
//         _sysmemBuffer[windowOffsetInFloat4 + _maxInstancePerWindow * 3 * 2 + i] = new float4(item.color, 1);
//
//     }
// }
