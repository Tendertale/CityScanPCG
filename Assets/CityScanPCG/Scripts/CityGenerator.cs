using System.Collections.Generic;
using System.Threading;
using BlockGeneration;
using GraphModel;
using MeshGeneration;
using RoadGeneration;
using BlockDivision;
using Services;
using UnityEngine;

public class CityGenerator : MonoBehaviour
{
    private Graph roadGraph;
    private List<BlockNode> blockNodes;
    private List<Block> blocks;
    private List<Block> thinnedBlocks;
    private List<Block> lots;
    private System.Random rand;

    private List<Block> concaveBlocks;
    private List<Block> convexBlocks;
    private List<BlockMesh> blockMeshes;
    private List<BlockMesh> lotMeshes;
    private List<BoundingRectangle> boundingRectangles;
    private float blockHeight = 0.02f;

    public List<Block> GetBlocks() => blocks;
    public Graph GetRoadGraph() => roadGraph;
    [Header("Seed and Size")]
    public int mapSize = 300; // 用户设定的默认值
    public int seed = 13;

    [Header("Major Road generation")]
    [Range(0, 20)]
    public int maxDegreeInCurves = 10;
    [Range(0.03f, 0.1f)]
    public float branchingProbability = 0.075f;

    [Header("Minor Road generation")]
    [Range(0.02f, 0.2f)]
    public float crossingDeletionProbability = 0.1f;

    [Header("Maximum Number of Roads")]
    public int maxMajorRoad = 2000;
    public int maxMinorRoad = 10000;

    [Header("Thickness of Roads")]
    [Range(0.1f, 2.5f)]
    public float majorThickness = 2.5f;
    [Range(0.1f, 2.5f)]
    public float minorThickness = 0.9f;

    [Header("Sidewalk generation")]
    [Range(0.1f, 1f)]
    public float sidewalkThickness = 0.5f;

    [Header("Building generation")]
    public float minBuildHeight = 4f;
    public float maxBuildHeight = 45f;

    [Header("Gizmos")]
    public bool drawRoadNodes;
    public bool drawRoads = true;
    public bool drawBlockNodes;
    public bool drawBlocks = true;
    public bool drawThinnedBlocks;
    public bool drawConvexBlocks;
    public bool drawConcaveBlocks;
    public bool drawTriangulatedMeshes;
    public bool drawBoundingBoxes;
    public bool drawLots = true;

    private bool genReady;
    private bool genDone;
    public bool GenerationComplete => genDone;
    void Start()
    {
        rand = new System.Random(seed);
        InitializeRandomParameters(); // 随机化参数

        roadGraph = new Graph();
        lots = new List<Block>();

        Thread t = new Thread(ThreadProc);
        t.Start();
    }

    void InitializeRandomParameters()
    {
        // 在用户设定值的基础上随机波动 ±10%
        mapSize = RandomizeInt(mapSize, 0.1f);
        maxDegreeInCurves = RandomizeInt(maxDegreeInCurves, 0.1f, 0, 20); // 限制在 [0, 20]
        branchingProbability = RandomizeFloat(branchingProbability, 0.1f, 0.03f, 0.1f); // 限制在 [0.03, 0.1]
        crossingDeletionProbability = RandomizeFloat(crossingDeletionProbability, 0.1f, 0.02f, 0.2f); // 限制在 [0.02, 0.2]
        maxMajorRoad = RandomizeInt(maxMajorRoad, 0.1f);
        maxMinorRoad = RandomizeInt(maxMinorRoad, 0.1f);
        majorThickness = RandomizeFloat(majorThickness, 0.1f, 0.1f, 2.5f); // 限制在 [0.1, 2.5]
        minorThickness = RandomizeFloat(minorThickness, 0.1f, 0.1f, 2.5f); // 限制在 [0.1, 2.5]
        sidewalkThickness = RandomizeFloat(sidewalkThickness, 0.1f, 0.1f, 1f); // 限制在 [0.1, 1.0]
        minBuildHeight = RandomizeFloat(minBuildHeight, 0.1f);
        maxBuildHeight = RandomizeFloat(maxBuildHeight, 0.1f);

        // 确保 minBuildHeight < maxBuildHeight
        if (minBuildHeight >= maxBuildHeight)
        {
            float temp = minBuildHeight;
            minBuildHeight = maxBuildHeight;
            maxBuildHeight = temp;
        }

        // 输出随机化后的参数以便调试
        Debug.Log($"Randomized Parameters - Seed: {seed}, mapSize: {mapSize}, maxDegreeInCurves: {maxDegreeInCurves}, " +
                  $"branchingProbability: {branchingProbability}, crossingDeletionProbability: {crossingDeletionProbability}, " +
                  $"maxMajorRoad: {maxMajorRoad}, maxMinorRoad: {maxMinorRoad}, majorThickness: {majorThickness}, " +
                  $"minorThickness: {minorThickness}, sidewalkThickness: {sidewalkThickness}, " +
                  $"minBuildHeight: {minBuildHeight}, maxBuildHeight: {maxBuildHeight}");
    }

