using UnityEngine;

/// <summary>
/// Управляет состоянием и данными одного фрагмента (чанка) мира.
/// Этот компонент будет находиться на префабе чанка.
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class Chunk : MonoBehaviour
{
    // Массив, хранящий данные о всех вокселях в этом чанке.
    public Voxel[,,] Voxels;

    // Ссылки на компоненты для отображения геометрии чанка.
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;

    private void Awake()
    {
        // Получаем ссылки на компоненты при создании объекта.
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
    }

    /// <summary>
    /// Метод для инициализации чанка с заданным размером.
    /// </summary>
    /// <param name="size">Размер чанка в вокселях по каждой оси.</param>
    public void Initialize(Vector3Int size)
    {
        Voxels = new Voxel[size.x, size.y, size.z];
    }
}