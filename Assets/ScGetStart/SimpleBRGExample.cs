using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;

public class SimpleBRGExample : MonoBehaviour
{
    public Mesh mesh;
    public Material material;

    private BatchRendererGroup m_BRG;

    
    private GraphicsBuffer m_InstanceData;
    private BatchID m_BatchID;
    private BatchMeshID m_MeshID;
    private BatchMaterialID m_MaterialID;
    

    // 一些辅助常量，用于使计算更方便。
    private const int kSizeOfMatrix = sizeof(float) * 4 * 4;                            //64    matrix4*4
    private const int kSizeOfPackedMatrix = sizeof(float) * 4 * 3;                      //48    压缩到 matrix4*3
    private const int kSizeOfFloat4 = sizeof(float) * 4;                                //16    color
    private const int kBytesPerInstance = (kSizeOfPackedMatrix * 2) + kSizeOfFloat4;    //112   2个4*4矩阵+1条颜色 objectToWorld + worldToObject + color
    private const int kExtraBytes = kSizeOfMatrix * 2;                                  //128
    private const int kNumInstances = 3;

    // PackedMatrix 结构用于表示 3x4 矩阵格式，用于将常规 4x4 矩阵 (Matrix4x4) 转换为 Unity SRP 着色器期望的格式。

    struct PackedMatrix
    {
        public float c0x;
        public float c0y;
        public float c0z;
        public float c1x;
        public float c1y;
        public float c1z;
        public float c2x;
        public float c2y;
        public float c2z;
        public float c3x;
        public float c3y;
        public float c3z;

        public PackedMatrix(Matrix4x4 m)
        {
            c0x = m.m00;
            c0y = m.m10;
            c0z = m.m20;
            c1x = m.m01;
            c1y = m.m11;
            c1z = m.m21;
            c2x = m.m02;
            c2y = m.m12;
            c2z = m.m22;
            c3x = m.m03;
            c3y = m.m13;
            c3z = m.m23;
        }
    }

    private void Start()
    {
        // 创建 BatchRendererGroup，用于执行剔除（culling）和渲染。
        m_BRG = new BatchRendererGroup(this.OnPerformCulling, IntPtr.Zero);
        m_MeshID = m_BRG.RegisterMesh(mesh);
        m_MaterialID = m_BRG.RegisterMaterial(material);

        // 分配实例数据缓冲区并填充数据。
        AllocateInstanceDataBuffer();
        PopulateInstanceDataBuffer();
    }

    private void AllocateInstanceDataBuffer()
    {
        // 使用 GraphicsBuffer 类分配实例数据缓冲区的内存。
        m_InstanceData = new GraphicsBuffer(GraphicsBuffer.Target.Raw,
            BufferCountForInstances(kBytesPerInstance, kNumInstances, kExtraBytes),
            sizeof(int));
    }

