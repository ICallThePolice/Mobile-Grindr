using System;
using UnityEngine;

[Serializable]
public abstract class BiomeModifier
{
    public float baseHeight;
    public Color biomeColor; // НОВОЕ: Цвет биома
    public abstract int GetBiomeType();
}

[Serializable]
public class VitalModifier : BiomeModifier
{
    public VitalModifier() { baseHeight = 20f; biomeColor = new Color(0.8f, 0.2f, 0.2f); } // Красный
    public override int GetBiomeType() => 0;
}

[Serializable]
public class ErebModifier : BiomeModifier
{
    public ErebModifier() { baseHeight = 15f; biomeColor = new Color(0.2f, 0.2f, 0.2f); } // Темно-серый
    public override int GetBiomeType() => 1;
}

[Serializable]
public class PsyModifier : BiomeModifier
{
    public PsyModifier() { baseHeight = 30f; biomeColor = new Color(0.2f, 0.8f, 0.8f); } // Бирюзовый
    public override int GetBiomeType() => 2;
}