    // 随机化整数参数，限制波动范围为 ±percent
    private int RandomizeInt(int baseValue, float percent, int minValue = int.MinValue, int maxValue = int.MaxValue)
    {
        float range = baseValue * percent;
        int min = Mathf.Max((int)(baseValue - range), minValue);
        int max = Mathf.Min((int)(baseValue + range), maxValue);
        return rand.Next(min, max + 1);
    }

    // 随机化浮点数参数，限制波动范围为 ±percent
    private float RandomizeFloat(float baseValue, float percent, float minValue = float.MinValue, float maxValue = float.MaxValue)
    {
        float range = baseValue * percent;
        float min = Mathf.Max(baseValue - range, minValue);
        float max = Mathf.Min(baseValue + range, maxValue);
        return (float)(rand.NextDouble() * (max - min) + min);
    }

    void Update()
    {
        if (genReady && !genDone)
        {
            genDone = true;
            GenerateGameObjects();
        }
    }

    private void ThreadProc()
    {
        System.Diagnostics.Stopwatch mainSw = System.Diagnostics.Stopwatch.StartNew();
        System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();

        MajorGenerator majorGen = new MajorGenerator(rand, mapSize, maxMajorRoad, maxDegreeInCurves, branchingProbability, roadGraph);
        majorGen.Run();
        MinorGenerator minorGen = new MinorGenerator(rand, mapSize, maxMinorRoad, crossingDeletionProbability, roadGraph, majorGen.GetRoadSegments());
        minorGen.Run();

        sw.Stop();
        Debug.Log("Road generation time taken: " + sw.Elapsed.TotalMilliseconds + " ms");
        Debug.Log(majorGen.GetRoadSegments().Count + " major road generated");
        Debug.Log(minorGen.GetRoadSegments().Count + " minor road generated");

        BlockGenerator blockGen = new BlockGenerator(roadGraph, mapSize, majorThickness, minorThickness, blockHeight);
        blockGen.Generate();
        blockNodes = blockGen.BlockNodes;
        blocks = blockGen.Blocks;
        Debug.Log(blockGen.Blocks.Count + " block generated");

        blockGen.ThickenBlocks(sidewalkThickness);
        thinnedBlocks = blockGen.ThinnedBlocks;
        Debug.Log("Sidewalk generation completed");

        sw = System.Diagnostics.Stopwatch.StartNew();

        BlockDivider blockDiv = new BlockDivider(rand, thinnedBlocks, lots);
        blockDiv.DivideBlocks();
        blockDiv.SetBuildingHeights(minBuildHeight, maxBuildHeight, blockHeight, mapSize);
        boundingRectangles = blockDiv.BoundingRectangles;

        sw.Stop();
        Debug.Log("Lot generation time taken: " + sw.Elapsed.TotalMilliseconds + " ms");
        Debug.Log(lots.Count + " lot generated");

        MeshGenerator blockMeshGen = new MeshGenerator(blocks, blockHeight);
        blockMeshGen.GenerateMeshes();
        blockMeshes = blockMeshGen.BlockMeshes;

        MeshGenerator lotMeshGen = new MeshGenerator(lots, blockHeight + blockHeight / 3);
        lotMeshGen.GenerateMeshes();

        convexBlocks = lotMeshGen.ConvexBlocks;
        concaveBlocks = lotMeshGen.ConcaveBlocks;
        lotMeshes = lotMeshGen.BlockMeshes;

        mainSw.Stop();
        Debug.Log("City generation time taken: " + mainSw.Elapsed.TotalMilliseconds + " ms");

        genReady = true;
    }

