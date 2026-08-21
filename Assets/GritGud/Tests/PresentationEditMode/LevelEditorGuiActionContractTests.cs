using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GritGud.Presentation.LevelEditing;
using GritGud.Presentation.LevelEditing.UI;
using NUnit.Framework;

namespace GritGud.Presentation.Tests
{
    public sealed class LevelEditorGuiActionContractTests
    {
        [Test]
        public void CompatibilityContractComposesSixCohesiveCapabilities()
        {
            Type[] capabilities = typeof(ILevelEditorGuiActions)
                .GetInterfaces();

            Assert.That(
                capabilities,
                Is.EquivalentTo(new[]
                {
                    typeof(ILevelEditorFileActions),
                    typeof(ILevelEditorHistoryActions),
                    typeof(ILevelEditorSelectionGroupActions),
                    typeof(ILevelEditorEnvironmentDressingActions),
                    typeof(ILevelEditorSpatialPlacementActions),
                    typeof(ILevelEditorPreviewTestActions),
                }));
        }

        [Test]
        public void CapabilitiesDoNotRedeclareEachOthersMembers()
        {
            Type[] capabilities = typeof(ILevelEditorGuiActions)
                .GetInterfaces();
            var owners = new Dictionary<string, Type>(StringComparer.Ordinal);

            foreach (Type capability in capabilities)
            {
                foreach (var member in capability.GetMembers())
                {
                    string signature = member.MemberType + ":" + member.Name;
                    Assert.That(
                        owners.TryGetValue(signature, out Type owner),
                        Is.False,
                        $"{signature} belongs to both {owner} and {capability}.");
                    owners.Add(signature, capability);
                }
            }
        }

        [Test]
        public void ControllerDoesNotOwnGuiActionCapabilities()
        {
            Type controller = typeof(LevelEditorController);

            foreach (Type capability in typeof(ILevelEditorGuiActions)
                .GetInterfaces())
            {
                Assert.That(capability.IsAssignableFrom(controller), Is.False);
            }
        }

        [Test]
        public void EveryCapabilityHasADedicatedAdapter()
        {
            var ownership = new Dictionary<Type, Type>
            {
                [typeof(ILevelEditorFileActions)] =
                    typeof(LevelEditorFileActions),
                [typeof(ILevelEditorHistoryActions)] =
                    typeof(LevelEditorHistoryActions),
                [typeof(ILevelEditorSelectionGroupActions)] =
                    typeof(LevelEditorSelectionGroupActions),
                [typeof(ILevelEditorEnvironmentDressingActions)] =
                    typeof(LevelEditorEnvironmentDressingActions),
                [typeof(ILevelEditorSpatialPlacementActions)] =
                    typeof(LevelEditorSpatialPlacementActions),
                [typeof(ILevelEditorPreviewTestActions)] =
                    typeof(LevelEditorPreviewTestActions),
            };

            Assert.That(
                ownership.Keys,
                Is.EquivalentTo(typeof(ILevelEditorGuiActions).GetInterfaces()));
            foreach (KeyValuePair<Type, Type> owner in ownership)
            {
                Assert.That(owner.Key.IsAssignableFrom(owner.Value), Is.True);
                Assert.That(
                    typeof(ILevelEditorGuiActions).IsAssignableFrom(owner.Value),
                    Is.False);
            }
        }

        [Test]
        public void GuiStoresNarrowCapabilitiesInsteadOfTheCompositeContract()
        {
            Type[] actionFieldTypes = typeof(LevelEditorGui)
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                .Select(field => field.FieldType)
                .Where(type => typeof(ILevelEditorGuiActions)
                    .IsAssignableFrom(type)
                    || type.Name.StartsWith(
                        "ILevelEditor",
                        StringComparison.Ordinal))
                .ToArray();

            Assert.That(
                actionFieldTypes,
                Is.EquivalentTo(typeof(ILevelEditorGuiActions)
                    .GetInterfaces()));
            Assert.That(
                actionFieldTypes,
                Has.None.EqualTo(typeof(ILevelEditorGuiActions)));
        }

        [Test]
        public void GuiConstructorRequiresNarrowCapabilitiesIndividually()
        {
            Type[] constructorParameters = typeof(LevelEditorGui)
                .GetConstructors()
                .Single()
                .GetParameters()
                .Select(parameter => parameter.ParameterType)
                .ToArray();

            Assert.That(
                constructorParameters,
                Has.None.EqualTo(typeof(ILevelEditorGuiActions)));
            Assert.That(
                constructorParameters.Intersect(
                    typeof(ILevelEditorGuiActions).GetInterfaces()),
                Is.EquivalentTo(typeof(ILevelEditorGuiActions).GetInterfaces()));
        }
    }
}
