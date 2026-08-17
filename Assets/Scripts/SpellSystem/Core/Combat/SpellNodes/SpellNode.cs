using System;
using SpellSystem.Data;
using SpellSystem.Testing;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpellSystem.Core
{

    // Базовый класс для всех узлов заклинаний
    public abstract class SpellNode
    {
        public SpellNode NextNode { get; set; }
        public EnergyDataSO LayerEnergy { get; set; }

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
}