    private void GenerateGameObjects()
    {
        var separator = new GameObject { name = "===========" };

        var roadPlane = new GameObject { name = "Road Plane" };
        roadPlane.AddComponent<MeshFilter>().mesh = MeshCreateService.GenerateRoadMesh(mapSize);
        roadPlane.AddComponent<MeshRenderer>().material = Resources.Load<Material>("Material/RoadMaterial");
        roadPlane.AddComponent<MeshCollider>();
        var blockContainer = new GameObject { name = "Block Container" };
        Material blockMaterial = Resources.Load<Material>("Material/BlockMaterial");
        Material parkMaterial = Resources.Load<Material>("Material/BlockGreenMaterial");

        for (int i = 0; i < blockMeshes.Count; i++)
        {
            var block = new GameObject { name = "Block" + i.ToString(), transform = { parent = blockContainer.transform } };
            block.AddComponent<MeshFilter>().mesh = MeshCreateService.GenerateBlockMesh(blockMeshes[i]);
            block.AddComponent<MeshRenderer>().material = blockMeshes[i].Block.IsPark ? parkMaterial : blockMaterial;
            block.AddComponent<MeshCollider>();
        }

        var lotContainer = new GameObject { name = "Lot Container" };
        Material lotMaterial = Resources.Load<Material>("Material/BlockMaterial");

        for (int i = 0; i < lotMeshes.Count; i++)
        {
            var lot = new GameObject { name = "Lot" + i.ToString(), transform = { parent = lotContainer.transform } };
            lot.AddComponent<MeshFilter>().mesh = MeshCreateService.GenerateBlockMesh(lotMeshes[i]);
            lot.AddComponent<MeshRenderer>().material = lotMeshes[i].Block.IsPark ? parkMaterial : lotMaterial;
            lot.AddComponent<MeshCollider>();
        }
    }

    private void OnDrawGizmos()
    {
        if (roadGraph == null) return;

        if (drawRoads)
        {
            GizmoService.DrawEdges(roadGraph.MajorEdges, Color.white);
            GizmoService.DrawEdges(roadGraph.MinorEdges, Color.black);
        }

        if (drawRoadNodes)
        {
            GizmoService.DrawNodes(roadGraph.MajorNodes, Color.white, 2f);
            GizmoService.DrawNodes(roadGraph.MinorNodes, Color.black, 1f);
        }

        if (drawBlockNodes)
        {
            GizmoService.DrawBlockNodes(blockNodes, Color.red, 0.4f);
        }

        if (drawBlocks)
        {
            GizmoService.DrawBlocks(blocks, new Color(0.7f, 0.4f, 0.4f));
        }

        if (drawThinnedBlocks)
        {
            GizmoService.DrawBlocks(thinnedBlocks, new Color(0.7f, 0.4f, 0.4f));
        }

        if (drawConvexBlocks && genDone)
        {
            GizmoService.DrawBlocks(convexBlocks, new Color(0.2f, 0.7f, 0.7f));
        }

        if (drawConcaveBlocks && genDone)
        {
            GizmoService.DrawBlocks(concaveBlocks, new Color(0.2f, 0.7f, 0.2f));
        }

        if (drawTriangulatedMeshes && genDone)
        {
            GizmoService.DrawBlockMeshes(blockMeshes, new Color(.8f, .8f, .8f));
        }

        if (drawBoundingBoxes && genDone)
        {
            List<Edge> cutEdges = new List<Edge>();
            foreach (var boundingBox in boundingRectangles)
            {
                GizmoService.DrawEdges(boundingBox.Edges, Color.white);
                cutEdges.Add(boundingBox.GetCutEdge());
            }
            GizmoService.DrawEdges(cutEdges, Color.yellow);
        }

        if (drawLots)
        {
            GizmoService.DrawBlocks(lots, new Color(0.2f, 0.7f, 0.7f));
        }
    }
}