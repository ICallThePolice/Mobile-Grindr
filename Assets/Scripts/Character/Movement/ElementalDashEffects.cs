using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpellSystem.Testing;
using SpellSystem.Data;

namespace SpellSystem.Core
{
    [RequireComponent(typeof(PlayerMovement), typeof(PlayerDashSystem))]
    public class ElementalDashEffects : MonoBehaviour
    {
        [Header("- Vital (Life)")]
        [SerializeField] private float vitalBaseMultiplier = 1.3f;
        [SerializeField] private float vitalEnhancedMultiplier = 1.8f;
        [SerializeField] private float vitalBoostDuration = 1.5f;

        [Header("- Ereb (Darkness)")]
        [SerializeField] private Material erebGhostMaterial;
        [SerializeField] private float erebSpeedMultiplier = 1.3f;
        [SerializeField] private float erebBoostDuration = 2f;
        [SerializeField] private float erebRadius = 3f;
        [SerializeField] private float erebDamagePerTick = 3f;
        [SerializeField] private float erebTickInterval = 0.5f;

        [Header("- Psy (Teleport & Clone)")]
        [SerializeField] private Material psyCloneMaterial;
        [SerializeField] private GameObject psyAoEPrefab;
        [SerializeField] private float psyExplosionRadius = 4f;
        [SerializeField] private float psyBaseDamage = 15f;

        [Header("References")]
        [SerializeField] private Transform characterModel;

        private PlayerMovement playerMovement;
        private PlayerDashSystem dashSystem;
        private CharacterController controller;
        private Renderer[] playerRenderers;

        private Coroutine erebRoutine;
        private Coroutine vitalRoutine;
        private Dictionary<Renderer, Material[]> originalMaterials = new Dictionary<Renderer, Material[]>();

        private void Awake()
        {
            playerMovement = GetComponent<PlayerMovement>();
            dashSystem = GetComponent<PlayerDashSystem>();
            controller = GetComponent<CharacterController>();

            if (characterModel != null)
                playerRenderers = characterModel.GetComponentsInChildren<Renderer>();
        }

        public void TriggerDashEffect(string energyName, float chargeMultiplier, Vector3 dashDir, float dashDuration, float dashSpeed)
        {
            if (energyName.Contains("psy"))
            {
                ExecutePsyTeleport(chargeMultiplier, dashDir, dashDuration, dashSpeed);
            }
            else if (energyName.Contains("ereb"))
            {
                if (erebRoutine != null) StopCoroutine(erebRoutine);
                erebRoutine = StartCoroutine(ErebBoostRoutine());
            }
        }

        public void TriggerPostDashEffect(string energyName)
        {
            if (energyName.Contains("vital"))
            {
                if (vitalRoutine != null) StopCoroutine(vitalRoutine);
                vitalRoutine = StartCoroutine(VitalBoostRoutine());
            }
        }

        // --- ЛОГИКА PSY ---
        private void ExecutePsyTeleport(float chargeMultiplier, Vector3 dashDir, float dashDuration, float dashSpeed)
        {
            Vector3 startPos = transform.position;
            Quaternion startRot = characterModel != null ? characterModel.rotation : transform.rotation;

            float maxDist = (dashSpeed * dashDuration) * (1f + (chargeMultiplier - 1f) * 0.5f);
            Vector3 targetPos = startPos + dashDir * maxDist;

            if (Physics.SphereCast(startPos, controller.radius, dashDir, out RaycastHit hit, maxDist))
            {
                if (!hit.collider.isTrigger) targetPos = hit.point - dashDir * (controller.radius + 0.1f);
            }

            SpawnPsyClone(startPos, startRot, chargeMultiplier);

            controller.enabled = false;
            transform.position = targetPos;
            controller.enabled = true;
        }

        private void SpawnPsyClone(Vector3 pos, Quaternion rot, float chargeMult)
        {
            if (characterModel == null) return;

            GameObject cloneObj = new GameObject("PsyClone");
            cloneObj.transform.position = pos;
            cloneObj.transform.rotation = rot;

            List<Mesh> bakedMeshes = new List<Mesh>();
            SkinnedMeshRenderer[] smrs = characterModel.GetComponentsInChildren<SkinnedMeshRenderer>();

            foreach (var smr in smrs)
            {
                Mesh bakedMesh = new Mesh();
                smr.BakeMesh(bakedMesh);
                bakedMeshes.Add(bakedMesh);

                GameObject meshObj = new GameObject(smr.gameObject.name + "_Clone");
                meshObj.transform.SetParent(cloneObj.transform);
                meshObj.transform.localPosition = smr.transform.localPosition;
                meshObj.transform.localRotation = smr.transform.localRotation;
                meshObj.transform.localScale = smr.transform.localScale;

                MeshFilter mf = meshObj.AddComponent<MeshFilter>();
                mf.mesh = bakedMesh;

                MeshRenderer mr = meshObj.AddComponent<MeshRenderer>();
                mr.material = psyCloneMaterial != null ? psyCloneMaterial : smr.material;
            }

            StartCoroutine(PsyCloneTickRoutine(cloneObj, chargeMult, bakedMeshes));
        }

