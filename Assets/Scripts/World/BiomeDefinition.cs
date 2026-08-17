using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "New Biome", menuName = "World/Biome Definition")]
public class BiomeDefinition : ScriptableObject
{
    // ИЗМЕНЕНИЕ ЗДЕСЬ: Добавляем наш новый атрибут
    [SerializeReference, SerializeReferenceMenu]
    public List<BiomeModifier> modifiers = new List<BiomeModifier>();
}