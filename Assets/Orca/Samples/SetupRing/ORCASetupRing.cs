
using Nebukam.Common;
#if UNITY_EDITOR
using Nebukam.Common.Editor;
#endif
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using static Unity.Mathematics.math;
using Random = UnityEngine.Random;

namespace Nebukam.ORCA
{
    public class ORCASetupRing : MonoBehaviour
    {

        private AgentGroup<Agent> agents;
        private ObstacleGroup obstacles;
        private ObstacleGroup dynObstacles;
        private RaycastGroup raycasts;
        private ORCA simulation;

        [Header("Settings")]
        public int seed = 12345;
        public Texture2D shapeTex;
        public float shapeSize = 1f;
        public TextMeshProUGUI workerCountText;
        public TextMeshProUGUI agentCountText;
        public Transform target;
        public GameObject prefab;
        public AxisPair axis = AxisPair.XY;

        [Header("Agents")]
        public int agentCount = 50;
        public float maxAgentRadius = 2f;
        public bool uniqueRadius = false;
        public float maxSpeed = 1f;
        public float minSpeed = 1f;

        [Header("Obstacles")]
        public int obstacleCount = 100;
        public int dynObstacleCount = 20;
        public float maxObstacleRadius = 2f;
        public int minObstacleEdgeCount = 2;
        public int maxObstacleEdgeCount = 2;

        [Header("Debug")]
        Color staticObstacleColor = Color.red;
        Color dynObstacleColor = Color.yellow;

        [Header("Raycasts")]
        public int raycastCount = 50;
        public float raycastDistance = 10f;
        private float4[] m_Colors;
        public float4[] AgentColors => m_Colors;
#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (agents != null)
            {
                for (int i = 0; i < agents.Count; i++)
                {
                    var agent = agents[i];
                    var agentPos = agent.pos;
                    //Agent body
                    Color bodyColor = i % 3 == 0 ? Color.red : Color.green;

                    if (axis == AxisPair.XY)
                    {
                        Draw.Circle2D(agentPos, agent.radius, bodyColor, 12);
                        Draw.Circle2D(agentPos, agent.radiusObst, Color.cyan.A(0.15f), 12);
                    }
                    else
                    {
                        Draw.Circle(agentPos, agent.radius, bodyColor, 12);
                        Draw.Circle(agentPos, agent.radiusObst, Color.cyan.A(0.15f), 12);

                    }
                    //Agent simulated velocity (ORCA compliant)
                    Draw.Line(agentPos, agentPos + (normalize(agent.velocity) * agent.radius), Color.green);
                    //Agent goal vector
                    Draw.Line(agentPos, agentPos + (normalize(agent.prefVelocity) * agent.radius), Color.grey);
                }
            }

