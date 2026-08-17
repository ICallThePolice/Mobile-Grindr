public struct Voxel
{
    public float VitalHealth;
    public float ErebHealth;
    public float PsychoHealth;

    /// <summary>
    /// Функция для вычисления общей "плотности" вокселя.
    /// Алгоритм Marching Cubes будет использовать это значение, чтобы определить,
    /// является ли точка "твердой" или "пустой", и построить геометрию на основе этого.
    /// </summary>
    /// <returns>Суммарное значение плотности.</returns>
    public float GetDensity()
    {
        // Простое сложение — хорошая отправная точка. Логику можно усложнить позже.
        return VitalHealth + ErebHealth + PsychoHealth;
    }
}