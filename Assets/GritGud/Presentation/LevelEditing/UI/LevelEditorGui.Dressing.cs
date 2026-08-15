using System;
using System.Linq;
using GritGud.Domain.Levels;
using GritGud.Presentation.Levels.Runtime;
using UnityEngine;

namespace GritGud.Presentation.LevelEditing.UI
{
    public sealed partial class LevelEditorGui
    {
        private LevelDressingTargetKind selectedDressingKind =
            LevelDressingTargetKind.Decal;
        private string selectedDressingId = string.Empty;
        private LevelDecalAuthoringRequest decalFields = new LevelDecalAuthoringRequest();
        private LevelAmbientVfxAuthoringRequest ambientVfxFields =
            new LevelAmbientVfxAuthoringRequest();
        private LevelAudioZoneAuthoringRequest audioZoneFields =
            new LevelAudioZoneAuthoringRequest();

        public void SyncDressingFields(LevelDocument document, bool force = false)
        {
            LevelDressingData dressing = document?.dressing;
            if (dressing == null)
                return;

            switch (selectedDressingKind)
            {
                case LevelDressingTargetKind.Decal:
                    LevelDecalData decal = dressing.decals.FirstOrDefault(value =>
                        string.Equals(value?.id, selectedDressingId, StringComparison.Ordinal));
                    if (decal != null && (force || !string.Equals(
                            decalFields.id,
                            decal.id,
                            StringComparison.Ordinal)))
                    {
                        decalFields = ToRequest(decal);
                    }
                    break;
                case LevelDressingTargetKind.AmbientVfx:
                    LevelAmbientVfxData effect = dressing.ambientVfx.FirstOrDefault(value =>
                        string.Equals(value?.id, selectedDressingId, StringComparison.Ordinal));
                    if (effect != null && (force || !string.Equals(
                            ambientVfxFields.id,
                            effect.id,
                            StringComparison.Ordinal)))
                    {
                        ambientVfxFields = ToRequest(effect);
                    }
                    break;
                case LevelDressingTargetKind.AudioZone:
                    LevelAudioZoneData zone = dressing.audioZones.FirstOrDefault(value =>
                        string.Equals(value?.id, selectedDressingId, StringComparison.Ordinal));
                    if (zone != null && (force || !string.Equals(
                            audioZoneFields.id,
                            zone.id,
                            StringComparison.Ordinal)))
                    {
                        audioZoneFields = ToRequest(zone);
                    }
                    break;
            }
        }

        public void SelectDressingItem(
            LevelDressingTargetKind kind,
            string id,
            LevelDocument document)
        {
            selectedDressingKind = kind;
            selectedDressingId = id ?? string.Empty;
            SyncDressingFields(document, force: true);
            presentationState.ShowPage(LevelEditorWorkspacePage.Dressing);
        }

        private void DrawDressing(LevelDocument document)
        {
            SyncDressingFields(document);
            LevelDressingData dressing = document.dressing;
            GUILayout.Space(LevelEditorGuiMetrics.SpaceSection);
            DrawSectionHeader("AUTHORING PREVIEW");
            bool previewAudio = GUILayout.Toggle(
                actions.AudioZonePreviewEnabled,
                "Preview ambient audio");
            if (previewAudio != actions.AudioZonePreviewEnabled)
                actions.SetAudioZonePreviewEnabled(previewAudio);
            GUILayout.Label("Blue boxes show audio zones in Edit mode. They are hidden in Preview and Test Play.");

            GUILayout.Space(LevelEditorGuiMetrics.SpaceSection);
            LevelDressingTargetKind previousKind = selectedDressingKind;
            selectedDressingKind = (LevelDressingTargetKind)GUILayout.Toolbar(
                (int)selectedDressingKind,
                new[] { "DECALS", "VFX", "AUDIO" });
            if (selectedDressingKind != previousKind)
                selectedDressingId = string.Empty;

            switch (selectedDressingKind)
            {
                case LevelDressingTargetKind.AmbientVfx:
                    DrawAmbientVfxSection(dressing);
                    break;
                case LevelDressingTargetKind.AudioZone:
                    DrawAudioZoneSection(dressing);
                    break;
                default:
                    DrawDecalSection(dressing);
                    break;
            }
        }