        private IEnumerator PsyCloneTickRoutine(GameObject clone, float chargeMult, List<Mesh> bakedMeshes)
        {
            float finalDamage = psyBaseDamage * chargeMult;
            float finalRadius = psyExplosionRadius * chargeMult;

            for (int i = 0; i < 3; i++)
            {
                yield return new WaitForSeconds(1f);
                if (clone == null) break;
                SpawnPsyExplosion(clone.transform.position, finalRadius, finalDamage, chargeMult);
            }

            if (clone != null) Destroy(clone);
            foreach (var m in bakedMeshes)
            {
                if (m != null) Destroy(m);
            }
        }

        private void SpawnPsyExplosion(Vector3 pos, float radius, float damage, float chargeMult)
        {
            EnergyDataSO currentEnergy = dashSystem.CurrentDashEnergy;

            if (psyAoEPrefab != null)
            {
                GameObject aoe = Instantiate(psyAoEPrefab, pos, Quaternion.identity);
                SpellAoE spellAoE = aoe.GetComponent<SpellAoE>();
                if (spellAoE != null && currentEnergy != null)
                {
                    spellAoE.Initialize(damage, radius, currentEnergy, chargeMult, 1);
                }
            }

            Collider[] hits = Physics.OverlapSphere(pos, radius);
            foreach (var hit in hits)
            {
                DummyTarget target = hit.GetComponentInParent<DummyTarget>();
                if (target != null)
                {
                    string enName = currentEnergy != null ? currentEnergy.name : "Psy";
                    Color enColor = currentEnergy != null ? currentEnergy.primaryColor : Color.white;
                    target.TakeDamage(damage, enName, enColor);
                }
            }
        }

        // --- ЛОГИКА VITAL ---
        private IEnumerator VitalBoostRoutine()
        {
            bool isEnemyNear = false;
            Collider[] hits = Physics.OverlapSphere(transform.position, 15f);
            foreach (var hit in hits)
            {
                if (hit.GetComponentInParent<DummyTarget>() != null)
                {
                    isEnemyNear = true;
                    break;
                }
            }

            float mult = isEnemyNear ? vitalEnhancedMultiplier : vitalBaseMultiplier;
            playerMovement.moveSpeed = playerMovement.OriginalMoveSpeed * mult;

            yield return new WaitForSeconds(vitalBoostDuration);
            playerMovement.moveSpeed = playerMovement.OriginalMoveSpeed;
        }

        // --- ЛОГИКА EREB ---
        private IEnumerator ErebBoostRoutine()
        {
            playerMovement.moveSpeed = playerMovement.OriginalMoveSpeed * erebSpeedMultiplier;
            SetErebMaterial(true);

            float startTime = Time.time;
            float nextErebTick = 0f;

            while (Time.time < startTime + erebBoostDuration)
            {
                if (Time.time >= nextErebTick)
                {
                    ApplyErebDamage();
                    nextErebTick = Time.time + erebTickInterval;
                }
                yield return null;
            }

            SetErebMaterial(false);
            playerMovement.moveSpeed = playerMovement.OriginalMoveSpeed;
        }

        private void ApplyErebDamage()
        {
            EnergyDataSO currentEnergy = dashSystem.CurrentDashEnergy;
            Collider[] hits = Physics.OverlapSphere(transform.position, erebRadius);

            foreach (var hit in hits)
            {
                DummyTarget target = hit.GetComponentInParent<DummyTarget>();
                if (target != null)
                {
                    string eName = currentEnergy != null ? currentEnergy.name : "Ereb";
                    Color eColor = currentEnergy != null ? currentEnergy.primaryColor : Color.white;
                    target.TakeDamage(erebDamagePerTick, eName, eColor);
                }
            }
        }

        private void SetErebMaterial(bool isActive)
        {
            if (playerRenderers == null) return;

            if (isActive && erebGhostMaterial != null)
            {
                originalMaterials.Clear();
                foreach (var rnd in playerRenderers)
                {
                    originalMaterials[rnd] = rnd.materials;
                    Material[] ghostMats = new Material[rnd.materials.Length];
                    for (int i = 0; i < ghostMats.Length; i++) ghostMats[i] = erebGhostMaterial;
                    rnd.materials = ghostMats;
                }
            }
            else
            {
                foreach (var kvp in originalMaterials)
                {
                    if (kvp.Key != null) kvp.Key.materials = kvp.Value;
                }
                originalMaterials.Clear();
            }
        }
    }
}