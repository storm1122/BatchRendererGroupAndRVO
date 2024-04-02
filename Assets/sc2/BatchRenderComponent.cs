using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Mathematics;

[Serializable]
public class RendererResources
{
    public DrawKey Key;

    public int capacity;
    public Mesh mesh;
    public Material material;
}
    
public struct DrawKey : IEquatable<DrawKey>, IComparable<DrawKey>
{
    public BatchMeshID MeshID;
    public uint SubmeshIndex;
    public BatchMaterialID MaterialID;

    public override int GetHashCode()
    {
        return HashCode.Combine(MeshID, SubmeshIndex, MaterialID);
    }

    public int CompareTo(DrawKey other)
    {
        int cmpMaterial = MaterialID.CompareTo(other.MaterialID);
        int cmpMesh = MeshID.CompareTo(other.MeshID);
        int cmpSubmesh = SubmeshIndex.CompareTo(other.SubmeshIndex);

        if (cmpMaterial != 0)
            return cmpMaterial;
        if (cmpMesh != 0)
            return cmpMesh;

        return cmpSubmesh;
    }

    public bool Equals(DrawKey other) => CompareTo(other) == 0;
}
        
public struct SrpBatch
{
    public BatchID BatchID;
    public DrawKey DrawKey;
    public int GraphicsBufferOffsetInFloat4;
    public int InstanceOffset;
    public int InstanceCount;
}

public class BatchRenderComponent : MonoBehaviour
{
    private List<RendererNode> m_AllRendererNodes = new List<RendererNode>();
    private Dictionary<int, int> m_BatchesVisibleCount = new Dictionary<int, int>();
    
    public RendererResources[] m_RendererResources;
    

    
}