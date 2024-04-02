// using Nebukam.ORCA;
// using System;
// using Unity.Burst;
// using Unity.Collections;
// using Unity.Collections.LowLevel.Unsafe;
// using Unity.Jobs;
// using Unity.Mathematics;
// using UnityEngine;
// using UnityEngine.Rendering;
// namespace BRG
// {
//     public class PlayerRenderGroup : MonoBehaviour
//     {
//         public Mesh mesh;
//         public Material material;
//         public ORCASetupRing orcaTest;
//         private BatchRendererGroup m_BRG;
//  
//         private GraphicsBuffer m_InstanceData;
//         private BatchID m_BatchID;
//         private BatchMeshID m_MeshID;
//         private BatchMaterialID m_MaterialID;
//  
//         // Some helper constants to make calculations more convenient.
//         private const int kSizeOfMatrix = sizeof(float) * 4 * 4;
//         private const int kSizeOfPackedMatrix = sizeof(float) * 3 * 4;
//         private const int kSizeOfFloat4 = sizeof(float) * 4;
//         private const int kBytesPerInstance = (kSizeOfPackedMatrix * 2) + kSizeOfFloat4;
//         private const int kExtraBytes = kSizeOfMatrix * 2;
//         [SerializeField] private int kNumInstances = 20000;
//         [SerializeField] private int m_RowCount = 200;
//         private NativeArray<float4x4> matrices;
//         private NativeArray<float3x4> objectToWorldMatrices;
//         private NativeArray<float3x4> worldToObjectMatrices;
//         private float4[] colors;
//  
//         private void Start()
//         {
//             m_BRG = new BatchRendererGroup(this.OnPerformCulling, IntPtr.Zero);
//             m_MeshID = m_BRG.RegisterMesh(mesh);
//             m_MaterialID = m_BRG.RegisterMaterial(material);
//             AllocateInstanceDateBuffer();
//             PopulateInstanceDataBuffer();
//         }
//  
//         private uint byteAddressObjectToWorld;
//         private uint byteAddressWorldToObject;
//         private uint byteAddressColor;
//  
//         private void Update()
//         {
//             var agents = orcaTest.GetAgents();
//             NativeArray<float3> tempPosArr = new NativeArray<float3>(matrices.Length, Allocator.TempJob);
//             NativeArray<quaternion> tempRotaArr = new NativeArray<quaternion>(matrices.Length, Allocator.TempJob);
//             for (int i = 0; i < agents.Count; i++)
//             {
//                 var agent = agents[i];
//                 tempPosArr[i] = agent.pos;
//                 tempRotaArr[i] = agent.rotation;
//             }
//             var matricesJob = new UpdateRendererMatricesJob()
//             {
//                 positionArr = tempPosArr,
//                 rotationArr = tempRotaArr,
//                 matrices = matrices,
//                 obj2WorldArr = objectToWorldMatrices,
//                 world2ObjArr = worldToObjectMatrices
//             };
//             var jobHandle = matricesJob.Schedule(matrices.Length, 64);
//             jobHandle.Complete();
//             matrices = matricesJob.matrices;
//             objectToWorldMatrices = matricesJob.obj2WorldArr;
//             worldToObjectMatrices = matricesJob.world2ObjArr;
//             RefreshData();
//             tempPosArr.Dispose();
//             tempRotaArr.Dispose();
//         }
//  
//         private void AllocateInstanceDateBuffer()
//         {
//             m_InstanceData = new GraphicsBuffer(GraphicsBuffer.Target.Raw,
//                 BufferCountForInstances(kBytesPerInstance, kNumInstances, kExtraBytes),
//                 sizeof(int));
//         }
//         private void RefreshData()
//         {
//             m_InstanceData.SetData(objectToWorldMatrices, 0, (int)(byteAddressObjectToWorld / kSizeOfPackedMatrix), objectToWorldMatrices.Length);
//             m_InstanceData.SetData(worldToObjectMatrices, 0, (int)(byteAddressWorldToObject / kSizeOfPackedMatrix), worldToObjectMatrices.Length);
//         }
//         public float3x4 ConvertFloat4x4To3x4(float4x4 matrix)
//         {
//             return new float3x4(matrix.c0.xyz, matrix.c1.xyz, matrix.c2.xyz, matrix.c3.xyz);
//         }
//         private void PopulateInstanceDataBuffer()
//         {
//             // Place a zero matrix at the start of the instance data buffer, so loads from address 0 return zero.
//             var zero = new Matrix4x4[1] { Matrix4x4.zero };
//  
//             // Create transform matrices for three example instances.
//             matrices = new NativeArray<float4x4>(kNumInstances, Allocator.Persistent);
//             // Convert the transform matrices into the packed format that shaders expects.
//             objectToWorldMatrices = new NativeArray<float3x4>(kNumInstances, Allocator.Persistent);
//             // Also create packed inverse matrices.
//             worldToObjectMatrices = new NativeArray<float3x4>(kNumInstances, Allocator.Persistent);
//             colors = orcaTest.AgentColors;
//             var offset = new Vector3(m_RowCount, 0, Mathf.CeilToInt(kNumInstances / (float)m_RowCount)) * 0.5f;
//             for (int i = 0; i < kNumInstances; i++)
//             {
//                 matrices[i] = Matrix4x4.Translate(new Vector3(i % m_RowCount, 0, i / m_RowCount) - offset);
//                 objectToWorldMatrices[i] = ConvertFloat4x4To3x4(matrices[i]);
//                 worldToObjectMatrices[i] = ConvertFloat4x4To3x4(Unity.Mathematics.math.inverse(matrices[0]));
//                 //colors[i] = orcaTest.AgentColors[i];
//             }
//  
//             byteAddressObjectToWorld = kSizeOfPackedMatrix * 2;
//             byteAddressWorldToObject = (uint)(byteAddressObjectToWorld + kSizeOfPackedMatrix * kNumInstances);
//             byteAddressColor = (uint)(byteAddressWorldToObject + kSizeOfPackedMatrix * kNumInstances);
//  
//             // Upload the instance data to the GraphicsBuffer so the shader can load them.
//             m_InstanceData.SetData(zero, 0, 0, 1);
//             m_InstanceData.SetData(objectToWorldMatrices, 0, (int)(byteAddressObjectToWorld / kSizeOfPackedMatrix), objectToWorldMatrices.Length);
//             m_InstanceData.SetData(worldToObjectMatrices, 0, (int)(byteAddressWorldToObject / kSizeOfPackedMatrix), worldToObjectMatrices.Length);
//             m_InstanceData.SetData(colors, 0, (int)(byteAddressColor / kSizeOfFloat4), colors.Length);
//  
//             var metadata = new NativeArray<MetadataValue>(3, Allocator.Temp);
//             metadata[0] = new MetadataValue { NameID = Shader.PropertyToID("unity_ObjectToWorld"), Value = 0x80000000 | byteAddressObjectToWorld, };
//             metadata[1] = new MetadataValue { NameID = Shader.PropertyToID("unity_WorldToObject"), Value = 0x80000000 | byteAddressWorldToObject, };
//             metadata[2] = new MetadataValue { NameID = Shader.PropertyToID("_BaseColor"), Value = 0x80000000 | byteAddressColor, };
//  
//             m_BatchID = m_BRG.AddBatch(metadata, m_InstanceData.bufferHandle);
//         }
//  
//         int BufferCountForInstances(int bytesPerInstance, int numInstances, int extraBytes = 0)
//         {
//             // Round byte counts to int multiples
//             bytesPerInstance = (bytesPerInstance + sizeof(int) - 1) / sizeof(int) * sizeof(int);
//             extraBytes = (extraBytes + sizeof(int) - 1) / sizeof(int) * sizeof(int);
//             int totalBytes = bytesPerInstance * numInstances + extraBytes;
//             return totalBytes / sizeof(int);
//         }
//  
//         private void OnDestroy()
//         {
//             m_BRG.Dispose();
//             matrices.Dispose();
//             objectToWorldMatrices.Dispose();
//             worldToObjectMatrices.Dispose();
//             //colors.Dispose();
//         }
//  
//         public unsafe JobHandle OnPerformCulling(
//             BatchRendererGroup rendererGroup,
//             BatchCullingContext cullingContext,
//             BatchCullingOutput cullingOutput,
//             IntPtr userContext)
//         {
//  
//             int alignment = UnsafeUtility.AlignOf<long>();
//  
//             var drawCommands = (BatchCullingOutputDrawCommands*)cullingOutput.drawCommands.GetUnsafePtr();
//             drawCommands->drawCommands = (BatchDrawCommand*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<BatchDrawCommand>(), alignment, Allocator.TempJob);
//             drawCommands->drawRanges = (BatchDrawRange*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<BatchDrawRange>(), alignment, Allocator.TempJob);
//             drawCommands->visibleInstances = (int*)UnsafeUtility.Malloc(kNumInstances * sizeof(int), alignment, Allocator.TempJob);
//             drawCommands->drawCommandPickingInstanceIDs = null;
//  
//             drawCommands->drawCommandCount = 1;
//             drawCommands->drawRangeCount = 1;
//             drawCommands->visibleInstanceCount = kNumInstances;
//  
//             drawCommands->instanceSortingPositions = null;
//             drawCommands->instanceSortingPositionFloatCount = 0;
//  
//             drawCommands->drawCommands[0].visibleOffset = 0;
//             drawCommands->drawCommands[0].visibleCount = (uint)kNumInstances;
//             drawCommands->drawCommands[0].batchID = m_BatchID;
//             drawCommands->drawCommands[0].materialID = m_MaterialID;
//             drawCommands->drawCommands[0].meshID = m_MeshID;
//             drawCommands->drawCommands[0].submeshIndex = 0;
//             drawCommands->drawCommands[0].splitVisibilityMask = 0xff;
//             drawCommands->drawCommands[0].flags = 0;
//             drawCommands->drawCommands[0].sortingPosition = 0;
//  
//             drawCommands->drawRanges[0].drawCommandsBegin = 0;
//             drawCommands->drawRanges[0].drawCommandsCount = 1;
//  
//             drawCommands->drawRanges[0].filterSettings = new BatchFilterSettings { renderingLayerMask = 0xffffffff, };
//  
//             for (int i = 0; i < kNumInstances; ++i)
//                 drawCommands->visibleInstances[i] = i;
//             return new JobHandle();
//         }
//     }
//     [BurstCompile]
//     partial struct UpdateRendererMatricesJob : IJobParallelFor
//     {
//         [ReadOnly]
//         public NativeArray<float3> positionArr;
//         [ReadOnly]
//         public NativeArray<quaternion> rotationArr;
//         public NativeArray<float4x4> matrices;
//         public NativeArray<float3x4> obj2WorldArr;
//         public NativeArray<float3x4> world2ObjArr;
//         [BurstCompile]
//         public void Execute(int index)
//         {
//             quaternion ExtractRotationFromMatrix(float4x4 matrix)
//             {
//                 // 获取矩阵的前三列，分别代表旋转、缩放和平移
//                 float3x3 rotationMatrix = new float3x3(matrix.c0.xyz, matrix.c1.xyz, matrix.c2.xyz);
//
//                 // 使用 Unity 的函数将 3x3 旋转矩阵转换为四元数
//                 quaternion rotation = quaternion.LookRotationSafe(rotationMatrix.c2, rotationMatrix.c1);
//
//                 return rotation;
//             }
//             
//             
//             var mat = matrices[index];
//             var rotation = rotationArr[index];
//             if (rotation.Equals(quaternion.identity))
//             {
//                 // rotation = mat.Rotation();
//                 rotation = ExtractRotationFromMatrix(mat);
//             }
//             // mat = float4x4.TRS(positionArr[index], rotation, mat.Scale());
//             mat = float4x4.TRS(positionArr[index], rotation, math.length(mat.c0.xyz));
//             matrices[index] = mat;
//             obj2WorldArr[index] = ConvertFloat4x4To3x4(mat);
//             world2ObjArr[index] = ConvertFloat4x4To3x4(math.inverse(mat));
//         }
//         public float3x4 ConvertFloat4x4To3x4(float4x4 matrix)
//         {
//             return new float3x4(matrix.c0.xyz, matrix.c1.xyz, matrix.c2.xyz, matrix.c3.xyz);
//         }
//     }
// }