# CityScanPCG

**English** | [简体中文](README.zh-CN.md)

CityScanPCG is a Unity C# plugin for procedural city-scene generation. It grows a two-level road graph, derives blocks and lots from road geometry, builds extruded building meshes, and can scatter configured props around roads and parks.

## What it does

- **Road network:** rule-based major-road growth with probabilistic branching and curvature, followed by orthogonal minor-road growth. Candidate segments use endpoint snapping, segment-intersection checks, direction constraints, and a maximum node degree of four.
- **Blocks and lots:** road-width offsets produce block outlines; an inset outline reserves sidewalk space. A randomly selected cut line recursively divides lots, with area and aspect-ratio checks.
- **Procedural meshes:** convex polygons use fan triangulation; concave polygons use ear clipping. The generator creates top and side faces, renderers, and colliders. Blocks with more than 10 boundary vertices can be marked as parks.
- **Optional props:** a separate component samples road and park locations for configured vehicle, roadside-prop, and bench prefabs.
- **Debugging:** Inspector parameters, Gizmos, and separate road, lot, and background-city timing logs.

## Technology

- Unity 2023.2 (project verified against version 2023.2.20f1c1)
- C# and UnityEngine
- Graph-based road representation and computational geometry
- Procedural Mesh, System.Random, a background Thread, and Unity coroutines

## How generation works

````mermaid
flowchart LR
    A[Seed and parameters] --> B[Major road growth]
    B --> C[Minor road growth and cleanup]
    C --> D[Road graph]
    D --> E[Offset block boundaries]
    E --> F[Recursive lot subdivision]
    F --> G[Polygon triangulation]
    G --> H[Main-thread Unity objects]
    H --> I[Optional roadside and park props]
````

The road growth is inspired by an extended L-system approach; the code implements explicit growth rules rather than a general-purpose grammar parser. Lot cuts are selected at 30%, 40%, 50%, or 60% along a bounding-rectangle edge. Subdivision stops after at most six iterations, and candidate lots are rejected when the bounding-rectangle area is below 10 Unity units² or its aspect ratio exceeds 4. These are algorithm settings, not measured quality or performance results.

## Quick start

1. Open a Unity 2023.2 project.
2. Copy Assets/CityScanPCG into the project's Assets directory and let Unity import the scripts and materials.
3. Create an empty GameObject and add the CityGenerator component.
4. Enter Play mode. Generated road, block, and lot objects are created at runtime.
5. To add props, add RandomObjectSpawner to a GameObject, assign the CityGenerator, and assign non-empty vehicle and roadside-prefab arrays plus a bench prefab in the Inspector.

When replacing the original CityPCG folder in an existing project, remove that copy before adding this one. Unity asset GUIDs are preserved in this source release.

The prop component expects prefabs to be supplied by the consuming project. Third-party model packs from the original development project are not included in this repository.

## Main settings

| Setting | Default in the inspected demo scene | Meaning |
|---|---:|---|
| mapSize | 300 | Road plane spans from -mapSize to +mapSize on each horizontal axis (600 × 600 Unity units before parameter randomization). |
| seed | 13 | City-generation random seed. |
| maxMajorRoad | 2,000 | Major-road segment cap before ±10% parameter randomization. |
| maxMinorRoad | 10,000 | Minor-road segment cap before ±10% parameter randomization. |
| branchingProbability | 0.075 | Major-road branching control; clamped by the configured range. |
| crossingDeletionProbability | 0.1 | Controls removal of some minor-road nodes during cleanup. |
| majorThickness / minorThickness | 2.5 / 0.9 | Road widths used when deriving block boundaries. |
| sidewalkThickness | 0.5 | Inset used when deriving buildable lots. |
| minBuildHeight / maxBuildHeight | 2 / 45 | Inputs to building-height assignment; the current implementation does not guarantee every resulting height lies within these bounds. |

At startup, 11 city parameters are randomized by approximately ±10%, subject to their configured clamps. The prop spawner uses a separate randomSeed (42 in the inspected scene). Fixed seeds enable seeded generation, but end-to-end repeatability has not been tested.

## Performance and verification

The code records road-generation, lot-generation, and background city-computation durations. The city timer stops before the main thread creates the Unity GameObjects, meshes, renderers, and colliders. No benchmark log, frame-rate result, memory measurement, or automated CityScanPCG test result is included here.

Object creation currently happens one object at a time on the main thread. This version does not implement GPU instancing, object pooling, mesh combining, LOD, or Unity Jobs/Burst. Treat generation speed, frame rate, memory use, output counts, placement quality, and complete-scene repeatability as unmeasured until profiled in a specified Unity Player build and on recorded hardware.

## Repository layout

    Assets/CityScanPCG/
    ├── RandomObjectSpawner.cs
    ├── Resources/Material/
    └── Scripts/
        ├── BlockDivision/
        ├── BlockGeneration/
        ├── GraphModel/
        ├── MeshGeneration/
        ├── RoadGeneration/
        └── Services/

## License

CityScanPCG is released under the MIT License. See LICENSE and THIRD_PARTY_NOTICES.md for upstream attribution and license notices.