    private void PopulateInstanceDataBuffer()
    {
        // 在实例数据缓冲区的开头放置一个零矩阵，以便从地址 0 处加载返回零。
        var zero = new Matrix4x4[1] { Matrix4x4.zero };

        // 创建三个示例实例的变换矩阵。
        var matrices = new Matrix4x4[kNumInstances]
        {
            Matrix4x4.Translate(new Vector3(-2, 0, 0)),
            Matrix4x4.Translate(new Vector3(0, 0, 0)),
            Matrix4x4.Translate(new Vector3(2, 0, 0)),
        };

        // 将变换矩阵转换为着色器期望的打包格式。
        var objectToWorld = new PackedMatrix[kNumInstances]
        {
            new PackedMatrix(matrices[0]),
            new PackedMatrix(matrices[1]),
            new PackedMatrix(matrices[2]),
        };
        

        // 同样创建打包的逆矩阵。
        var worldToObject = new PackedMatrix[kNumInstances]
        {
            new PackedMatrix(matrices[0].inverse),
            new PackedMatrix(matrices[1].inverse),
            new PackedMatrix(matrices[2].inverse),
        };

        // 使所有实例具有唯一的颜色。
        var colors = new Vector4[kNumInstances]
        {
            new Vector4(1, 0, 0, 1),
            new Vector4(0, 1, 0, 1),
            new Vector4(0, 0, 1, 1),
        };

        // 在这个简单的示例中，实例数据以以下方式放置在缓冲区中：
        //    偏移 | 描述
        //      0 | 64 字节的零值，以便从地址 0 处加载返回零
        //     64 | 32 未初始化的字节，以便更容易使用 SetData 进行操作，否则不必要
        //     96 | unity_ObjectToWorld，三个打包的 float3x4 矩阵
        //    240 | unity_WorldToObject，三个打包的 float3x4 矩阵
        //    384 | _BaseColor，三个 float4

        // 计算不同实例属性的起始地址。unity_ObjectToWorld 从地址 96 开始，而不是从 64 开始，
        // 这意味着还有 32 位未初始化。这是因为 computeBufferStartIndex 参数要求起始偏移量必须可以被源数组元素类型的大小整除。
        // 在这种情况下，源数组元素类型的大小为 PackedMatrix 的大小，即 48。
        uint byteAddressObjectToWorld = kSizeOfPackedMatrix * 2;
        uint byteAddressWorldToObject = byteAddressObjectToWorld + kSizeOfPackedMatrix * kNumInstances;
        uint byteAddressColor = byteAddressWorldToObject + kSizeOfPackedMatrix * kNumInstances;

        // 将实例数据上传到 GraphicsBuffer，以便着色器加载。
        m_InstanceData.SetData(zero, 0, 0, 1);
        m_InstanceData.SetData(objectToWorld, 0, (int)(byteAddressObjectToWorld / kSizeOfPackedMatrix), objectToWorld.Length);
        m_InstanceData.SetData(worldToObject, 0, (int)(byteAddressWorldToObject / kSizeOfPackedMatrix), worldToObject.Length);
        m_InstanceData.SetData(colors, 0, (int)(byteAddressColor / kSizeOfFloat4), colors.Length);

        // 设置元数据值，以指向实例数据。设置每个最高有效位 0x80000000，
        // 指示着色器数据是一个数组，每个值与实例索引相关。
        // 着色器使用的未在此处设置的任何元数据值都将为零。当着色器在 UNITY_ACCESS_DOTS_INSTANCED_PROP（即无默认值）中使用这样的值时，
        // 着色器解释为从缓冲区的开头加载。缓冲区的开头是零矩阵，因此这种加载保证返回零，这是一个合理的默认值。
        var metadata = new NativeArray<MetadataValue>(3, Allocator.Temp);
        metadata[0] = new MetadataValue { NameID = Shader.PropertyToID("unity_ObjectToWorld"), Value = 0x80000000 | byteAddressObjectToWorld, };
        metadata[1] = new MetadataValue { NameID = Shader.PropertyToID("unity_WorldToObject"), Value = 0x80000000 | byteAddressWorldToObject, };
        metadata[2] = new MetadataValue { NameID = Shader.PropertyToID("_BaseColor"), Value = 0x80000000 | byteAddressColor, };

        // 最后，为实例创建批处理，并使用实例数据的 GraphicsBuffer 以及指定属性位置的元数据值。
        m_BatchID = m_BRG.AddBatch(metadata, m_InstanceData.bufferHandle);
    }

    // Raw 缓冲区以 int 分配。这是一个实用程序方法，用于计算所需的整数数量以存储数据。
    int BufferCountForInstances(int bytesPerInstance, int numInstances, int extraBytes = 0)
    {
        // 将字节数四舍五入到整数的倍数
        bytesPerInstance = (bytesPerInstance + sizeof(int) - 1) / sizeof(int) * sizeof(int);
        extraBytes = (extraBytes + sizeof(int) - 1) / sizeof(int) * sizeof(int);
        int totalBytes = bytesPerInstance * numInstances + extraBytes;
        return totalBytes / sizeof(int);
    }

