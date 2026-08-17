using System.Collections.Generic;
using UnityEngine;
using SpellSystem.Data;

namespace SpellSystem.Core
{
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