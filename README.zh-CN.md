# CityScanPCG

[English](README.md) | **简体中文**

CityScanPCG 是一个使用 C# 编写的 Unity 程序化城市场景生成插件。它根据地图尺度和生成参数构建主次道路图、道路围合街区与建筑地块、挤出式建筑网格、启发式公园区域，并可选放置街道道具。项目目标是生成规模、密度和路网复杂度可调的城市布局，避免在规则网格上均匀铺设建筑。

## 解决的问题、输入与输出

插件用于减少大型城市可视化场景和测试场景的重复手工搭建工作：先按规则生长主路及其分支，再生成支路；根据道路宽度提取街区并递归切分可建地块，最后生成场景几何。独立的道具组件还可放置车辆、道路杆件和长椅。

**输入：** Unity 场景中的 `CityGenerator`、Inspector 参数（地图尺寸、随机种子、道路生长上限与概率、道路宽度、人行道内缩、建筑高度参数）；如需道具，还需提供预制件并配置 `RandomObjectSpawner`。坐标和距离使用 Unity 单位。当前生成器不导入 GIS/OSM 数据、地形、用地规划图或用户绘制的道路。

**输出：** 内存中的主次路网 `Graph` 以及街区/地块数据；运行时创建名为 `Road Plane`、`Block Container` 和 `Lot Container` 的 Unity 对象，并附带网格、渲染器和碰撞体。公园以绿色平面街区呈现，建筑以地块挤出网格呈现。可选预制件实例会放在道路平面对象下。

**面向的使用方式：** 仓库提供的是在 Unity 场景中通过 Inspector 配置、运行时生成的流程。生成场景可用于可视化，也可作为仿真项目的环境起点。当前版本没有独立地图导出格式或仿真器集成 API；周边原型中的“准备完成”通知并不传输道路或场景几何。

**如何判断生成成功：** 在演示中检查道路图、非空街区/地块几何，以及场景中的可见对象和碰撞体；确认道路空隙、建筑地块和绿色公园均有生成，且没有生成异常。下方截图记录了一次成功的视觉运行。本仓库没有自动化质量门槛或性能基准。此外，当前 `GenerationComplete` 会在主线程开始创建 GameObject 之前置为真，不能把它单独当作场景已经完整生成的依据。

## 效果展示

以下图片和视频来自用户提供的 Unity 运行记录。Inspector 截图中的数值是当时配置的值；`CityGenerator` 启动时会对 11 项城市参数进行约 ±10% 扰动，因此截图值不一定等于该次运行实际使用值。

### 城市与地块布局

城市截图展示了灰色挤出建筑、道路空间和绿色平面区域；地块和路网截图从俯视与侧视角展示生成布局及 Gizmos。

![生成后的城市场景](media/screenshots/city-overview.png)

![生成的建筑地块分布](media/screenshots/building-lot-distribution.png)

![路网局部效果](media/screenshots/road-network-detail-1.png)

![路网局部效果](media/screenshots/road-network-detail-2.png)

![路网与城市天际线](media/screenshots/road-network-overview.png)

[观看路网生成视频](media/video/road-network-generation.mp4)

### 截图中的参数

| 组件 | 截图所示 Inspector 值 | 控制内容 |
|---|---|---|
| `CityGenerator` | `mapSize` 300；`seed` 13 | 初始地图范围和城市随机源。扰动前道路平面在水平两轴约覆盖 −300 至 +300。 |
| 主路 | `maxDegreeInCurves` 10；`branchingProbability` 0.075；`maxMajorRoad` 2,000 | 方向变化范围、分支控制和路段数上限。上限不是保证生成数量。 |
| 支路 | `crossingDeletionProbability` 0.1；`maxMinorRoad` 10,000 | 支路清理概率和路段数上限。 |
| 道路/街区尺寸 | `majorThickness` 2.5；`minorThickness` 0.9；`sidewalkThickness` 0.5 | 用于偏移街区边界及内缩建筑可用区域的宽度参数。 |
| 建筑高度输入 | `minBuildHeight` 2；`maxBuildHeight` 45 | 地块高度分配的输入值；当前实现不保证最终高度严格落在该范围。 |
| `RandomObjectSpawner` | 车辆密度 0.3、最小/最大 10/120；杆件密度 0.2、最小/最大 5/60；道路中心偏移 2；每个花园长椅 1–3；`randomSeed` 42 | 可选道具放置参数。截图还显示车辆与街区距离 0.2–1.5、杆件与街区距离 0.05–0.4、花园边缘距离 0.1、3 个车辆预制件、2 个杆件预制件及 `Bench_4`。 |

![CityGenerator 参数面板](media/screenshots/city-generator-settings.png)

