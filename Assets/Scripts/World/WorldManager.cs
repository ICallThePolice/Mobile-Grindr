using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

public struct BiomeData
{
    public float baseHeight;
    public int biomeType;
    public float pad1; // Выравнивание до 16 байт
    public float pad2; // Выравнивание до 16 байт
}

public struct VoxelData
{
    public float density;
    public float liquidDensity;
    public float biomeWeightR;
    public float biomeWeightG;
    public float biomeWeightB;
    public float pad1; // Выравнивание до 32 байт (8 флоатов)
    public float pad2; // Выравнивание до 32 байт
    public float pad3; // Выравнивание до 32 байт
}

public class WorldManager : MonoBehaviour
{
    public static WorldManager Instance;

    [Header("Настройки мира")]
    public Vector3Int chunkSize = new Vector3Int(24, 128, 24);
    public int viewDistance = 4;
    public float isoLevel = 0f;

    [Header("Настройки генерации")]
    public float noiseFrequency = 0.02f;
    public float noiseAmplitude = 15f;
    public float biomeMapScale = 0.01f;
    public float surfaceLevel = 24f;

    [Header("Настройки фрактального шума (fBm)")]
    public float lacunarity = 2.0f;
    public float persistence = 0.5f;

    [Header("Ссылки")]
    public GameObject chunkPrefab;
    public Transform player;
    public ComputeShader noiseGenerator;
    public GameObject psyCrystalPrefab;
    public GameObject floatingIslandPrefab; // Префаб для островов Эреба
    public Material worldMaterial;
    public Material liquidMaterial;

    [Header("Биомы")]
    public BiomeDefinition[] biomeDefinitions;
    [Header("Ядра Биомов (Biome Cores)")]
    public GameObject coreCrystalPrefab;
    [Tooltip("Расстояние между потенциальными ядрами в юнитах")]
    public float coreGridSpacing = 500f;

    private readonly Dictionary<Vector3Int, Chunk> activeChunks = new Dictionary<Vector3Int, Chunk>();
    private readonly Queue<Vector3Int> generationQueue = new Queue<Vector3Int>();
    private readonly HashSet<Vector3Int> generatingChunks = new HashSet<Vector3Int>();

    private ComputeBuffer biomeDataBuffer;

