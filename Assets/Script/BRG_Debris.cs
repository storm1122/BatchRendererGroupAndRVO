using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Jobs;
using UnityEngine;
using System.Threading;
using System.Collections.Generic;

public unsafe class BRG_Debris : MonoBehaviour
{
    public Mesh m_mesh;
    public Material m_material;
    public bool m_castShadows;

    public static BRG_Debris gDebrisManager;
    
    
    //残骸数的最大值为 16384，则原始缓冲区便为 112x16384 字节，或 1.75 MiB。
    public const int kMaxDebris = 16 * 1024;
    public const int kDebrisGpuSize = (3+3+1)*16;     // 3float4 for obj2world, 3 float4 for w2obj and 1 float4 for color
    
    
    private const int kMaxJustLandedPerFrame = 256;
    private const int kMaxDeadPerFrame = 256;
    private const float kDebrisScale = 1.0f/4.0f;

    private BRG_Container m_brgContainer;

    private const int kDebrisCounter = 0;
    private const int kGpuItemsCounter = 1;
    private const int kJustLandedCounter = 2;
    private const int kJustDeadCounter = 3;
    private const int kTotalCounters = 4;

    private Unity.Mathematics.Random m_rndGen;

    struct GfxItem
    {
        public float3 pos;
        public int groundCell;
        public float3 speed;
        public float3x3 mat;
        public float3 color;
        public float antiZFight;
        public int landedCount;
    };

    struct DebrisSpawnDesc
    {
        public float3 pos;
        public int count;
        public float rndHueColor;
    };

    private NativeArray<GfxItem> m_gfxItems;
    private List<DebrisSpawnDesc> m_debrisExplosions = new List<DebrisSpawnDesc>();

    public NativeArray<int> m_inOutCounters;
    public NativeArray<int> m_justLandedList;
    public NativeArray<int> m_justDeadList;


    public void Awake()
    {
        gDebrisManager = this;
    }

    // Start is called before the first frame update
    void Start()
    {
        m_rndGen = new Unity.Mathematics.Random(0x22112003);

        m_brgContainer = new BRG_Container();
        m_brgContainer.Init(m_mesh, m_material, kMaxDebris, kDebrisGpuSize, m_castShadows);

        m_inOutCounters = new NativeArray<int>(kTotalCounters, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        m_justLandedList = new NativeArray<int>(kMaxJustLandedPerFrame, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        m_justDeadList = new NativeArray<int>(kMaxDeadPerFrame, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

        // setup positions & scale of each background elements
        m_gfxItems = new NativeArray<GfxItem>(kMaxDebris, Allocator.Persistent, NativeArrayOptions.ClearMemory);

        
    }


    public void GenerateBurstOfDebris(Vector3 pos, int count, float rndHue)
    {
        DebrisSpawnDesc foo;
        foo.count = count;
        foo.pos = pos;
        foo.rndHueColor = rndHue;
        m_debrisExplosions.Add(foo);
    }

    public void UploadGpuData()
    {
        m_brgContainer.UploadGpuData(m_inOutCounters[kGpuItemsCounter]);
    }

    private void OnDestroy()
    {
        if ( m_brgContainer != null )
            m_brgContainer.Shutdown();

        m_gfxItems.Dispose();
        m_inOutCounters.Dispose();
        m_justLandedList.Dispose();
        m_justDeadList.Dispose();
    }
}