![RandomObjectSpawner 装饰物参数面板](media/screenshots/prop-spawner-settings.png)

## 生成流程

```mermaid
flowchart LR
    A[随机种子与 Inspector 参数] --> B[主路生长]
    B --> C[支路生长与清理]
    C --> D[道路图]
    D --> E[道路偏移生成街区边界]
    E --> F[人行道内缩与递归地块切分]
    F --> G[多边形剖分与挤出]
    G --> H[主线程创建 Unity 场景对象]
    H --> I[可选放置车辆、杆件和长椅]
```

主路使用固定 10 Unity 单位的路段长度，通过概率分支和阶段性方向变化生长，并检查端点吸附、线段相交、地图边界和节点连接数。支路从主路网络继续生长，并按相交删除规则清理。算法思路受扩展 L-system 启发，但源码实现的是明确的规则生长，并非通用 L-system 解析器。

道路宽度用于偏移街区边界；随后对边界内缩以预留人行道和建筑空间，再递归切分地块。当前切分位置从外接矩形边上的 30%、40%、50% 或 60% 选取，最多递归 6 轮；当候选地块外接矩形面积小于 10 Unity 单位²或长宽比大于 4 时，不再继续切分。凸多边形采用扇形剖分，凹多边形采用耳切法，生成顶面和侧面网格及碰撞体。

边界顶点超过 10 个的街区会按几何启发式标记为公园。这能在展示效果中形成非建筑绿色区域，但它不是语义用地分类：插件目前不接收 Garden/功能区标签，也不保证公园分布或城市规划合理性。“让城市更符合常识”是设计目标；截图证明的是路网/街区布局有变化并出现绿地区域，不是经过验证的真实感评分。

## `MajorGenerator` 构造函数参数

`MajorGenerator` 是内部道路生长类，构造函数接收以下参数：

| 参数 | 含义 |
|---|---|
| `seededRandom` | 由城市种子创建、并与后续生成阶段共享的 `System.Random` 实例。它本身不是整数种子。相同随机输入有助于控制生成，但完整场景复现性尚未验证。 |
| `mapSize` | 道路生长按以原点为中心的边界检查；水平两轴范围为 `[-mapSize, +mapSize]`。 |
| `maxRoad` | 主路可接受路段数量的最大值，是停止上限而非目标数量。 |
| `maxDegree` | 方向变化/偏转参数的最大角度，单位为度。尽管名字中有 Degree，它不是图节点度数；节点连接数由独立逻辑限制为最多 4 条边。 |
| `branchingChance` | 分支选择的概率型控制值，用于决定增加左支路、右支路或双向支路。实际概率会被整数随机桶量化，并受参数范围约束。 |
| `graphToBuild` | 接收主路节点和边的可变 `Graph`；后续阶段继续加入支路，并据此提取街区。 |

## 快速开始

1. 打开 Unity 2023.2 项目。
2. 将 `Assets/CityScanPCG` 复制到项目的 `Assets` 目录，并等待 Unity 导入脚本和材质。
3. 在空 GameObject 上添加 `CityGenerator`，配置 Inspector 参数。
4. 进入 Play 模式，在运行时生成城市对象。
5. 如需道具，添加 `RandomObjectSpawner`，指定 `CityGenerator`，并提供需要的车辆、杆件和长椅预制件。

若在现有工程中替换原 `CityPCG` 文件夹，请先移除旧副本再添加本仓库。此源码发行版保留了 Unity 资源 GUID。道具预制件由使用方提供；原开发工程中的第三方模型包未包含在仓库中。

## 技术栈与限制

- Unity 2023.2（源码工程版本：2023.2.20f1c1）、C#、UnityEngine
- 图结构道路表示与计算几何
- 程序化 `Mesh`、`System.Random`、后台 `Thread` 和 Unity 协程
- Inspector 参数及 Gizmos，可检查道路、街区、三角剖分、地块和包围框

源码记录道路、地块和后台城市计算耗时，但城市计时在主线程创建 GameObject、网格、渲染器和碰撞体之前结束。仓库没有可引用的基准日志、帧率或内存结果、CityScanPCG 自动化测试或量化视觉质量评估。当前对象由主线程逐个创建；未实现 GPU Instancing、对象池、网格合并、LOD 或 Jobs/Burst。性能、输出数量、摆放质量和完整场景复现性都需在明确的 Unity Player 构建版本及记录硬件上测量后再报告。

## 仓库结构

```text
Assets/CityScanPCG/   Unity 插件源码和材质
media/screenshots/   Unity 效果及 Inspector 截图
media/video/         路网生成过程视频
```

## 许可证

CityScanPCG 使用 MIT 许可证开源。许可证和上游来源声明见 [LICENSE](LICENSE) 与 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。
