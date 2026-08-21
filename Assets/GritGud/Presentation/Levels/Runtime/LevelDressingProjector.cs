using System;
using System.Collections.Generic;
using GritGud.Domain.Levels;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GritGud.Presentation.Levels.Runtime
{
    public sealed class LevelDressingProjector : IDisposable
    {
        private static readonly int DecalColor = Shader.PropertyToID("_Color");
        private static readonly int DecalStyle = Shader.PropertyToID("_Style");

        private readonly Transform parent;
        private readonly LevelDressingCatalog catalog;
        private readonly List<Material> materials = new List<Material>();
        private readonly List<AudioClip> audioClips = new List<AudioClip>();
        private readonly List<AmbientAudioZoneController> audioZones =
            new List<AmbientAudioZoneController>();
        private GameObject root;
        private LevelDressingData current = new LevelDressingData();
        private bool showAudioZoneGizmos;
        private bool audioPlaybackEnabled;

        public LevelDressingProjector(Transform parent, LevelDressingCatalog catalog)
        {
            this.parent = parent != null ? parent : throw new ArgumentNullException(nameof(parent));
            this.catalog = catalog != null ? catalog : throw new ArgumentNullException(nameof(catalog));
        }

        public void Replace(
            LevelDressingData source,
            bool showZoneGizmos,
            bool playAudio)
        {
            LevelDressingData data = source?.DeepCopy()
                ?? throw new ArgumentNullException(nameof(source));
            data.Normalize();
            var replacement = new GameObject("Level Dressing");
            replacement.SetActive(false);
            replacement.transform.SetParent(parent, false);
            var replacementMaterials = new List<Material>();
            var replacementClips = new List<AudioClip>();
            var replacementZones = new List<AmbientAudioZoneController>();
            try
            {
                Material decalMaterial = data.decals.Count > 0
                    ? CreateDecalMaterial(replacementMaterials)
                    : null;
                foreach (LevelDecalData decal in data.decals)
                    CreateDecal(replacement.transform, decal, decalMaterial);
                foreach (LevelAmbientVfxData effect in data.ambientVfx)
                    CreateAmbientVfx(replacement.transform, effect);
                Material gizmoMaterial = showZoneGizmos && data.audioZones.Count > 0
                    ? CreateGizmoMaterial(replacementMaterials)
                    : null;
                var clipsById = new Dictionary<string, AudioClip>(StringComparer.Ordinal);
                foreach (LevelAudioZoneData zone in data.audioZones)
                {
                    AudioClip clip = null;
                    if (playAudio && !clipsById.TryGetValue(zone.soundId, out clip))
                    {
                        clip = ProceduralAmbientAudioFactory.Create(zone.soundId);
                        clipsById.Add(zone.soundId, clip);
                        replacementClips.Add(clip);
                    }
                    AmbientAudioZoneController controller = CreateAudioZone(
                        replacement.transform,
                        zone,
                        clip,
                        playAudio,
                        gizmoMaterial);
                    replacementZones.Add(controller);
                }
            }
            catch
            {
                Destroy(replacement);
                DestroyAll(replacementMaterials);
                DestroyAll(replacementClips);
                throw;
            }

            bool visible = root == null || root.activeSelf;
            GameObject previousRoot = root;
            root = replacement;
            root.SetActive(visible);
            Destroy(previousRoot);
            DestroyAll(materials);
            DestroyAll(audioClips);
            materials.Clear();
            materials.AddRange(replacementMaterials);
            audioClips.Clear();
            audioClips.AddRange(replacementClips);
            audioZones.Clear();
            audioZones.AddRange(replacementZones);
            current = data;
            showAudioZoneGizmos = showZoneGizmos;
            audioPlaybackEnabled = playAudio;
        }

        public void SetVisible(bool visible)
        {
            if (root != null)
                root.SetActive(visible);
        }

        public void SetEditorPresentation(bool showZoneGizmos, bool playAudio)
        {
            if (showAudioZoneGizmos == showZoneGizmos
                && audioPlaybackEnabled == playAudio)
            {
                return;
            }
            Replace(current, showZoneGizmos, playAudio);
        }

        public void Dispose()
        {
            Destroy(root);
            root = null;
            DestroyAll(materials);
            DestroyAll(audioClips);
            materials.Clear();
            audioClips.Clear();
            audioZones.Clear();
        }

        private static Material CreateDecalMaterial(ICollection<Material> owned)
        {
            Shader shader = Shader.Find("GritGud/SurfaceDecal");
            if (shader == null)
                throw new InvalidOperationException("The surface-decal shader is unavailable.");
            var material = new Material(shader)
            {
                name = "Authored Level Decals",
                hideFlags = HideFlags.HideAndDontSave,
            };
            owned.Add(material);
            return material;
        }

        private static Material CreateGizmoMaterial(ICollection<Material> owned)
        {
            Material material = RuntimeMaterialFactory.CreateColor(
                new Color(0.2f, 0.8f, 1f, 0.8f),
                "Audio Zone Gizmos");
            owned.Add(material);
            return material;
        }

        private static void CreateDecal(
            Transform parent,
            LevelDecalData data,
            Material material)
        {
            GameObject decal = GameObject.CreatePrimitive(PrimitiveType.Quad);
            decal.name = data.displayName;
            decal.transform.SetParent(parent, false);
            decal.transform.SetPositionAndRotation(
                ToVector(data.position),
                Quaternion.Euler(ToVector(data.rotationEuler)));
            decal.transform.localScale = new Vector3(data.size.x, data.size.y, 1f);
            Collider collider = decal.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
                Destroy(collider);
            }
            MeshRenderer renderer = decal.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            var properties = new MaterialPropertyBlock();
            properties.SetColor(DecalColor, ToColor(data.color));
            properties.SetFloat(DecalStyle, DecalStyleIndex(data.styleId));
            renderer.SetPropertyBlock(properties);
        }

        private void CreateAmbientVfx(Transform parent, LevelAmbientVfxData data)
        {
            if (!catalog.TryGetAmbientEffect(data.effectId, out AmbientVfxDefinition definition)
                || definition.Prefab == null)
            {
                throw new InvalidOperationException(
                    $"Ambient VFX '{data.effectId}' is unavailable in the dressing catalog.");
            }
            GameObject effect = Object.Instantiate(
                definition.Prefab,
                ToVector(data.position),
                Quaternion.Euler(ToVector(data.rotationEuler)),
                parent);
            effect.name = data.displayName;
            effect.transform.localScale = Vector3.Scale(
                definition.Prefab.transform.localScale,
                ToVector(data.scale));
            foreach (ParticleSystem particles in effect.GetComponentsInChildren<ParticleSystem>(true))
                particles.Play(withChildren: true);
        }

        private static AmbientAudioZoneController CreateAudioZone(
            Transform parent,
            LevelAudioZoneData data,
            AudioClip clip,
            bool playAudio,
            Material gizmoMaterial)
        {
            var zoneObject = new GameObject(data.displayName);
            zoneObject.transform.SetParent(parent, false);
            AmbientAudioZoneController controller =
                zoneObject.AddComponent<AmbientAudioZoneController>();
            controller.Initialize(data, clip, playAudio);
            if (gizmoMaterial != null)
                CreateBoxGizmo(zoneObject.transform, ToVector(data.size), gizmoMaterial);
            return controller;
        }

        private static void CreateBoxGizmo(Transform parent, Vector3 size, Material material)
        {
            Vector3 half = size * 0.5f;
            Vector3[] corners =
            {
                new Vector3(-half.x, -half.y, -half.z),
                new Vector3(half.x, -half.y, -half.z),
                new Vector3(half.x, -half.y, half.z),
                new Vector3(-half.x, -half.y, half.z),
                new Vector3(-half.x, half.y, -half.z),
                new Vector3(half.x, half.y, -half.z),
                new Vector3(half.x, half.y, half.z),
                new Vector3(-half.x, half.y, half.z),
            };
            int[,] edges =
            {
                { 0, 1 }, { 1, 2 }, { 2, 3 }, { 3, 0 },
                { 4, 5 }, { 5, 6 }, { 6, 7 }, { 7, 4 },
                { 0, 4 }, { 1, 5 }, { 2, 6 }, { 3, 7 },
            };
            for (int index = 0; index < edges.GetLength(0); index++)
            {
                var edge = new GameObject("Zone Edge");
                edge.transform.SetParent(parent, false);
                LineRenderer line = edge.AddComponent<LineRenderer>();
                line.useWorldSpace = false;
                line.positionCount = 2;
                line.widthMultiplier = 0.035f;
                line.sharedMaterial = material;
                line.SetPosition(0, corners[edges[index, 0]]);
                line.SetPosition(1, corners[edges[index, 1]]);
            }
        }

        private static float DecalStyleIndex(string styleId)
        {
            for (int index = 0; index < LevelDressingIds.DecalStyles.Count; index++)
            {
                if (string.Equals(
                    LevelDressingIds.DecalStyles[index],
                    styleId,
                    StringComparison.Ordinal))
                {
                    return index;
                }
            }
            return 0f;
        }

        private static Vector3 ToVector(Float3Data value) =>
            new Vector3(value.x, value.y, value.z);

        private static Color ToColor(FloatColorData value) =>
            new Color(value.r, value.g, value.b, value.a);

        private static void DestroyAll<T>(IEnumerable<T> values) where T : Object
        {
            foreach (T value in values)
                Destroy(value);
        }

        private static void Destroy(Object value)
        {
            if (value == null)
                return;
            if (UnityEngine.Application.isPlaying)
                Object.Destroy(value);
            else
                Object.DestroyImmediate(value);
        }
    }
}
