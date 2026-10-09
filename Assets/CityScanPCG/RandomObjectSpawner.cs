using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GraphModel;
using BlockGeneration;
public class RandomObjectSpawner : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject[] carPrefabs;
    public GameObject[] polePrefabs;
    public GameObject benchPrefab;

    [Header("Car Settings")]
    [Range(0.1f, 0.6f)] public float carDensity = 0.3f;
    public int minCars = 10;
    public int maxCars = 120;
    public float carMinDistanceToBlock = 0.2f;
    public float carMaxDistanceToBlock = 1.5f;

    [Header("Utility Pole Settings")]
    [Range(0.1f, 0.4f)] public float poleDensity = 0.2f;
    public int minPoles = 5;
    public int maxPoles = 60;
    public float poleOffsetFromRoadCenter = 2.0f;
    public float poleMinDistanceToBlock = 0.05f;
    public float poleMaxDistanceToBlock = 0.4f;
    public float poleMinDistanceToGardenEdge = 0.1f;

    [Header("Bench Settings")]
    public int minBenchesPerGarden = 1;
    public int maxBenchesPerGarden = 3;

    [Header("Random Seed")]
    public int randomSeed = 42;

    [Header("References")]
    public CityGenerator cityGenerator;
    private GameObject roadPlane;

    private List<Vector3> roadPositions = new List<Vector3>();
    private List<Vector3> roadEdgePositions = new List<Vector3>();
    private System.Random rand;
    private float mapSize;
    private float edgeBuffer = 20f;
    private bool hasSpawned = false;

    void Start()
    {
        rand = new System.Random(randomSeed);
        if (cityGenerator == null)
        {
            Debug.LogError("请检查 CityGenerator 是否正确分配！");
            return;
        }

        mapSize = cityGenerator.mapSize;
        StartCoroutine(WaitForCityGeneration()); // 开始等待城市生成
    }

    IEnumerator WaitForCityGeneration()
    {
        // 等待城市生成完成（检查 blocks 是否有数据）
        while (cityGenerator.GetBlocks() == null || cityGenerator.GetBlocks().Count == 0)
        {
            yield return new WaitForSeconds(1f); // 每秒检查一次
        }

        // 额外等待，确保 RoadPlane 已生成
        yield return new WaitForSeconds(5f);

        // 检查 RoadPlane 是否存在
        roadPlane = GameObject.Find("Road Plane");
        while (roadPlane == null)
        {
            Debug.Log("等待 RoadPlane 生成...");
            yield return new WaitForSeconds(2f); // 每2秒检查一次
            roadPlane = GameObject.Find("Road Plane");
        }

        // RoadPlane 存在后生成物体
        SpawnObjects();
        hasSpawned = true;
    }

    void SpawnObjects()
    {
        ExtractRoadPositions();
        SpawnCars();
        SpawnUtilityPoles();
        SpawnBenches();
    }

    void ExtractRoadPositions()
    {
        Graph graph = cityGenerator.GetRoadGraph();
        float segmentLength = 5.0f;

        float halfMapSize = mapSize / 2f;
        float minX = -halfMapSize + edgeBuffer;
        float maxX = halfMapSize - edgeBuffer;
        float minZ = -halfMapSize + edgeBuffer;
        float maxZ = halfMapSize - edgeBuffer;

        foreach (var edge in graph.MajorEdges)
        {
            Vector3 fromPos = new Vector3(edge.NodeA.X, 0, edge.NodeA.Y);
            Vector3 toPos = new Vector3(edge.NodeB.X, 0, edge.NodeB.Y);
            float distance = Vector3.Distance(fromPos, toPos);
            int segments = Mathf.FloorToInt(distance / segmentLength);

            for (int i = 0; i <= segments; i++)
            {
                Vector3 pos = Vector3.Lerp(fromPos, toPos, (float)i / segments);
                if (pos.x >= minX && pos.x <= maxX && pos.z >= minZ && pos.z <= maxZ)
                {
                    roadPositions.Add(pos);
                    Vector3 direction = (toPos - fromPos).normalized;
                    Vector3 perpendicular = Vector3.Cross(direction, Vector3.up).normalized;
                    Vector3 edgePos1 = pos + perpendicular * poleOffsetFromRoadCenter;
                    Vector3 edgePos2 = pos - perpendicular * poleOffsetFromRoadCenter;
                    if (edgePos1.x >= minX && edgePos1.x <= maxX && edgePos1.z >= minZ && edgePos1.z <= maxZ)
                        roadEdgePositions.Add(edgePos1);
                    if (edgePos2.x >= minX && edgePos2.x <= maxX && edgePos2.z >= minZ && edgePos2.z <= maxZ)
                        roadEdgePositions.Add(edgePos2);
                }
            }
        }

        foreach (var edge in graph.MinorEdges)
        {
            Vector3 fromPos = new Vector3(edge.NodeA.X, 0, edge.NodeA.Y);
            Vector3 toPos = new Vector3(edge.NodeB.X, 0, edge.NodeB.Y);
            float distance = Vector3.Distance(fromPos, toPos);
            int segments = Mathf.FloorToInt(distance / segmentLength);

            for (int i = 0; i <= segments; i++)
            {
                Vector3 pos = Vector3.Lerp(fromPos, toPos, (float)i / segments);
                if (pos.x >= minX && pos.x <= maxX && pos.z >= minZ && pos.z <= maxZ)
                {
                    roadPositions.Add(pos);
                    Vector3 direction = (toPos - fromPos).normalized;
                    Vector3 perpendicular = Vector3.Cross(direction, Vector3.up).normalized;
                    Vector3 edgePos1 = pos + perpendicular * poleOffsetFromRoadCenter;
                    Vector3 edgePos2 = pos - perpendicular * poleOffsetFromRoadCenter;
                    if (edgePos1.x >= minX && edgePos1.x <= maxX && edgePos1.z >= minZ && edgePos1.z <= maxZ)
                        roadEdgePositions.Add(edgePos1);
                    if (edgePos2.x >= minX && edgePos2.x <= maxX && edgePos2.z >= minZ && edgePos2.z <= maxZ)
                        roadEdgePositions.Add(edgePos2);
                }
            }
        }

        FilterPositionsNearBlocks();
    }

    void FilterPositionsNearBlocks()
    {
        var blocks = cityGenerator.GetBlocks();
        List<Vector3> filteredRoadPositions = new List<Vector3>();
        List<Vector3> filteredEdgePositions = new List<Vector3>();

        foreach (var pos in roadPositions)
        {
            bool isValid = true;
            foreach (var block in blocks)
            {
                float distance = GetMinDistanceToBlock(pos, block);
                if (distance < carMinDistanceToBlock || distance > carMaxDistanceToBlock)
                {
                    isValid = false;
                    break;
                }
            }
            if (isValid) filteredRoadPositions.Add(pos);
        }

        foreach (var pos in roadEdgePositions)
        {
            bool isValid = true;
            foreach (var block in blocks)
            {
                float distance = GetMinDistanceToBlock(pos, block);
                if (block.IsPark)
                {
                    if (IsPointInsideBlock(pos, block) && distance < poleMinDistanceToGardenEdge)
                    {
                        isValid = false;
                        break;
                    }
                }
                else
                {
                    if (distance < poleMinDistanceToBlock || distance > poleMaxDistanceToBlock)
                    {
                        isValid = false;
                        break;
                    }
                }
            }
            if (isValid) filteredEdgePositions.Add(pos);
        }

        roadPositions = filteredRoadPositions;
        roadEdgePositions = filteredEdgePositions;
    }

    float GetMinDistanceToBlock(Vector3 pos, Block block)
    {
        float minDistance = float.MaxValue;
        for (int i = 0; i < block.Nodes.Count; i++)
        {
            Vector3 a = new Vector3(block.Nodes[i].X, 0, block.Nodes[i].Y);
            Vector3 b = new Vector3(block.Nodes[(i + 1) % block.Nodes.Count].X, 0, block.Nodes[(i + 1) % block.Nodes.Count].Y);
            Vector3 closest = ClosestPointOnLine(a, b, pos);
            float distance = Vector3.Distance(pos, closest);
            if (distance < minDistance) minDistance = distance;
        }
        return minDistance;
    }

    Vector3 ClosestPointOnLine(Vector3 a, Vector3 b, Vector3 p)
    {
        Vector3 ap = p - a;
        Vector3 ab = b - a;
        float t = Vector3.Dot(ap, ab) / Vector3.Dot(ab, ab);
        t = Mathf.Clamp01(t);
        return a + t * ab;
    }

    bool IsPointInsideBlock(Vector3 point, Block block)
    {
        bool inside = false;
        for (int i = 0, j = block.Nodes.Count - 1; i < block.Nodes.Count; j = i++)
        {
            Vector3 vi = new Vector3(block.Nodes[i].X, 0, block.Nodes[i].Y);
            Vector3 vj = new Vector3(block.Nodes[j].X, 0, block.Nodes[j].Y);
            if ((vi.z > point.z) != (vj.z > point.z) &&
                (point.x < vj.x + (vi.x - vj.x) * (point.z - vj.z) / (vi.z - vj.z)))
                inside = !inside;
        }
        return inside;
    }

    void SpawnCars()
    {
        int carCount = Mathf.Clamp((int)(roadPositions.Count * carDensity), minCars, maxCars);
        for (int i = 0; i < carCount; i++)
        {
            Vector3 spawnPos;
            if (roadPositions.Count > 0)
            {
                int index = rand.Next(0, roadPositions.Count);
                spawnPos = roadPositions[index];
                roadPositions.RemoveAt(index);
            }
            else
            {
                spawnPos = new Vector3((float)(rand.NextDouble() * mapSize - mapSize / 2), 0, (float)(rand.NextDouble() * mapSize - mapSize / 2));
            }

            GameObject carPrefab = carPrefabs[rand.Next(0, carPrefabs.Length)];
            GameObject car = Instantiate(carPrefab, spawnPos + Vector3.up * 0.15f, Quaternion.identity, roadPlane.transform);
            car.name = "Car_" + i;
            car.transform.rotation = Quaternion.LookRotation(rand.Next(0, 2) == 0 ? Vector3.forward : Vector3.right);
            Debug.Log($"生成汽车于: {spawnPos}");
        }
    }

    void SpawnUtilityPoles()
    {
        int poleCount = Mathf.Clamp((int)(roadEdgePositions.Count * poleDensity), minPoles, maxPoles);
        for (int i = 0; i < poleCount; i++)
        {
            Vector3 spawnPos;
            if (roadEdgePositions.Count > 0)
            {
                int index = rand.Next(0, roadEdgePositions.Count);
                spawnPos = roadEdgePositions[index];
                roadEdgePositions.RemoveAt(index);
            }
            else
            {
                spawnPos = new Vector3((float)(rand.NextDouble() * mapSize - mapSize / 2), 0, (float)(rand.NextDouble() * mapSize - mapSize / 2));
            }

            GameObject polePrefab = polePrefabs[rand.Next(0, polePrefabs.Length)];
            GameObject pole = Instantiate(polePrefab, spawnPos + Vector3.up * 0.15f, Quaternion.identity, roadPlane.transform);
            pole.name = "Pole_" + i;
            Debug.Log($"生成电线杆于: {spawnPos}");
        }
    }

    void SpawnBenches()
    {
        var gardens = cityGenerator.GetBlocks().FindAll(b => b.IsPark);
        foreach (var garden in gardens)
        {
            int benchCount = rand.Next(minBenchesPerGarden, maxBenchesPerGarden + 1);
            for (int i = 0; i < benchCount; i++)
            {
                Vector3 spawnPos = GetRandomPointInsideBlock(garden);
                if (spawnPos == Vector3.zero)
                {
                    float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
                    foreach (var node in garden.Nodes)
                    {
                        if (node.X < minX) minX = node.X;
                        if (node.X > maxX) maxX = node.X;
                        if (node.Y < minZ) minZ = node.Y;
                        if (node.Y > maxZ) maxZ = node.Y;
                    }
                    spawnPos = new Vector3(
                        (float)(minX + rand.NextDouble() * (maxX - minX)),
                        0,
                        (float)(minZ + rand.NextDouble() * (maxZ - minZ))
                    );
                }

                GameObject bench = Instantiate(benchPrefab, spawnPos + Vector3.up * 0.15f, Quaternion.identity, roadPlane.transform);
                bench.name = "Bench_" + i + "_in_Garden_" + gardens.IndexOf(garden);
                Debug.Log($"生成长椅于: {spawnPos}");
            }
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        foreach (var pos in roadPositions)
        {
            Gizmos.DrawSphere(pos, 0.5f);
        }
        Gizmos.color = Color.red;
        foreach (var pos in roadEdgePositions)
        {
            Gizmos.DrawSphere(pos, 0.5f);
        }
    }

    Vector3 GetRandomPointInsideBlock(Block block)
    {
        float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
        foreach (var node in block.Nodes)
        {
            if (node.X < minX) minX = node.X;
            if (node.X > maxX) maxX = node.X;
            if (node.Y < minZ) minZ = node.Y;
            if (node.Y > maxZ) maxZ = node.Y;
        }

        int attempts = 0;
        while (attempts < 100)
        {
            Vector3 point = new Vector3(
                (float)(minX + rand.NextDouble() * (maxX - minX)),
                0,
                (float)(minZ + rand.NextDouble() * (maxZ - minZ))
            );
            if (IsPointInsideBlock(point, block))
                return point;
            attempts++;
        }
        return Vector3.zero;
    }
}