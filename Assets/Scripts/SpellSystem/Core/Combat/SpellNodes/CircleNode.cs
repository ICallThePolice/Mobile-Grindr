using System.Collections.Generic;
using UnityEngine;
using SpellSystem.Data;
using SpellSystem.Testing; // Необходимо для DummyTarget

namespace SpellSystem.Core
{
    public class CircleNode : SpellNode
    {
        private SpellAoE aoePrefab;

        public CircleNode(SpellAoE prefab, EnergyDataSO energy)
        {
            this.aoePrefab = prefab;
            this.LayerEnergy = energy;
        }

        public override void Execute(SpellContext context)
        {
            Vector3 spawnPos = Vector3.zero;
            float maxDistance = 15f;

            if (context.IsChainCast)
            {
                if (context.SourceTarget != null) spawnPos = context.HitPosition;
                else if (context.Target != null) spawnPos = context.Target.position;
                else if (context.HitPosition != Vector3.zero) spawnPos = context.HitPosition;
            }
            else
            {
                if (context.IsHardLocked && context.Target != null)
                {
                    spawnPos = context.Target.position;
                }
                else if (context.Caster != null)
                {
                    Vector3 origin = context.Caster.position + Vector3.up * 1f;

                    // Берем направление от КАМЕРЫ (куда смотрит игрок)
                    Vector3 dir = context.Direction;
                    if (Camera.main != null)
                    {
                        dir = Camera.main.transform.forward;
                    }

                    dir.y = 0f; // Обязательно обнуляем Y, чтобы луч шел строго параллельно земле
                    dir.Normalize();

                    // Пускаем широкий луч (радиус 2f) по направлению взгляда камеры
                    if (Physics.SphereCast(origin, 2f, dir, out RaycastHit hit, maxDistance))
                    {
                        DummyTarget dummy = hit.collider.GetComponentInParent<DummyTarget>();

                        // Если на линии взгляда есть манекен или разрушаемый объект
                        if (dummy != null || hit.collider.CompareTag("Damageable"))
                        {
                            spawnPos = hit.collider.transform.position;
                        }
                        else
                        {
                            // Если луч врезался в стену/препятствие
                            spawnPos = hit.point;
                        }
                    }
                    else
                    {
                        // Если впереди на 15 метров чисто - спавним круг на максимальной дальности
                        spawnPos = context.Caster.position + dir * maxDistance;
                    }

                    // Выравниваем круг по высоте персонажа
                    spawnPos.y = context.Caster.position.y;
                }
            }

            spawnPos.y += 0.1f; // Слегка приподнимаем над землей, чтобы избежать z-fighting (мерцания текстур)

            float damage = LayerEnergy != null ? LayerEnergy.baseDamage * 1.2f : 15f;
            float baseRadius = 4f;
            float finalRadius = baseRadius * context.ChargeMultiplier;

            context.HitTargets.Clear();
            Collider[] hits = Physics.OverlapSphere(spawnPos, finalRadius);
            foreach (var h in hits)
            {
                DummyTarget dummy = h.GetComponentInParent<DummyTarget>();
                if (dummy != null && !context.HitTargets.Contains(dummy.transform))
                {
                    context.HitTargets.Add(dummy.transform);
                }
            }

            if (aoePrefab != null)
            {
                var aoe = GameObject.Instantiate(aoePrefab, spawnPos, Quaternion.identity);
                aoe.Initialize(damage, finalRadius, LayerEnergy, context.ChargeMultiplier, context.ChargeLevel);
            }

            float cascadeDelay = 0.3f;
            if (context.HitTargets.Count > 0)
            {
                TriggerNextPhaseForEachTarget(context, true, cascadeDelay);
            }
            else
            {
                context.HitPosition = spawnPos;
                context.SourceTarget = null;
                context.Target = null;
                context.Direction = Vector3.up;
                context.IsChainCast = true;
                TriggerNextPhase(context, cascadeDelay);
            }
        }
    }
}