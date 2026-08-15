using System.Collections.Generic;
using UnityEngine;

namespace SpellSystem.Core
{
    public class SpellContext
    {
        public Transform Caster;
        public Vector3 HitPosition;
        public Transform Target;          // Конечная цель (куда летим)
        public Transform SourceTarget;    // Источник (откуда вылетаем, чтобы игнорировать его)
        public Vector3 Direction;
        public List<Transform> HitTargets = new List<Transform>();
        public bool IsHardLocked;
    }
}