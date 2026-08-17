using System.Collections.Generic;
using UnityEngine;

namespace SpellSystem.Core
{
    public class SpellContext
    {
        public Transform Caster;
        public Vector3 HitPosition;
        public Transform Target;
        public Transform SourceTarget;
        public Vector3 Direction;
        public List<Transform> HitTargets = new List<Transform>();
        public bool IsHardLocked;

        public int ChargeLevel = 0;
        public float ChargeMultiplier = 1f;

        public bool IsChainCast = false;

        // НОВОЕ: Показывает, врожденная ли это атака (клик без рисования)
        public bool IsInnate = false;
    }
}