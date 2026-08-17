using System;

[Serializable]
public abstract class BiomeModifier
{
    public float baseHeight;
    public abstract int GetBiomeType(); // 0 = Vital, 1 = Ereb, 2 = Psy
}

[Serializable]
public class VitalModifier : BiomeModifier
{
    public VitalModifier() { baseHeight = 20f; }
    public override int GetBiomeType() => 0;
}

[Serializable]
public class ErebModifier : BiomeModifier
{
    public ErebModifier() { baseHeight = 15f; }
    public override int GetBiomeType() => 1;
}

[Serializable]
public class PsyModifier : BiomeModifier
{
    public PsyModifier() { baseHeight = 30f; }
    public override int GetBiomeType() => 2;
}