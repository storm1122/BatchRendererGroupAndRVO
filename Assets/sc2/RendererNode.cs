using Unity.Mathematics;
using static Unity.Mathematics.math;
using Unity.Burst;

public struct RendererNodeId
{
    public long drawKey;
    public int index;

    public RendererNodeId(long drawKey, int index)
    {
        this.drawKey = drawKey;
        this.index = index;
    }
}


[BurstCompile]
public struct RendererNode
{
    public RendererNodeId Id { get; private set; }
    public bool Enable
    {
        get
        {
            return active && visible;
        }
    }
    /// <summary>
    /// 是否启用
    /// </summary>
    public bool active;
    /// <summary>
    /// 是否在视口内
    /// </summary>
    public bool visible;
    /// <summary>
    /// 位置
    /// </summary>
    public float3 position;
    /// <summary>
    /// 旋转
    /// </summary>
    public quaternion rotation;
    /// <summary>
    /// 缩放
    /// </summary>
    public float3 localScale;
    /// <summary>
    /// 顶点颜色
    /// </summary>
    public float4 color;
 
    /// <summary>
    /// 动画id
    /// </summary>
    public float4 animClipId;
    /// <summary>
    /// Mesh的原始AABB(无缩放)
    /// </summary>
    public AABB unscaleAABB;
 
    /// <summary>
    /// 受缩放影响的AABB
    /// </summary>
    public AABB aabb
    {
        get
        {
            //var result = unscaleAABB;
            //result.Extents *= localScale;
            return unscaleAABB;
        }
    }
    public bool IsEmpty
    {
        get
        {
            return unscaleAABB.Size.Equals(Unity.Mathematics.float3.zero);
        }
    }
    public static readonly RendererNode Empty = new RendererNode();
    public RendererNode(RendererNodeId id, float3 position, quaternion rotation, float3 localScale, AABB meshAABB)
    {
        this.Id = id;
        this.position = position;
        this.rotation = rotation;
        this.localScale = localScale;
        this.unscaleAABB = meshAABB;
        this.color = float4(1);
        this.active = false;
        this.visible = true;
        this.animClipId = 0;
    }
    /// <summary>
    /// 构建矩阵
    /// </summary>
    /// <returns></returns>
    [BurstCompile]
    public float4x4 BuildMatrix()
    {
        return Unity.Mathematics.float4x4.TRS(position, rotation, localScale);
    }
}