        private void DrawDecalSection(LevelDressingData dressing)
        {
            GUILayout.Space(LevelEditorGuiMetrics.SpaceSection);
            DrawSectionHeader(
                $"DECALS ({dressing.decals.Count}/{LevelDressingData.MaximumDecalCount})");
            foreach (LevelDecalData decal in dressing.decals.Where(value => value != null))
                DrawDressingItemButton(LevelDressingTargetKind.Decal, decal.id, decal.displayName);
            GUI.enabled = dressing.decals.Count < LevelDressingData.MaximumDecalCount;
            if (GUILayout.Button("+ ADD AT VIEW", PanelButtonLayout()))
                actions.AddDecal();
            GUI.enabled = true;

            LevelDecalData selected = selectedDressingKind == LevelDressingTargetKind.Decal
                ? dressing.decals.FirstOrDefault(value => string.Equals(
                    value?.id,
                    selectedDressingId,
                    StringComparison.Ordinal))
                : null;
            if (selected == null)
                return;
            GUILayout.Space(LevelEditorGuiMetrics.SpaceGroup);
            DrawLabeledField("Name", ref decalFields.displayName);
            decalFields.styleId = DrawChoice(
                "Style",
                decalFields.styleId,
                LevelDressingIds.DecalStyles);
            DrawVectorFields("Position", decalFields.position);
            DrawVectorFields("Rotation", decalFields.rotation);
            DrawVectorFields("Size", decalFields.size);
            DrawColorFields("Color RGB", decalFields.color);
            DrawLabeledField("Opacity", ref decalFields.alpha);
            if (GUILayout.Button("APPLY DECAL", PanelApplyButtonLayout()))
                actions.ApplyDecal(decalFields);
            DrawDeleteButton("DELETE DECAL", () => actions.DeleteDecal(selected.id));
        }

        private void DrawAmbientVfxSection(LevelDressingData dressing)
        {
            GUILayout.Space(LevelEditorGuiMetrics.SpaceSection);
            DrawSectionHeader(
                $"AMBIENT VFX ({dressing.ambientVfx.Count}/"
                + $"{LevelDressingData.MaximumAmbientVfxCount})");
            foreach (LevelAmbientVfxData effect in dressing.ambientVfx.Where(value => value != null))
                DrawDressingItemButton(LevelDressingTargetKind.AmbientVfx, effect.id, effect.displayName);
            GUI.enabled = dressing.ambientVfx.Count < LevelDressingData.MaximumAmbientVfxCount;
            if (GUILayout.Button("+ ADD AT VIEW", PanelButtonLayout()))
                actions.AddAmbientVfx();
            GUI.enabled = true;

            LevelAmbientVfxData selected = selectedDressingKind == LevelDressingTargetKind.AmbientVfx
                ? dressing.ambientVfx.FirstOrDefault(value => string.Equals(
                    value?.id,
                    selectedDressingId,
                    StringComparison.Ordinal))
                : null;
            if (selected == null)
                return;
            GUILayout.Space(LevelEditorGuiMetrics.SpaceGroup);
            DrawLabeledField("Name", ref ambientVfxFields.displayName);
            ambientVfxFields.effectId = DrawAmbientEffectChoice(ambientVfxFields.effectId);
            DrawVectorFields("Position", ambientVfxFields.position);
            DrawVectorFields("Rotation", ambientVfxFields.rotation);
            DrawVectorFields("Scale", ambientVfxFields.scale);
            if (GUILayout.Button("APPLY AMBIENT VFX", PanelApplyButtonLayout()))
                actions.ApplyAmbientVfx(ambientVfxFields);
            DrawDeleteButton(
                "DELETE AMBIENT VFX",
                () => actions.DeleteAmbientVfx(selected.id));
        }

