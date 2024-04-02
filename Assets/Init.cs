using System.Collections;
using System.Collections.Generic;
using BurstGridSearch;
using Nebukam.Common;
// using Nebukam.Common.Editor;
using Nebukam.ORCA;
using TMPro;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Unity.Jobs.LowLevel.Unsafe;
using static Unity.Mathematics.math;
using float3 = Unity.Mathematics.float3;
using float4 = Unity.Mathematics.float4;
using Random = Unity.Mathematics.Random;

public class Init : MonoBehaviour
{
    private float DeltaTime;
    public TextMeshProUGUI text;
    public TextMeshProUGUI workerCountText;
    public Transform Target;
    public AgentGroup<RVOAgent> agents;
    public ObstacleGroup obstacles;

    public ORCA simulation;

    public int Count = 5;

    public NativeArray<RendererNode> m_AllRendererNodes;
    public NativeArray<float3> allPos;
    
    
    public Mesh mesh;
    public Material material;

    // public Container Container;
    
    private BRG_Container m_brgContainer;
    
    GridSearchBurst m_GridSearch = new GridSearchBurst(-1.0f, 28);
    private bool m_GirdInitialized;


    private const int kGpuItemSize = (3 * 2 + 1) * 16;  //  GPU item size ( 2 * 4x3 matrices plus 1 color per item )   ->  3float4 for obj2world, 3 float4 for w2obj and 1 float4 for color
    //128
    private int kNumInstances = 1;
    
    // Start is called before the first frame update
    void Start()
    {
        agents = new AgentGroup<RVOAgent>();
        obstacles = new ObstacleGroup();
            
        simulation = new ORCA();
        AxisPair axis = AxisPair.XZ;
        simulation.plane = axis; //设置XY方向寻路还是XZ方向
        simulation.agents = agents;
        simulation.staticObstacles = obstacles;

    
        m_AllRendererNodes = new NativeArray<RendererNode>(Count, Allocator.Persistent);
        allPos = new NativeArray<float3>(Count, Allocator.Persistent);
        CreateAgents();

        kNumInstances = Count;
        
        m_brgContainer = new BRG_Container();
        
        m_brgContainer.Init(mesh, material, kNumInstances, kGpuItemSize, false);
        
        m_brgContainer.UploadGpuData(kNumInstances);


        text.text = Count.ToString();

#if !UNITY_EDITOR
        Application.targetFrameRate = 60;
#endif
        
    }

    void CreateAgents()
    {
        RVOAgent a;            

        for (int i = 0; i < Count; i++)
        {

            var pos = UnityEngine.Random.insideUnitCircle * 20;
            a = agents.Add(float3(pos.x, 0, pos.y));
            a.radius = 0.2f;
            a.radiusObst = a.radius;
            
            // var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            // a.CachedTransform = go.transform;
            
            a.prefVelocity = float3(0f);
            a.maxSpeed = 3;
            a.timeHorizon = 5;
            a.maxNeighbors = 10;
            a.neighborDist = 10;
            a.timeHorizonObst = 5;

            // a.targetPosition = float3(100f + i, 0f, 0f);
            // a.targetPosition = float3(Target.position.x, 0f, Target.position.z);

            var node = new RendererNode();
            node.position = a.pos;
            node.rotation = a.rotation;
            node.color = float4(1, 1, 1, 1);

            m_AllRendererNodes[i] = node;
        }
    }


    // Update is called once per frame
    private void Update()
    {
        for (int i = 0; i < Count; i++)
        {
            var agent = agents[i];
            agent.targetPosition = float3(Target.position.x, 0f, Target.position.z);
            
            // Draw.Circle(agent.pos, agent.radius, Color.green, 12);
        }



        //Schedule the simulation job. 
        simulation.Schedule(Time.deltaTime);



        var h = Input.GetAxis("Horizontal");
        var v = Input.GetAxis("Vertical");

        if (abs(h) > 0.01f || abs(v) > 0.01f)
        {
            Target.Translate(h, 0, v);
        }
        
        
        
        DeltaTime += (Time.unscaledDeltaTime - DeltaTime) * 0.1f;
        float fps = 1.0f / DeltaTime;
        
        workerCountText.text = $"JobWorkerCount:{JobsUtility.JobWorkerCount} ,fps:{Mathf.Round(fps)}";


        if (Input.GetKeyUp(KeyCode.Q))
        {
            
            Vector3[] posInfo = new []{Target.position};
            var count = GetNearestAgents(posInfo);
            Debug.Log($"count:{count}");
        }

    }
    private void LateUpdate()
    {
        if (simulation.TryComplete())
        {
            if (!simulation.TryGetFirst(-1, out IAgentProvider agentProvider, true))
            {
                return;
            }
            
            NativeArray<AgentData> tempAgentDatas = new NativeArray<AgentData>(agentProvider.outputAgents, Allocator.TempJob);

            foreach (var outputAgent in agentProvider.outputAgents)
            {
                
#if UNITY_EDITOR
             
#endif
            }
            
            SetRendererData(tempAgentDatas);
            StartRendererNodeJob(m_AllRendererNodes);
            m_brgContainer.UploadGpuData(kNumInstances);

        }
    }
    

