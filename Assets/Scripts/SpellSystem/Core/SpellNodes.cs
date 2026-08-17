using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpellSystem.Data;
using SpellSystem.Testing;

namespace SpellSystem.Core
{
    // ------------------------- БАЗОВЫЙ УЗЕЛ -------------------------
    public abstract class SpellNode
    {
        public SpellNode NextNode;
        public EnergyDataSO LayerEnergy;
        public abstract void Execute(SpellContext context);

        protected void TriggerNextPhase(SpellContext context, float delay = 0f)
        {
            if (NextNode == null) return;

            if (delay > 0f && context.Caster != null && context.Caster.TryGetComponent(out MonoBehaviour mono))
                mono.StartCoroutine(DelayRoutine(() => NextNode.Execute(context), delay));
            else
                NextNode.Execute(context);
        }

        protected void TriggerNextPhaseForEachTarget(SpellContext context, bool requireTargets = false, float delay = 0f)
        {
            if (NextNode == null) return;

            if (context.HitTargets.Count > 0)
            {
                var mono = context.Caster != null ? context.Caster.GetComponent<MonoBehaviour>() : null;
                List<Transform> targets = new List<Transform>(context.HitTargets);

                Action cascadeAction = () =>
                {
                    foreach (var sourceTarget in targets)
                    {
                        if (sourceTarget == null) continue;

                        Transform nextTarget = FindNearestTarget(sourceTarget.position, sourceTarget);
                        Vector3 dir = nextTarget != null ? (nextTarget.position - sourceTarget.position).normalized : sourceTarget.forward;

                        SpellContext subContext = new SpellContext
                        {
                            Caster = context.Caster,
                            HitPosition = sourceTarget.position,
                            Target = nextTarget,
                            SourceTarget = sourceTarget,
                            Direction = dir,
                            HitTargets = new List<Transform>(),
                            ChargeLevel = context.ChargeLevel,
                            ChargeMultiplier = context.ChargeMultiplier,
                            IsChainCast = true,
                            IsInnate = context.IsInnate
                        };

                        NextNode.Execute(subContext);
                    }
                };

                if (mono != null && delay > 0f) mono.StartCoroutine(DelayRoutine(cascadeAction, delay));
                else cascadeAction.Invoke();
            }
            else if (!requireTargets)
            {
                TriggerNextPhase(context, delay);
            }
        }

        private IEnumerator DelayRoutine(Action action, float delay)
        {
            yield return new WaitForSeconds(delay);
            action?.Invoke();
        }

