using System.Collections.Generic;
using UnityEngine;
using SpellSystem.Data;
using SpellSystem.Testing;

namespace SpellSystem.Core
{
    public abstract class SpellNode
    {
        public SpellNode NextNode;
        public EnergyDataSO LayerEnergy;
        public abstract void Execute(SpellContext context);

        protected void TriggerNextPhase(SpellContext context)
        {
            if (NextNode != null) NextNode.Execute(context);
        }

        // Добавлен флаг requireTargets: если true, то при отсутствии целей каскад просто прекращается, 
        // не переключаясь на спавн из центра (тотема/кастера).
        protected void TriggerNextPhaseForEachTarget(SpellContext context, bool requireTargets = false)
        {
            if (NextNode == null) return;

            if (context.HitTargets.Count > 0)
            {
                foreach (var sourceTarget in context.HitTargets)
                {
                    if (sourceTarget == null) continue;

                    Transform nextTarget = FindNearestTarget(sourceTarget.position, sourceTarget);

                    Vector3 dir = nextTarget != null
                        ? (nextTarget.position - sourceTarget.position).normalized
                        : sourceTarget.forward;

                    SpellContext subContext = new SpellContext
                    {
                        Caster = context.Caster,
                        HitPosition = sourceTarget.position, // Позиция задетого врага
                        Target = nextTarget,                  // Ближайший сосед для прицела
                        SourceTarget = sourceTarget,          // Источник (для игнорирования коллизий)
                        Direction = dir,
                        HitTargets = new List<Transform>()
                    };

                    NextNode.Execute(subContext);
                }
            }
            else if (!requireTargets)
            {
                NextNode.Execute(context);
            }
        }