        private void DrawAudioZoneSection(LevelDressingData dressing)
        {
            GUILayout.Space(LevelEditorGuiMetrics.SpaceSection);
            DrawSectionHeader(
                $"AUDIO ZONES ({dressing.audioZones.Count}/"
                + $"{LevelDressingData.MaximumAudioZoneCount})");
            foreach (LevelAudioZoneData zone in dressing.audioZones.Where(value => value != null))
                DrawDressingItemButton(LevelDressingTargetKind.AudioZone, zone.id, zone.displayName);
            GUI.enabled = dressing.audioZones.Count < LevelDressingData.MaximumAudioZoneCount;
            if (GUILayout.Button("+ ADD AT VIEW", PanelButtonLayout()))
                actions.AddAudioZone();
            GUI.enabled = true;

            LevelAudioZoneData selected = selectedDressingKind == LevelDressingTargetKind.AudioZone
                ? dressing.audioZones.FirstOrDefault(value => string.Equals(
                    value?.id,
                    selectedDressingId,
                    StringComparison.Ordinal))
                : null;
            if (selected == null)
                return;
            GUILayout.Space(LevelEditorGuiMetrics.SpaceGroup);
            DrawLabeledField("Name", ref audioZoneFields.displayName);
            audioZoneFields.soundId = DrawChoice(
                "Sound",
                audioZoneFields.soundId,
                LevelDressingIds.AmbientSounds);
            DrawVectorFields("Center", audioZoneFields.center);
            DrawVectorFields("Size", audioZoneFields.size);
            DrawLabeledField("Volume 0-1", ref audioZoneFields.volume);
            DrawLabeledField("Fade distance", ref audioZoneFields.fadeDistance);
            if (GUILayout.Button("APPLY AUDIO ZONE", PanelApplyButtonLayout()))
                actions.ApplyAudioZone(audioZoneFields);
            DrawDeleteButton(
                "DELETE AUDIO ZONE",
                () => actions.DeleteAudioZone(selected.id));
        }

        private void DrawDressingItemButton(
            LevelDressingTargetKind kind,
            string id,
            string displayName)
        {
            Color previous = GUI.backgroundColor;
            if (kind == selectedDressingKind
                && string.Equals(id, selectedDressingId, StringComparison.Ordinal))
            {
                GUI.backgroundColor = LevelEditorTheme.Active;
            }
            if (GUILayout.Button(displayName, PanelCompactButtonLayout()))
            {
                selectedDressingKind = kind;
                selectedDressingId = id;
            }
            GUI.backgroundColor = previous;
        }

        private string DrawAmbientEffectChoice(string selected)
        {
            GUILayout.Label("Effect");
            foreach (AmbientVfxDefinition definition in dressingCatalog.AmbientEffects)
            {
                if (definition == null)
                    continue;
                Color previous = GUI.backgroundColor;
                if (string.Equals(definition.EffectId, selected, StringComparison.Ordinal))
                    GUI.backgroundColor = LevelEditorTheme.Active;
                if (GUILayout.Button(definition.DisplayName, PanelCompactButtonLayout()))
                    selected = definition.EffectId;
                GUI.backgroundColor = previous;
            }
            return selected;
        }

        private static string DrawChoice(
            string label,
            string selected,
            System.Collections.Generic.IReadOnlyList<string> values)
        {
            GUILayout.Label(label);
            GUILayout.BeginHorizontal();
            foreach (string value in values)
            {
                Color previous = GUI.backgroundColor;
                if (string.Equals(value, selected, StringComparison.Ordinal))
                    GUI.backgroundColor = LevelEditorTheme.Active;
                if (GUILayout.Button(value.ToUpperInvariant(), PanelCompactButtonLayout()))
                    selected = value;
                GUI.backgroundColor = previous;
            }
            GUILayout.EndHorizontal();
            return selected;
        }

        private static void DrawDeleteButton(string label, Action delete)
        {
            Color previous = GUI.backgroundColor;
            GUI.backgroundColor = LevelEditorTheme.Warning;
            if (GUILayout.Button(label, PanelButtonLayout()))
                delete();
            GUI.backgroundColor = previous;
        }

        private static LevelDecalAuthoringRequest ToRequest(LevelDecalData value) =>
            new LevelDecalAuthoringRequest
            {
                id = value.id,
                displayName = value.displayName,
                styleId = value.styleId,
                position = ToText(value.position),
                rotation = ToText(value.rotationEuler),
                size = ToText(value.size),
                color = ToText(value.color),
                alpha = Format(value.color.a),
            };

        private static LevelAmbientVfxAuthoringRequest ToRequest(LevelAmbientVfxData value) =>
            new LevelAmbientVfxAuthoringRequest
            {
                id = value.id,
                displayName = value.displayName,
                effectId = value.effectId,
                position = ToText(value.position),
                rotation = ToText(value.rotationEuler),
                scale = ToText(value.scale),
            };

        private static LevelAudioZoneAuthoringRequest ToRequest(LevelAudioZoneData value) =>
            new LevelAudioZoneAuthoringRequest
            {
                id = value.id,
                displayName = value.displayName,
                soundId = value.soundId,
                center = ToText(value.center),
                size = ToText(value.size),
                volume = Format(value.volume),
                fadeDistance = Format(value.fadeDistance),
            };
    }
}
