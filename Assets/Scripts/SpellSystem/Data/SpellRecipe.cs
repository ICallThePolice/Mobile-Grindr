using System;

namespace SpellSystem.Data
{
    [Serializable]
    public struct SpellRecipe
    {
        public ShapeDataSO shape;
        public EnergyDataSO energy;

        public bool IsValid => shape != null && energy != null;

        public void Clear()
        {
            shape = null;
            energy = null;
        }
    }
}