        protected Transform FindNearestTarget(Vector3 position, Transform excludeTarget, float searchRadius = 25f)
        {
            // ИСПРАВЛЕНИЕ: Используем переданный радиус вместо жестких 25f
            Collider[] hits = Physics.OverlapSphere(position, searchRadius);
            Transform closest = null;
            float minDistance = float.MaxValue;

            foreach (var hit in hits)
            {
                DummyTarget dummy = hit.GetComponentInParent<DummyTarget>();
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


    // ------------------------- ТРЕУГОЛЬНИК (СНАРЯД) -------------------------
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

            // Берем точку старта (которая теперь точно равна CastPoint)
            Vector3 spawnPos = context.HitPosition != Vector3.zero ? context.HitPosition : (context.Caster != null ? context.Caster.position : Vector3.zero);

            // Направление берем из контекста (переданное от CastPoint)
            Vector3 direction = context.Direction != Vector3.zero ? context.Direction.normalized : Vector3.forward;

            // Если есть конкретная захваченная цель - корректируем направление на неё
            if (context.Target != null && !context.IsChainCast)
            {
                direction = (context.Target.position - spawnPos).normalized;
            }

            float damage = LayerEnergy != null ? LayerEnergy.baseDamage : 10f;
            int projectilesCount = context.IsInnate ? 1 : 2; // Здесь в будущем будет влиять количество зарядов

            // Математически вычисляем вектор "вправо" относительно направления полета.
            // Это гарантирует, что снаряды выстроятся в ровную линию перпендикулярно выстрелу.
            Vector3 rightVector = Vector3.Cross(Vector3.up, direction).normalized;
            if (rightVector == Vector3.zero) rightVector = Vector3.right; // Защита от выстрела строго вверх/вниз

            for (int i = 0; i < projectilesCount; i++)
            {
                Vector3 currentDir = direction;
                Vector3 currentPos = spawnPos;

                // Универсальная логика выстраивания любого количества снарядов параллельно
                if (projectilesCount > 1 && !context.IsChainCast)
                {
                    float spacing = 0.8f; // Расстояние между снарядами
                    float offset = (i - (projectilesCount - 1) / 2f) * spacing;

                    // Сдвигаем старт строго вбок от линии прицеливания
                    currentPos += rightVector * offset;
                }

                var proj = GameObject.Instantiate(projectilePrefab, currentPos, Quaternion.LookRotation(currentDir));
                proj.Initialize(damage, LayerEnergy, context.Target, context.SourceTarget, context.ChargeMultiplier);

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


    // ------------------------- КРУГ (АОЕ) -------------------------
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

            if (context.IsChainCast)
            {
                if (context.SourceTarget != null) spawnPos = context.HitPosition;
                else if (context.Target != null) spawnPos = context.Target.position;
                else if (context.HitPosition != Vector3.zero) spawnPos = context.HitPosition;
            }
            else
            {
                if (context.IsHardLocked && context.Target != null) spawnPos = context.Target.position;
                else if (context.Caster != null)
                {
                    Transform autoTarget = FindNearestTarget(context.Caster.position, context.Caster);
                    if (autoTarget != null) spawnPos = autoTarget.position;
                    else spawnPos = context.HitPosition + context.Direction.normalized * 4f; // Спавним точно по линии прицеливания
                }
            }

            spawnPos.y += 0.1f;

            float damage = LayerEnergy != null ? LayerEnergy.baseDamage * 1.2f : 15f;
            float baseRadius = 4f;
            float finalRadius = baseRadius * context.ChargeMultiplier;

            context.HitTargets.Clear();
            Collider[] hits = Physics.OverlapSphere(spawnPos, finalRadius);
            foreach (var hit in hits)
            {
                DummyTarget dummy = hit.GetComponentInParent<DummyTarget>();
                if (dummy != null && !context.HitTargets.Contains(dummy.transform))
                    context.HitTargets.Add(dummy.transform);
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


    // ------------------------- КВАДРАТ (ТОТЕМ / ПРОКЛЯТИЕ) -------------------------
    // ------------------------- КВАДРАТ (ТОТЕМ / ПРОКЛЯТИЕ) -------------------------
    public class SquareNode : SpellNode
    {
        private SpellDebuff debuffPrefab;
        private SpellTotem totemPrefab;
        private SpellProjectile projectilePrefab;

        public SquareNode(SpellDebuff debuff, SpellTotem totem, SpellProjectile proj, EnergyDataSO energy)
        {
            this.debuffPrefab = debuff;
            this.totemPrefab = totem;
            this.projectilePrefab = proj;
            this.LayerEnergy = energy;
        }

        public override void Execute(SpellContext context)
        {
            // --- ЛОГИКА ВЫБОРА: ДЕБАФФ ИЛИ ТОТЕМ ---

            // Условие для Дебаффа: Либо это цепная реакция (попал снаряд), 
            // ЛИБО игрок кастует Квадрат напрямую, имея захваченную цель (Hard Lock).
            bool shouldCastDebuff = context.IsChainCast || (context.IsHardLocked && context.Target != null);

            // СЦЕНАРИЙ 1: ПРОКЛЯТИЕ (ДОТ НА ВРАГЕ)
            if (shouldCastDebuff)
            {
                Transform target = context.SourceTarget;
                if (target == null) target = context.Target;

                if (target != null && debuffPrefab != null)
                {
                    float damage = LayerEnergy != null ? LayerEnergy.baseDamage : 10f;
                    var debuff = GameObject.Instantiate(debuffPrefab, target.position, Quaternion.identity);

                    debuff.Initialize(target, damage, LayerEnergy, context.ChargeMultiplier, () =>
                    {
                        // Дебафф больше не турель! Он просто передает комбо дальше (например, взрывает Круг),
                        // если после него были нарисованы еще фигуры.
                        if (NextNode != null)
                        {
                            SpellContext tickContext = new SpellContext
                            {
                                Caster = context.Caster,
                                HitPosition = target.position,
                                Target = target, // Эпицентром остается сам враг
                                SourceTarget = target,
                                Direction = Vector3.up,
                                HitTargets = new List<Transform>(),
                                ChargeLevel = context.ChargeLevel,
                                ChargeMultiplier = context.ChargeMultiplier,
                                IsChainCast = true,
                                IsInnate = true
                            };
                            NextNode.Execute(tickContext);
                        }
                    });
                }
                return; // Дебафф наложен, выходим из метода
            }

            // СЦЕНАРИЙ 2: ТОТЕМ (ОРБИТА ВОКРУГ ИГРОКА)
            // Срабатывает только если это прямой каст БЕЗ захваченной цели.
            if (context.Caster != null)
            {
                SpellTotem existingTotem = FindExistingTotem(context.Caster.position, 5f);

                if (existingTotem != null)
                {
                    existingTotem.MutateTotem(LayerEnergy);
                }
                else
                {
                    if (totemPrefab != null)
                    {
                        var totem = GameObject.Instantiate(totemPrefab, context.Caster.position, Quaternion.identity);
                        totem.Initialize(LayerEnergy, context.ChargeMultiplier, context.Caster, () =>
                        {
                            // А вот Тотем остается турелью и продолжает выстреливать снаряды по врагам вокруг
                            if (totem != null)
                                ExecuteTotemTick(context, totem.transform);
                        });
                    }
                }
            }
        }

        private SpellTotem FindExistingTotem(Vector3 position, float radius)
        {
            Collider[] hits = Physics.OverlapSphere(position, radius);
            foreach (var hit in hits)
            {
                SpellTotem totem = hit.GetComponent<SpellTotem>();
                if (totem != null) return totem;
            }
            return null;
        }

        private void ExecuteTotemTick(SpellContext context, Transform sourceTransform)
        {
            if (sourceTransform == null) return;

            Transform nextTarget = FindNearestTarget(sourceTransform.position, sourceTransform, 15f);
            if (nextTarget == null) return;

            Vector3 dir = (nextTarget.position - sourceTransform.position).normalized;
            Vector3 totemShootPos = sourceTransform.position;

            SpellContext tickContext = new SpellContext
            {
                Caster = sourceTransform,
                HitPosition = totemShootPos,
                Target = nextTarget,
                SourceTarget = sourceTransform,
                Direction = dir,
                HitTargets = new List<Transform>(),
                ChargeLevel = context.ChargeLevel,
                ChargeMultiplier = context.ChargeMultiplier,
                IsChainCast = false,
                IsInnate = context.IsInnate
            };

            if (NextNode == null)
            {
                TriangleNode basicProj = new TriangleNode(projectilePrefab, LayerEnergy);
                tickContext.IsInnate = true;
                basicProj.Execute(tickContext);
            }
            else if (NextNode is SquareNode)
            {
                TriangleNode deliveryProj = new TriangleNode(projectilePrefab, LayerEnergy);
                deliveryProj.NextNode = NextNode;
                tickContext.IsInnate = true;
                deliveryProj.Execute(tickContext);
            }
            else
            {
                NextNode.Execute(tickContext);
            }
        }
    }
}