        protected Transform FindNearestTarget(Vector3 position, Transform excludeTarget)
        {
            Collider[] hits = Physics.OverlapSphere(position, 25f);
            Transform closest = null;
            float minDistance = float.MaxValue;

            foreach (var hit in hits)
            {
                DummyTarget dummy = hit.GetComponent<DummyTarget>();
                if (dummy != null && dummy.transform != excludeTarget)
                {
                    float dist = Vector3.Distance(position, dummy.transform.position);
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        closest = dummy.transform;
                    }
                }
            }
            return closest;
        }
    }

    // УЗЕЛ 1: ТРЕУГОЛЬНИК (СНАРЯД)
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
            Vector3 direction = context.Direction != Vector3.zero ? context.Direction : (context.Caster != null ? context.Caster.forward : Vector3.forward);

            var proj = GameObject.Instantiate(projectilePrefab, spawnPos, Quaternion.LookRotation(direction));

            float damage = LayerEnergy != null ? LayerEnergy.baseDamage : 10f;

            proj.Initialize(damage, LayerEnergy, context.Target, context.SourceTarget);

            proj.OnImpact += (hitPos, hitTarget) =>
            {
                context.HitPosition = hitPos;
                context.Target = hitTarget;

                if (hitTarget != null && !context.HitTargets.Contains(hitTarget))
                {
                    context.HitTargets.Add(hitTarget);
                }

                TriggerNextPhase(context);
            };
        }
    }

    // УЗЕЛ 2: КРУГ (АОЕ)
    public class CircleNode : SpellNode
    {
        private SpellAoE aoePrefab; // <--- Вот это поле должно быть объявлено

        public CircleNode(SpellAoE prefab, EnergyDataSO energy)
        {
            this.aoePrefab = prefab;
            this.LayerEnergy = energy;
        }

        public override void Execute(SpellContext context)
        {
            Vector3 spawnPos = Vector3.zero;

            // 1. ПРИОРИТЕТ: Если у нас есть захваченная цель, спавним АоЕ прямо на ней
            if (context.Target != null)
            {
                spawnPos = context.Target.position;
            }
            // 2. ВТОРИЧНО: Если цели нет, но есть HitPosition (например, от попавшего снаряда)
            else if (context.HitPosition != Vector3.zero)
            {
                spawnPos = context.HitPosition;
            }
            // 3. ПО УМОЛЧАНИЮ: Спавним перед кастером
            else if (context.Caster != null)
            {
                spawnPos = context.Caster.position + context.Caster.forward * 3f;
            }

            spawnPos.y += 0.1f; // Небольшой оффсет, чтобы не проваливалось под пол

            float damage = LayerEnergy != null ? LayerEnergy.baseDamage * 1.2f : 15f;
            float radius = 6f;

            // Сканируем врагов вокруг точки взрыва
            context.HitTargets.Clear();
            Collider[] hits = Physics.OverlapSphere(spawnPos, radius);
            foreach (var hit in hits)
            {
                DummyTarget dummy = hit.GetComponentInParent<DummyTarget>();
                if (dummy != null && !context.HitTargets.Contains(dummy.transform))
                {
                    context.HitTargets.Add(dummy.transform);
                }
            }

            if (aoePrefab != null)
            {
                var aoe = GameObject.Instantiate(aoePrefab, spawnPos, Quaternion.identity);
                aoe.Initialize(damage, radius, LayerEnergy);
            }

            Debug.Log($"[CircleNode] Взрыв AoE в точке {spawnPos}. Целей: {context.HitTargets.Count}");

            TriggerNextPhaseForEachTarget(context, requireTargets: true);
        }
    }

    // УЗЕЛ 3: КВАДРАТ (ТОТЕМ ИЛИ ПРОКЛЯТИЕ)
    public class SquareNode : SpellNode
    {
        private SpellShield shieldPrefab;
        private SpellTotem totemPrefab;

        public SquareNode(SpellShield shield, SpellTotem totem, EnergyDataSO energy)
        {
            this.shieldPrefab = shield;
            this.totemPrefab = totem;
            this.LayerEnergy = energy;
        }

        public override void Execute(SpellContext context)
        {
            // СЦЕНАРИЙ А: Есть ЖЕСТКИЙ лок (игрок явно тапнул по врагу) -> вешаем проклятие НА ВРАГА
            if (context.IsHardLocked && context.Target != null)
            {
                if (shieldPrefab != null)
                {
                    var shield = GameObject.Instantiate(shieldPrefab, context.Target.position, Quaternion.identity);
                    shield.Initialize(context.Target, LayerEnergy, () => ExecuteTick(context, context.Target.position, context.Target));
                    Debug.Log($"[SquareNode] Проклятие наложено на жестко залоченную цель: {context.Target.name}");
                }
                return;
            }

            // СЦЕНАРИЙ Б: Свободный режим / автозахват -> создаем ТОТЕМ на земле
            // Защита от нулевых координат: если Caster не задан или позиция нулевая, берем Vector3.up или центр сцены, 
            // но надежнее строить относительно кастера.
            Vector3 casterPos = (context.Caster != null) ? context.Caster.position : Vector3.zero;
            Vector3 forwardDir = (context.Caster != null) ? context.Caster.forward : Vector3.forward;

            // Если HitPosition валидная и не равна нулю, используем её, иначе спавним в 2 метрах перед игроком
            Vector3 spawnPos = (context.HitPosition != Vector3.zero) ? context.HitPosition : (casterPos + forwardDir * 2f);
            spawnPos.y = 0f; // Прижимаем к земле

            if (totemPrefab != null)
            {
                var totem = GameObject.Instantiate(totemPrefab, spawnPos, Quaternion.identity);
                totem.Initialize(LayerEnergy, () =>
                {
                    ExecuteTick(context, spawnPos, null);
                });

                Debug.Log($"[SquareNode] Тотем возведен в свободной зоне: {spawnPos}");
            }
            else
            {
                Debug.Log("[SquareNode] Префаб тотема не назначен, запуск мгновенного каскада.");
                TriggerNextPhaseForEachTarget(context);
            }
        }

        private void ExecuteTick(SpellContext context, Vector3 position, Transform sourceTarget)
        {
            if (NextNode == null) return;

            Transform nextTarget = FindNearestTarget(position, sourceTarget);
            Vector3 dir = nextTarget != null ? (nextTarget.position - position).normalized : Vector3.forward;

            SpellContext tickContext = new SpellContext
            {
                Caster = context.Caster,
                HitPosition = position,
                Target = nextTarget,
                SourceTarget = sourceTarget,
                Direction = dir,
                HitTargets = sourceTarget != null ? new List<Transform> { sourceTarget } : new List<Transform>()
            };

            NextNode.Execute(tickContext);
        }
    }
}