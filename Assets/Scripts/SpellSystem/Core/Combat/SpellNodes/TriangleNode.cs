using System.Collections.Generic;
using UnityEngine;
using SpellSystem.Data;

namespace SpellSystem.Core
{
    public class TriangleNode : SpellNode
    {
        private SpellProjectile projectilePrefab;

        public TriangleNode(SpellProjectile prefab, EnergyDataSO energy)
        {
            this.projectilePrefab = prefab;
            this.LayerEnergy = energy;
        }

        public override void Execute(SpellContext context)
        {
            if (projectilePrefab == null) return;

            Vector3 spawnPos = context.HitPosition != Vector3.zero ? context.HitPosition : (context.Caster != null ? context.Caster.position : Vector3.zero);
            Vector3 direction = context.Direction != Vector3.zero ? context.Direction.normalized : Vector3.forward;

            if (context.Target != null && !context.IsChainCast)
            {
                direction = (context.Target.position - spawnPos).normalized;
            }

            float damage = LayerEnergy != null ? LayerEnergy.baseDamage : 10f;
            int projectilesCount = context.IsInnate ? 1 : 2;

            Vector3 rightVector = Vector3.Cross(Vector3.up, direction).normalized;
            if (rightVector == Vector3.zero) rightVector = Vector3.right;

            for (int i = 0; i < projectilesCount; i++)
            {
                Vector3 currentDir = direction;
                Vector3 currentPos = spawnPos;

                // Универсальная логика выстраивания любого количества снарядов параллельно
                if (projectilesCount > 1 && !context.IsChainCast)
                {
                    // ИСПРАВЛЕНИЕ: Чем больше шары, тем шире мы их расставляем
                    float spacing = 1.0f * context.ChargeMultiplier;
                    float offset = (i - (projectilesCount - 1) / 2f) * spacing;

                    currentPos += rightVector * offset;
                }

                var proj = GameObject.Instantiate(projectilePrefab, currentPos, Quaternion.LookRotation(currentDir));

                // ИСПРАВЛЕНИЕ: Передаем Caster в качестве защиты, если SourceTarget пуст
                Transform safeIgnore = context.SourceTarget != null ? context.SourceTarget : context.Caster;
                proj.Initialize(damage, LayerEnergy, context.Target, safeIgnore, context.ChargeMultiplier);

                proj.OnImpact += (hitPos, hitTarget) =>
                {
                    SpellContext branchContext = new SpellContext
                    {
                        Caster = context.Caster,
                        HitPosition = hitPos,
                        SourceTarget = hitTarget,
                        HitTargets = new List<Transform>(),
                        IsHardLocked = context.IsHardLocked,
                        ChargeLevel = context.ChargeLevel,
                        ChargeMultiplier = context.ChargeMultiplier,
                        IsChainCast = true,
                        IsInnate = context.IsInnate
                    };

                    if (hitTarget != null) branchContext.HitTargets.Add(hitTarget);

                    Transform nextTarget = FindNearestTarget(hitPos, hitTarget);
                    branchContext.Target = nextTarget;
                    branchContext.Direction = nextTarget != null ? (nextTarget.position - hitPos).normalized : currentDir;

                    TriggerNextPhase(branchContext);
                };
            }
        }
    }
}
