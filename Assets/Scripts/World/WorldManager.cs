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
    public float surfaceLevel = 24f; // Оставлено для совместимости, но теперь базовая высота берется из биомов

    [Header("Настройки фрактального шума (fBm)")]
    public float lacunarity = 2.0f;
    public float persistence = 0.5f;

    [Header("Ссылки")]
    public GameObject chunkPrefab;
    public Transform player;
    public ComputeShader noiseGenerator;
    public Material worldMaterial;

    [Header("Биомы")]
    public BiomeDefinition[] biomeDefinitions;

    private readonly Dictionary<Vector3Int, Chunk> activeChunks = new Dictionary<Vector3Int, Chunk>();
    private readonly Queue<Vector3Int> generationQueue = new Queue<Vector3Int>();

    private ComputeBuffer biomeDataBuffer;

    // Переменные для динамической подгрузки и спавна
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
        // Отключаем гравитацию и управление у игрока, пока он не заспавнился на земле
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
            RequestChunkData(generationQueue.Dequeue());
        }
    }

    private void OnDestroy()
    {
        if (biomeDataBuffer != null)
        {
            biomeDataBuffer.Release();
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

    // --- ДИНАМИЧЕСКАЯ ЗАГРУЗКА И ВЫГРУЗКА ---
    private void UpdateVisibleChunks()
    {
        if (player == null) return;

        // Вычисляем, в каком чанке сейчас стоит игрок
        Vector3Int newPlayerChunk = new Vector3Int(
            Mathf.FloorToInt(player.position.x / (chunkSize.x - 1)),
            0,
            Mathf.FloorToInt(player.position.z / (chunkSize.z - 1))
        );

        // Если игрок перешел в новый чанк
        if (newPlayerChunk != currentPlayerChunk)
        {
            currentPlayerChunk = newPlayerChunk;

            // 1. Добавляем в очередь новые чанки в радиусе видимости
            for (int x = -viewDistance; x <= viewDistance; x++)
            {
                for (int z = -viewDistance; z <= viewDistance; z++)
                {
                    Vector3Int coord = new Vector3Int(currentPlayerChunk.x + x, 0, currentPlayerChunk.z + z);

                    if (!activeChunks.ContainsKey(coord) && !generationQueue.Contains(coord))
                    {
                        generationQueue.Enqueue(coord);
                    }
                }
            }

            // 2. Ищем и удаляем чанки, которые вышли за радиус (viewDistance + 1 для буфера)
            List<Vector3Int> chunksToRemove = new List<Vector3Int>();
            foreach (var chunkCoord in activeChunks.Keys)
            {
                if (Mathf.Abs(chunkCoord.x - currentPlayerChunk.x) > viewDistance + 1 ||
                    Mathf.Abs(chunkCoord.z - currentPlayerChunk.z) > viewDistance + 1)
                {
                    chunksToRemove.Add(chunkCoord);
                }
            }

            // Уничтожаем старые чанки
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
        ComputeBuffer densityBuffer = new ComputeBuffer(numVoxels, sizeof(float));
        int kernel = noiseGenerator.FindKernel("CSMain");

        noiseGenerator.SetBuffer(kernel, "densityBuffer", densityBuffer);
        noiseGenerator.SetBuffer(kernel, "_BiomeData", biomeDataBuffer);

        noiseGenerator.SetInts("chunkSize", chunkSize.x, chunkSize.y, chunkSize.z);
        noiseGenerator.SetInts("chunkPosition", chunkCoord.x, chunkCoord.y, chunkCoord.z);
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

        AsyncGPUReadback.Request(densityBuffer, request => OnChunkDataReceived(request, chunkCoord, densityBuffer));
    }

    private void OnChunkDataReceived(AsyncGPUReadbackRequest request, Vector3Int chunkCoord, ComputeBuffer buffer)
    {
        if (request.hasError || !this.enabled)
        {
            if (buffer != null) buffer.Release();
            return;
        }

        NativeArray<float> persistentDensities = new NativeArray<float>(request.GetData<float>(), Allocator.TempJob);
        NativeList<float3> vertices = new NativeList<float3>(Allocator.TempJob);
        NativeList<int> triangles = new NativeList<int>(Allocator.TempJob);

        MarchingCubesJob job = new MarchingCubesJob
        {
            densities = persistentDensities,
            chunkSize = new int3(chunkSize.x, chunkSize.y, chunkSize.z),
            isoLevel = this.isoLevel,
            vertices = vertices,
            triangles = triangles
        };

        JobHandle handle = job.Schedule();
        StartCoroutine(ProcessMeshData(handle, chunkCoord, persistentDensities, vertices, triangles));

        buffer.Release();
    }

    private IEnumerator ProcessMeshData(JobHandle jobHandle, Vector3Int chunkCoord, NativeArray<float> persistentDensities, NativeList<float3> vertices, NativeList<int> triangles)
    {
        yield return new WaitUntil(() => jobHandle.IsCompleted);
        jobHandle.Complete();

        if (vertices.Length > 3)
        {
            CreateChunkObject(chunkCoord, vertices.AsArray(), triangles.AsArray());
        }

        persistentDensities.Dispose();
        vertices.Dispose();
        triangles.Dispose();
    }

    private void CreateChunkObject(Vector3Int chunkCoord, NativeArray<float3> vertices, NativeArray<int> triangles)
    {
        Mesh mesh = new Mesh { indexFormat = IndexFormat.UInt32 };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles.ToArray(), 0);
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();

        Vector3 position = new Vector3(chunkCoord.x * (chunkSize.x - 1), chunkCoord.y * (chunkSize.y - 1), chunkCoord.z * (chunkSize.z - 1));

        GameObject chunkObject = Instantiate(chunkPrefab, position, Quaternion.identity, this.transform);
        chunkObject.name = $"Chunk {chunkCoord}";

        chunkObject.GetComponent<MeshFilter>().sharedMesh = mesh;
        chunkObject.GetComponent<MeshRenderer>().material = worldMaterial;

        MeshCollider collider = chunkObject.AddComponent<MeshCollider>();
        collider.sharedMesh = mesh;

        if (!activeChunks.ContainsKey(chunkCoord))
        {
            activeChunks.Add(chunkCoord, chunkObject.GetComponent<Chunk>());
        }

        // --- ЛОГИКА БЕЗОПАСНОГО СПАВНА ---
        // Если это первый центральный чанк под игроком, спавним его
        if (!isPlayerSpawned && chunkCoord == currentPlayerChunk)
        {
            StartCoroutine(SpawnPlayerSafely());
        }
    }

    private IEnumerator SpawnPlayerSafely()
    {
        // Ждем два кадра, чтобы физический движок Unity успел зарегистрировать MeshCollider
        yield return new WaitForEndOfFrame();
        yield return new WaitForFixedUpdate();

        // Стреляем лучом с высоты 150 вниз, чтобы найти наивысшую точку сгенерированной земли
        Vector3 rayStart = new Vector3(player.position.x, 150f, player.position.z);

        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 200f))
        {
            CharacterController charController = player.GetComponent<CharacterController>();

            // Обязательно отключаем контроллер перед телепортацией, иначе Unity проигнорирует смену координат
            charController.enabled = false;

            // Ставим игрока чуть выше найденной точки, чтобы он плавно упал на ноги
            player.position = hit.point + (Vector3.up * 1f);

            charController.enabled = true;
            isPlayerSpawned = true;

            Debug.Log($"Игрок успешно заспавнен на высоте {hit.point.y}");
        }
    }
}