    void StartRendererNodeJob(NativeArray<RendererNode> RendererNodes)
    {
        
        int totalGpuBufferSize;
        int alignedWindowSize;
        NativeArray<float4> _sysmemBuffer = m_brgContainer.GetSysmemBuffer(out totalGpuBufferSize, out alignedWindowSize);
        var _maxInstancePerWindow = alignedWindowSize / kGpuItemSize;
        var _windowSizeInFloat4 = alignedWindowSize / 16;


        RendererNodeJob job = new RendererNodeJob
        {
            _sysmemBuffer = _sysmemBuffer,
            _maxInstancePerWindow = _maxInstancePerWindow,
            _windowSizeInFloat4 = _windowSizeInFloat4,
            Nodes = RendererNodes,
        };
        
        job.Schedule(RendererNodes.Length , 64).Complete();
        
    }


    [BurstCompile]
    private struct RendererNodeJob : IJobParallelFor
    {
        [WriteOnly]
        [NativeDisableParallelForRestriction]
        public NativeArray<float4> _sysmemBuffer;
        
        public int _maxInstancePerWindow;
        public int _windowSizeInFloat4;
        public NativeArray<RendererNode> Nodes;
        
        public void Execute(int index)
        {
            var node = Nodes[index];
            
            int i;
            int windowId = System.Math.DivRem(index, _maxInstancePerWindow, out i);
            
            int windowOffsetInFloat4 = windowId * _windowSizeInFloat4;
            
            
            Vector3 bpos = node.position;
            
            float3x3 rot = float3x3(node.rotation);

            // compute the new current frame matrix
            _sysmemBuffer[(windowOffsetInFloat4 + i * 3 + 0)] = new float4(rot.c0.x, rot.c0.y, rot.c0.z, rot.c1.x);
            _sysmemBuffer[(windowOffsetInFloat4 + i * 3 + 1)] = new float4(rot.c1.y, rot.c1.z, rot.c2.x, rot.c2.y);
            _sysmemBuffer[(windowOffsetInFloat4 + i * 3 + 2)] = new float4(rot.c2.z, bpos.x, bpos.y, bpos.z);

            // compute the new inverse matrix
            _sysmemBuffer[(windowOffsetInFloat4 + _maxInstancePerWindow * 3 * 1 + i * 3 + 0)] = new float4(rot.c0.x, rot.c1.x, rot.c2.x, rot.c0.y);
            _sysmemBuffer[(windowOffsetInFloat4 + _maxInstancePerWindow * 3 * 1 + i * 3 + 1)] = new float4(rot.c1.y, rot.c2.y, rot.c0.z, rot.c1.z);
            _sysmemBuffer[(windowOffsetInFloat4 + _maxInstancePerWindow * 3 * 1 + i * 3 + 2)] = new float4(rot.c2.z, -bpos.x, -bpos.y, -bpos.z);

            // update colors
            
            float4 color = new float4(1, 1, 1, 1);
            _sysmemBuffer[windowOffsetInFloat4 + _maxInstancePerWindow * 3 * 2 + i] = color;
        }
    }



    internal void SetRendererData(NativeArray<AgentData> tempAgentDatas)
    {
        
        var job = new SyncRendererNodeTransformJob
        {
            AgentDataArr = tempAgentDatas,
            Nodes = m_AllRendererNodes,
            AllPos = allPos,
        };
        job.Schedule(tempAgentDatas.Length, 64).Complete();
    }

    [BurstCompile]
    private struct SyncRendererNodeTransformJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<AgentData> AgentDataArr;
        [NativeDisableParallelForRestriction]
        public NativeArray<RendererNode> Nodes;
        
        [NativeDisableParallelForRestriction]
        public NativeArray<float3> AllPos;
        
        

        public void Execute(int index)
        {
            var agentDt = AgentDataArr[index];

            // var node = Nodes[agentDt.rendererIndex];
            var node = Nodes[index];
            node.position = agentDt.worldPosition;
            node.rotation = agentDt.worldQuaternion;
            node.animClipId = agentDt.animationIndex;
            // Nodes[agentDt.rendererIndex] = node;
            Nodes[index] = node;
            AllPos[index] = node.position;

            // Debug.Log($"index:{index} {agentDt.worldPosition} , target:{agentDt.targetPosition}");
        }
    }
    
    private void OnDestroy()
    {
        if ( m_brgContainer != null )
            m_brgContainer.Shutdown();
            
    }
    
    
    /// <summary>
    /// 给定多个point,一次查询各个点最近的Agent
    /// </summary>
    /// <param name="points"></param>
    /// <param name="nearestAgents"></param>
    /// <returns></returns>
    public int GetNearestAgents(Vector3[] points)
    {

        if (!m_GirdInitialized)
        {        
            m_GridSearch.clean();
            m_GridSearch.initGrid(allPos);
        }
        else
        {
            m_GridSearch.updatePositions(allPos);
        }
        
        // var indexes = m_GridSearch.searchClosestPoint(points);


        var indexes = m_GridSearch.searchWithin(points, 0.5f, 50);
        
        
        int resultCount = 0;
        for (int i = 0; i < indexes.Length; i++)
        {
            var index = indexes[i];
            if (index == -1) continue;
            var searchAgent = agents[index];
            if (searchAgent == null) continue;
            // nearestAgents[resultCount++] = searchAgent;
            resultCount++;
        }
        return resultCount;
    }

}

public class RVOAgent : Agent
{
    public Transform CachedTransform;
    public bool IsMoving;
    public int rendererIndex;
}