            if (obstacles != null)
            {
                //Draw static obstacles
                Obstacle o;
                int oCount = obstacles.Count, subCount;
                for (int i = 0; i < oCount; i++)
                {
                    o = obstacles[i];
                    subCount = o.Count;

                    //Draw each segment
                    for (int j = 1, count = o.Count; j < count; j++)
                    {
                        Draw.Line(o[j - 1].pos, o[j].pos, staticObstacleColor);
                    }
                    //Draw closing segment (simulation consider 2+ segments to be closed.)
                    if (!o.edge)
                        Draw.Line(o[subCount - 1].pos, o[0].pos, staticObstacleColor);
                }

                if (dynObstacles != null)
                {
                    float delta = Time.deltaTime * 50f;

                    //Draw dynamic obstacles
                    oCount = dynObstacles.Count;
                    for (int i = 0; i < oCount; i++)
                    {
                        o = dynObstacles[i];
                        subCount = o.Count;

                        //Draw each segment
                        for (int j = 1, count = o.Count; j < count; j++)
                        {
                            Draw.Line(o[j - 1].pos, o[j].pos, dynObstacleColor);
                        }
                        //Draw closing segment (simulation consider 2+ segments to be closed.)
                        if (!o.edge)
                            Draw.Line(o[subCount - 1].pos, o[0].pos, dynObstacleColor);

                    }
                }
            }
            if (raycasts != null)
            {
                Raycast r;
                float rad = 0.2f;
                for (int i = 0, count = raycasts.Count; i < count; i++)
                {
                    r = raycasts[i] as Raycast;
                    Draw.Circle2D(r.pos, rad, Color.white, 3);
                    if (r.anyHit)
                    {
                        Draw.Line(r.pos, r.pos + r.dir * r.distance, Color.white.A(0.5f));

                        if (axis == AxisPair.XY)
                        {
                            if (r.obstacleHit != null) { Draw.Circle2D(r.obstacleHitLocation, rad, Color.cyan, 3); }
                            if (r.agentHit != null) { Draw.Circle2D(r.agentHitLocation, rad, Color.magenta, 3); }
                        }
                        else
                        {
                            if (r.obstacleHit != null) { Draw.Circle(r.obstacleHitLocation, rad, Color.cyan, 3); }
                            if (r.agentHit != null) { Draw.Circle(r.agentHitLocation, rad, Color.magenta, 3); }
                        }

                    }
                    else
                    {
                        Draw.Line(r.pos, r.pos + r.dir * r.distance, Color.blue.A(0.5f));
                    }
                }
            }
        }
#endif
        private void Start()
        {
            Application.targetFrameRate = -1;
            agents = new AgentGroup<Agent>();
            obstacles = new ObstacleGroup();
            dynObstacles = new ObstacleGroup();
            raycasts = new RaycastGroup();
            m_Colors = new float4[agentCount];
            simulation = new ORCA();
            simulation.plane = axis;
            simulation.agents = agents;
            simulation.staticObstacles = obstacles;
            simulation.dynamicObstacles = dynObstacles;
            simulation.raycasts = raycasts;

            agentCountText.text = $"Agent Count:{agentCount}";
            workerCountText.text = $"JobWorkerCount:{JobsUtility.JobWorkerCount}";

            float radius = ((agentCount * (maxAgentRadius * 2f)) / PI) * 0.02f;
            Random.InitState(seed);

            #region create obstacles

            float dirRange = 2f;
            List<float3> vList = new List<float3>();
            Obstacle o;
            for (int i = 0; i < obstacleCount; i++)
            {
                int vCount = Random.Range(minObstacleEdgeCount, maxObstacleEdgeCount);
                vList.Clear();
                vList.Capacity = vCount;

                //build branch-like obstacle

                float3 start = float3(Random.Range(-radius, radius), Random.Range(-radius, radius), 0f),
                    pt = start,
                    dir = float3(Random.Range(-dirRange, dirRange), Random.Range(-dirRange, dirRange), 0f);

                if (axis == AxisPair.XZ)
                {
                    pt = start = float3(start.x, 0f, start.y);
                    dir = float3(dir.x, 0f, dir.y);
                }

                vList.Add(start);
                vCount--;

                for (int j = 0; j < vCount; j++)
                {
                    dir = normalize(Maths.RotateAroundPivot(dir, float3(0f),
                        axis == AxisPair.XY ? float3(0f, 0f, (math.PI) / vCount) : float3(0f, (math.PI) / vCount, 0f)));

                    pt = pt + dir * Random.Range(1f, maxObstacleRadius);
                    vList.Add(pt);
                }

                //if (vCount != 2) { vList.Add(start); }

                o = obstacles.Add(vList, axis == AxisPair.XZ);
            }

            #endregion

            Random.InitState(seed + 10);

            #region create dyanmic obstacles

            for (int i = 0; i < dynObstacleCount; i++)
            {
                int vCount = Random.Range(minObstacleEdgeCount, maxObstacleEdgeCount);
                vList.Clear();
                vList.Capacity = vCount;

                //build branch-like obstacle

                float3 start = float3(Random.Range(-radius, radius), Random.Range(-radius, radius), 0f),
                    pt = start,
                    dir = float3(Random.Range(-dirRange, dirRange), Random.Range(-dirRange, dirRange), 0f);

                if (axis == AxisPair.XZ)
                {
                    pt = start = float3(start.x, 0f, start.y);
                    dir = float3(dir.x, 0f, dir.y);
                }

                vList.Add(start);
                vCount--;

                for (int j = 0; j < vCount; j++)
                {
                    dir = normalize(Maths.RotateAroundPivot(dir, float3(0f),
                        axis == AxisPair.XY ? float3(0f, 0f, (math.PI) / vCount) : float3(0f, (math.PI) / vCount, 0f)));
                    pt = pt + dir * Random.Range(1f, maxObstacleRadius);
                    vList.Add(pt);
                }

                //if (vCount != 2) { vList.Add(start); }

                dynObstacles.Add(vList, axis == AxisPair.XZ);
            }

            #endregion

            #region create agents

            var colors = shapeTex.GetPixels32();
            float radius2 = (((agentCount - colors.Length) * (maxAgentRadius * 2f)) / PI) * 0.05f;
            IAgent a;

            float angleInc = (PI * 2) / agentCount;
            float angleInc2 = (PI * 2) / (agentCount - colors.Length);
            float halfWidth = shapeTex.width * 0.5f;
            float halfHeight = shapeTex.height * 0.5f;
            int freeIdx = 0;
            for (int i = 0; i < agentCount; i++)
            {
                float2 pos = float2(sin(angleInc * i), cos(angleInc * i)) * (Random.Range(radius * 0.5f, radius));

                if (axis == AxisPair.XY)
                {
                    a = agents.Add((float3)transform.position + float3(pos.x, pos.y, 0f)) as IAgent;
                }
                else
                {
                    a = agents.Add((float3)transform.position + float3(pos.x, 0f, pos.y)) as IAgent;
                }

                a.radius = uniqueRadius ? maxAgentRadius : 0.5f + Random.value * maxAgentRadius;
                a.radiusObst = a.radius;// + Random.value * maxAgentRadius;
                a.prefVelocity = float3(0f);
                a.maxSpeed = maxSpeed;
                a.timeHorizon = 5;
                a.maxNeighbors = 10;
                a.neighborDist = 10;
                a.timeHorizonObst = 5;
                if (i < colors.Length)
                {
                    a.targetPosition = new float3(i % shapeTex.width - halfWidth, 0, i / shapeTex.width - halfHeight);
                    var col = colors[i];
                    m_Colors[i] = new float4(col.r / 255f, col.g / 255f, col.b / 255f, 1f);
                    a.radius = 0.2f;
                    a.radiusObst = a.radius;// + Random.value * maxAgentRadius;
                }
                else
                {
                    m_Colors[i] = new float4(0.7735849f, 0.427747f, 0f, 1f);
                    pos = float2(sin(angleInc2 * freeIdx), cos(angleInc2 * freeIdx)) * (Random.Range(radius2 * 0.5f, radius2));
                    a.targetPosition = (float3)transform.position + float3(pos.x, 0f, pos.y);
                    freeIdx++;
                }
            }

            #endregion

            #region create raycasts

            Raycast r;

            for (int i = 0; i < raycastCount; i++)
            {
                if (axis == AxisPair.XY)
                {
                    r = raycasts.Add(float3(Random.Range(-radius, radius), Random.Range(-radius, radius), 0f)) as Raycast;
                    r.dir = normalize(float3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0f));
                }
                else
                {
                    r = raycasts.Add(float3(Random.Range(-radius, radius), 0f, Random.Range(-radius, radius))) as Raycast;
                    r.dir = normalize(float3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)));
                }

                r.distance = raycastDistance;
            }

            #endregion
        }

        private void Update()
        {
            //Schedule the simulation job. 
            simulation.Schedule(Time.deltaTime);
        }

        private void LateUpdate()
        {
            //Attempt to complete and apply the simulation results, only if the job is done.
            //TryComplete will not force job completion.
            if (simulation.TryComplete())
            {

                //Move dynamic obstacles randomly
                int oCount = dynObstacles.Count;
                float delta = Time.deltaTime * 50f;

                for (int i = 0; i < oCount; i++)
                    dynObstacles[i].Offset(float3(Random.Range(-delta, delta), Random.Range(-delta, delta), 0f));

            }
        }

        private void OnApplicationQuit()
        {
            //Make sure to clean-up the jobs
            simulation.DisposeAll();
        }

        public AgentGroup<Agent> GetAgents()
        {
            return agents;
        }
    }
}