    private void OnDisable()
    {
        // 清理资源时，释放 BatchRendererGroup。
        m_BRG.Dispose();
    }

    public unsafe JobHandle OnPerformCulling(
        BatchRendererGroup rendererGroup,
        BatchCullingContext cullingContext,
        BatchCullingOutput cullingOutput,
        IntPtr userContext)
    {
        // UnsafeUtility.Malloc() 需要对齐，因此使用最大的整数类型对齐作为合理的默认值。
        int alignment = UnsafeUtility.AlignOf<long>();

        // 获取 BatchCullingOutputDrawCommands 结构的指针，以便可以直接修改它。
        var drawCommands = (BatchCullingOutputDrawCommands*)cullingOutput.drawCommands.GetUnsafePtr();

        // 为输出数组分配内存。在更复杂的实现中，您将根据可见性动态计算要分配的内存量。
        // 本示例假设所有实例都可见，并因此为每个实例分配内存。所需的分配如下：
        // - 一个绘制命令（用于绘制 kNumInstances 个实例）
        // - 一个绘制范围（覆盖我们的单个绘制命令）
        // - kNumInstances 可见实例索引。
        // 必须始终使用 Allocator.TempJob 分配数组。
        drawCommands->drawCommands = (BatchDrawCommand*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<BatchDrawCommand>(), alignment, Allocator.TempJob);
        drawCommands->drawRanges = (BatchDrawRange*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<BatchDrawRange>(), alignment, Allocator.TempJob);
        drawCommands->visibleInstances = (int*)UnsafeUtility.Malloc(kNumInstances * sizeof(int), alignment, Allocator.TempJob);
        drawCommands->drawCommandPickingInstanceIDs = null;

        drawCommands->drawCommandCount = 1;
        drawCommands->drawRangeCount = 1;
        drawCommands->visibleInstanceCount = kNumInstances;

        // 本示例不使用深度排序，因此将 instanceSortingPositions 设置为 null。
        drawCommands->instanceSortingPositions = null;
        drawCommands->instanceSortingPositionFloatCount = 0;

        // 配置单个绘制命令，以绘制 kNumInstances 个实例，从数组的偏移量 0 处开始，使用在 Start() 方法中注册的 batch、material 和 mesh ID。
        // 它不设置任何特殊标志。
        drawCommands->drawCommands[0].visibleOffset = 0;
        drawCommands->drawCommands[0].visibleCount = kNumInstances;
        drawCommands->drawCommands[0].batchID = m_BatchID;
        drawCommands->drawCommands[0].materialID = m_MaterialID;
        drawCommands->drawCommands[0].meshID = m_MeshID;
        drawCommands->drawCommands[0].submeshIndex = 0;
        drawCommands->drawCommands[0].splitVisibilityMask = 0xff;
        drawCommands->drawCommands[0].flags = 0;
        drawCommands->drawCommands[0].sortingPosition = 0;

        // 配置单个绘制范围，覆盖位于偏移 0 处的单个绘制命令。
        drawCommands->drawRanges[0].drawCommandsBegin = 0;
        drawCommands->drawRanges[0].drawCommandsCount = 1;

        // 本示例不关心阴影或运动矢量，因此除了 renderingLayerMask 设置为全部为 1 外，其他值都保持默认值为零。
        drawCommands->drawRanges[0].filterSettings = new BatchFilterSettings { renderingLayerMask = 0xffffffff, };

        // 最后，将实际的可见实例索引写入数组。在更复杂的实现中，输出将依赖于可见性，但本示例假定所有实例都可见。
        for (int i = 0; i < kNumInstances; ++i)
            drawCommands->visibleInstances[i] = i;

        // 这个简单示例不使用作业，因此返回一个空的 JobHandle。
        // 鼓励性能敏感的应用程序使用异步作业来执行剔除和渲染。
        return new JobHandle();
    }
}