    private Vector3Int currentPlayerChunk = new Vector3Int(999999, 999999, 999999);
    private bool isPlayerSpawned = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        SetupBiomeData();
    }

    private void Start()
    {
        if (player != null)
        {
            var charController = player.GetComponent<CharacterController>();
            if (charController != null) charController.enabled = false;
        }
    }

    private void Update()
    {
        UpdateVisibleChunks();

        if (generationQueue.Count > 0)
        {
            Vector3Int chunkToGenerate = generationQueue.Dequeue();
            generatingChunks.Add(chunkToGenerate);
            RequestChunkData(chunkToGenerate);
        }
    }

    private void OnDestroy() //[cite: 2]
    {
        // Очищаем статику для анализатора Unity
        if (Instance == this) Instance = null;

        if (biomeDataBuffer != null) //[cite: 2]
        {
            biomeDataBuffer.Release(); //[cite: 2]
        }
    }

    private void SetupBiomeData()
    {
        if (biomeDefinitions == null || biomeDefinitions.Length == 0) return;

        var biomeDataArray = new BiomeData[biomeDefinitions.Length];
        for (int i = 0; i < biomeDefinitions.Length; i++)
        {
            var modifier = biomeDefinitions[i].modifiers.FirstOrDefault();
            if (modifier != null)
            {
                biomeDataArray[i].baseHeight = modifier.baseHeight;
                biomeDataArray[i].biomeType = modifier.GetBiomeType();
            }
        }

        biomeDataBuffer = new ComputeBuffer(biomeDataArray.Length, System.Runtime.InteropServices.Marshal.SizeOf(typeof(BiomeData)));
        biomeDataBuffer.SetData(biomeDataArray);
    }

    private void UpdateVisibleChunks()
    {
        if (player == null) return;

        Vector3Int newPlayerChunk = new Vector3Int(
            Mathf.FloorToInt(player.position.x / (chunkSize.x - 1)),
            0,
            Mathf.FloorToInt(player.position.z / (chunkSize.z - 1))
        );

        if (newPlayerChunk != currentPlayerChunk)
        {
            currentPlayerChunk = newPlayerChunk;

            for (int x = -viewDistance; x <= viewDistance; x++)
            {
                for (int z = -viewDistance; z <= viewDistance; z++)
                {
                    Vector3Int coord = new Vector3Int(currentPlayerChunk.x + x, 0, currentPlayerChunk.z + z);

                    // ИСПРАВЛЕНО: Проверка находится внутри цикла
                    if (!activeChunks.ContainsKey(coord) && !generationQueue.Contains(coord) && !generatingChunks.Contains(coord))
                    {
                        generationQueue.Enqueue(coord);
                    }
                }
            }

            List<Vector3Int> chunksToRemove = new List<Vector3Int>();
            foreach (var chunkCoord in activeChunks.Keys)
            {
                if (Mathf.Abs(chunkCoord.x - currentPlayerChunk.x) > viewDistance + 1 ||
                    Mathf.Abs(chunkCoord.z - currentPlayerChunk.z) > viewDistance + 1)
                {
                    chunksToRemove.Add(chunkCoord);
                }
            }

            foreach (var coord in chunksToRemove)
            {
                Destroy(activeChunks[coord].gameObject);
                activeChunks.Remove(coord);
            }
        }
    }

    private void RequestChunkData(Vector3Int chunkCoord)
    {
        if (activeChunks.ContainsKey(chunkCoord) || biomeDataBuffer == null) return;

        int numVoxels = chunkSize.x * chunkSize.y * chunkSize.z;
        ComputeBuffer voxelBuffer = new ComputeBuffer(numVoxels, System.Runtime.InteropServices.Marshal.SizeOf(typeof(VoxelData)));
        int kernel = noiseGenerator.FindKernel("CSMain");

        noiseGenerator.SetBuffer(kernel, "voxelBuffer", voxelBuffer);
        noiseGenerator.SetBuffer(kernel, "_BiomeData", biomeDataBuffer);

        noiseGenerator.SetVector("chunkSize", new Vector4(chunkSize.x, chunkSize.y, chunkSize.z, 0));
        noiseGenerator.SetVector("chunkPosition", new Vector4(chunkCoord.x, chunkCoord.y, chunkCoord.z, 0));
        noiseGenerator.SetInt("numBiomes", biomeDefinitions.Length);

        noiseGenerator.SetFloat("_Amplitude", noiseAmplitude);
        noiseGenerator.SetFloat("_Frequency", noiseFrequency);
        noiseGenerator.SetFloat("_SurfaceLevel", surfaceLevel);
        noiseGenerator.SetFloat("_BiomeMapScale", biomeMapScale);
        noiseGenerator.SetFloat("_Lacunarity", lacunarity);
        noiseGenerator.SetFloat("_Persistence", persistence);

        int dispatchSizeX = Mathf.CeilToInt((float)chunkSize.x / 8);
        int dispatchSizeY = Mathf.CeilToInt((float)chunkSize.y / 8);
        int dispatchSizeZ = Mathf.CeilToInt((float)chunkSize.z / 8);
        noiseGenerator.Dispatch(kernel, dispatchSizeX, dispatchSizeY, dispatchSizeZ);

        // Вычисляем центр текущего чанка в 2D
        Vector2 chunkCenter2D = new Vector2(
            chunkCoord.x * (chunkSize.x - 1) + (chunkSize.x / 2f),
            chunkCoord.z * (chunkSize.z - 1) + (chunkSize.z / 2f)
        );

        // Находим ближайшее ядро и передаем в GPU
        Vector2 nearestCorePos = GetNearestCorePosition(chunkCenter2D);
        noiseGenerator.SetVector("_NearestCorePos", new Vector4(nearestCorePos.x, nearestCorePos.y, 0, 0));

        AsyncGPUReadback.Request(voxelBuffer, request => OnChunkDataReceived(request, chunkCoord, voxelBuffer));
    }

    private void OnChunkDataReceived(AsyncGPUReadbackRequest request, Vector3Int chunkCoord, ComputeBuffer buffer)
    {
        if (request.hasError || !this.enabled)
        {
            generatingChunks.Remove(chunkCoord);
            if (buffer != null) buffer.Release();
            return;
        }

        NativeArray<VoxelData> voxels = new NativeArray<VoxelData>(request.GetData<VoxelData>(), Allocator.TempJob);

        NativeList<float3> vertices = new NativeList<float3>(Allocator.TempJob);
        NativeList<int> triangles = new NativeList<int>(Allocator.TempJob);
        NativeList<Color> vertexColors = new NativeList<Color>(Allocator.TempJob);

        NativeList<float3> liquidVertices = new NativeList<float3>(Allocator.TempJob);
        NativeList<int> liquidTriangles = new NativeList<int>(Allocator.TempJob);
        NativeList<Color> liquidColors = new NativeList<Color>(Allocator.TempJob);

        MarchingCubesJob job = new MarchingCubesJob
        {
            voxels = voxels,
            chunkSize = new int3(chunkSize.x, chunkSize.y, chunkSize.z),
            isoLevel = this.isoLevel,
            vertices = vertices,
            triangles = triangles,
            vertexColors = vertexColors,
            liquidVertices = liquidVertices,
            liquidTriangles = liquidTriangles,
            liquidColors = liquidColors
        };

        JobHandle handle = job.Schedule();
        StartCoroutine(ProcessMeshData(handle, chunkCoord, voxels, vertices, triangles, vertexColors, liquidVertices, liquidTriangles, liquidColors));
        buffer.Release();
    }

    private IEnumerator ProcessMeshData(JobHandle jobHandle, Vector3Int chunkCoord, NativeArray<VoxelData> voxels,
        NativeList<float3> vertices, NativeList<int> triangles, NativeList<Color> vertexColors,
        NativeList<float3> liquidVertices, NativeList<int> liquidTriangles, NativeList<Color> liquidColors)
    {
        yield return new WaitUntil(() => jobHandle.IsCompleted);
        jobHandle.Complete();

        if (vertices.Length > 3)
        {
            CreateChunkObject(chunkCoord, vertices.AsArray(), triangles.AsArray(), vertexColors.AsArray(),
                                          liquidVertices.AsArray(), liquidTriangles.AsArray(), liquidColors.AsArray());
        }

        voxels.Dispose();
        vertices.Dispose(); triangles.Dispose(); vertexColors.Dispose();
        liquidVertices.Dispose(); liquidTriangles.Dispose(); liquidColors.Dispose();
    }

    private void CreateChunkObject(Vector3Int chunkCoord, NativeArray<float3> vertices, NativeArray<int> triangles, NativeArray<Color> vertexColors,
                                                          NativeArray<float3> lVerts, NativeArray<int> lTris, NativeArray<Color> lCols)
    {
        generatingChunks.Remove(chunkCoord);

        if (activeChunks.ContainsKey(chunkCoord)) return;

        Mesh mesh = new Mesh { indexFormat = IndexFormat.UInt32 };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles.ToArray(), 0);
        mesh.SetColors(vertexColors);
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();

        Vector3 position = new Vector3(chunkCoord.x * (chunkSize.x - 1), chunkCoord.y * (chunkSize.y - 1), chunkCoord.z * (chunkSize.z - 1));

        GameObject chunkObject = Instantiate(chunkPrefab, position, Quaternion.identity, this.transform);
        chunkObject.name = $"Chunk {chunkCoord}";

        chunkObject.GetComponent<MeshFilter>().sharedMesh = mesh;
        chunkObject.GetComponent<MeshRenderer>().material = worldMaterial;

        MeshCollider collider = chunkObject.AddComponent<MeshCollider>();
        collider.sharedMesh = mesh;

        if (lVerts.Length > 3)
        {
            Mesh liquidMesh = new Mesh { indexFormat = IndexFormat.UInt32 };
            liquidMesh.SetVertices(lVerts);
            liquidMesh.SetTriangles(lTris.ToArray(), 0);
            liquidMesh.SetColors(lCols);
            liquidMesh.RecalculateBounds();
            liquidMesh.RecalculateNormals();

            GameObject liquidObj = new GameObject("Liquid");
            liquidObj.transform.SetParent(chunkObject.transform);
            liquidObj.transform.localPosition = Vector3.zero;

            MeshCollider liquidCollider = liquidObj.AddComponent<MeshCollider>();
            liquidCollider.sharedMesh = liquidMesh;
            liquidObj.tag = "Hazard";

            liquidObj.AddComponent<MeshFilter>().sharedMesh = liquidMesh;
            liquidObj.AddComponent<MeshRenderer>().material = liquidMaterial;
        }

        // === СПАВН ЯДРА БИОМА ===
        Vector2 chunkCenter2D = new Vector2(
            chunkCoord.x * (chunkSize.x - 1) + (chunkSize.x / 2f),
            chunkCoord.z * (chunkSize.z - 1) + (chunkSize.z / 2f)
        );
        Vector2 corePos = GetNearestCorePosition(chunkCenter2D);

        // Проверяем, находится ли координата Ядра физически внутри текущего сгенерированного чанка
        float minX = chunkCoord.x * (chunkSize.x - 1);
        float maxX = minX + chunkSize.x;
        float minZ = chunkCoord.z * (chunkSize.z - 1);
        float maxZ = minZ + chunkSize.z;

        if (corePos.x >= minX && corePos.x < maxX && corePos.y >= minZ && corePos.y < maxZ)
        {
            // Если Ядро в этом чанке - запускаем корутину установки Кристалла
            StartCoroutine(SpawnCoreSafely(new Vector3(corePos.x, 0, corePos.y), chunkObject.transform));
        }

        // --- СПАВН ДЕКОРАЦИЙ (Кристаллы) ---
        if (psyCrystalPrefab != null && vertices.Length > 0)
        {
            uint seed = (uint)(chunkCoord.x * 73856093 ^ chunkCoord.y * 19349663 ^ chunkCoord.z * 83492791 | 1);
            Unity.Mathematics.Random rng = new Unity.Mathematics.Random(seed);
            Vector3[] normals = mesh.normals;

            for (int i = 0; i < vertices.Length; i += 20)
            {
                if (normals[i].y > 0.8f)
                {
                    Vector3 localPos = vertices[i];
                    Vector3 worldPos = position + localPos;

                    // БИОМ ПСАЙ: Спавн кристаллов (синий канал)
                    if (vertexColors[i].b > 0.6f && rng.NextFloat() < 0.02f)
                    {
                        GameObject crystal = Instantiate(psyCrystalPrefab, worldPos, Quaternion.identity, chunkObject.transform);
                        crystal.transform.rotation = Quaternion.Euler(0, rng.NextFloat(0f, 360f), 0);
                        crystal.transform.localScale = Vector3.one * rng.NextFloat(0.5f, 2.0f);
                    }
                }
            }
        }

        if (!activeChunks.ContainsKey(chunkCoord)) activeChunks.Add(chunkCoord, chunkObject.GetComponent<Chunk>());
        if (!isPlayerSpawned && chunkCoord == currentPlayerChunk) StartCoroutine(SpawnPlayerSafely());
    }

    private IEnumerator SpawnCoreSafely(Vector3 targetPos, Transform parentChunk)
    {
        // Даем физике Unity полсекунды на создание коллайдеров меша
        yield return new WaitForSeconds(0.5f);

        // Пускаем луч с высоты 200 юнитов строго вниз, чтобы нащупать дно нашего Гранд-кратера
        Vector3 rayStart = new Vector3(targetPos.x, 200f, targetPos.z);

        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 250f))
        {
            if (coreCrystalPrefab != null)
            {
                // Спавним Ядро на 3 юнита ВЫШЕ дна, чтобы оно угрожающе парило над жижей
                GameObject core = Instantiate(coreCrystalPrefab, hit.point + (Vector3.up * 3f), Quaternion.identity, parentChunk);
                core.name = "BiomeCore_Ereb";
            }
        }
    }

    public Vector2 GetNearestCorePosition(Vector2 worldPos2D)
    {
        // ИСПРАВЛЕНИЕ: Используем int и Mathf.FloorToInt вместо float
        int sectorX = Mathf.FloorToInt(worldPos2D.x / coreGridSpacing);
        int sectorY = Mathf.FloorToInt(worldPos2D.y / coreGridSpacing);

        // Теперь побитовые операции сработают без ошибок
        uint seed = (uint)(sectorX * 73856093 ^ sectorY * 19349663 | 1);
        Unity.Mathematics.Random rng = new Unity.Mathematics.Random(seed);

        float offsetX = rng.NextFloat(0.2f, 0.8f) * coreGridSpacing;
        float offsetY = rng.NextFloat(0.2f, 0.8f) * coreGridSpacing;

        float coreX = (sectorX * coreGridSpacing) + offsetX;
        float coreY = (sectorY * coreGridSpacing) + offsetY;

        return new Vector2(coreX, coreY);
    }

    private IEnumerator SpawnPlayerSafely()
    {
        CharacterController charController = player.GetComponent<CharacterController>();
        if (charController != null) charController.enabled = false;

        bool isSpawned = false;

        while (!isSpawned)
        {
            yield return new WaitForSeconds(0.1f);

            // Ищем землю с высоты 30, чтобы оказаться строго под летающими островами
            Vector3 rayStart = new Vector3(player.position.x, 30f, player.position.z);

            if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 40f))
            {
                if (hit.collider.gameObject.name.Contains("Chunk"))
                {
                    // 1. Проверяем, что не упали в жижу (Hazard)
                    // 2. Убеждаемся, что высота земли в пределах нормы биомов (от 8 до 26)
                    if (hit.collider.CompareTag("Hazard") || hit.point.y < 8f || hit.point.y > 26f)
                    {
                        // Спавн неудачный. Смещаемся на 15 юнитов по диагонали и пробуем снова.
                        player.position += new Vector3(15f, 0, 15f);
                    }
                    else
                    {
                        // Поднимаем игрока на 2 юнита над землей, чтобы точно не застрять в полигонах
                        Vector3 spawnPos = hit.point + (Vector3.up * 2.0f);

                        // Делаем физическую проверку сферы: если в радиусе 0.5 юнитов есть камни - спавн отменяется
                        if (!Physics.CheckSphere(spawnPos, 0.5f))
                        {
                            player.position = spawnPos;
                            if (charController != null) charController.enabled = true;

                            isSpawned = true;
                            isPlayerSpawned = true;
                            Debug.Log($"Игрок успешно заспавнен на безопасной высоте {hit.point.y}");
                        }
                        else
                        {
                            player.position += new Vector3(15f, 0, 15f);
                        }
                    }
                }
            }
            else
            {
                // Луч улетел в пустоту - смещаемся
                player.position += new Vector3(15f, 0, 15f);
            }
        }
    }
}