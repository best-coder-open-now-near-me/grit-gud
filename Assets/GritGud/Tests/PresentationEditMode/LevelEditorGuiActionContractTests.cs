using System;
using System.Collections.Generic;
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
        public void ControllerImplementsEveryCapabilityDirectly()
        {
            Type controller = typeof(LevelEditorController);

            foreach (Type capability in typeof(ILevelEditorGuiActions)
                .GetInterfaces())
            {
                Assert.That(capability.IsAssignableFrom(controller), Is.True);
            }
        }
    }
}
