using System;
using System.Collections.Generic;
using System.Text;
using GritGud.Domain.Gameplay;

namespace GritGud.Application.Gameplay
{
    public enum GameplaySemanticCapability
    {
        Move,
        ChangeStance,
        DirectAttack,
        LaunchProjectile,
        ThrowExplosive,
        Displace,
        Equip,
        Interact,
        EndTurn,
        AdvanceProjectile,
        AdvanceWorld,
        EmergencyReaction,
    }

    public readonly struct GameplayCapabilityTrait
    {
        public GameplayCapabilityTrait(string name, string value)
        {
            Name = GameplayContentIdentity.RequireText(name, nameof(name));
            Value = GameplayContentIdentity.RequireText(value, nameof(value));
        }

        public string Name { get; }
        public string Value { get; }
    }

    public sealed class GameplayCapabilityProfile : IEquatable<
        GameplayCapabilityProfile>
    {
        private readonly string signature;

        public GameplayCapabilityProfile(
            GameplaySemanticCapability capability,
            int semanticVersion,
            IEnumerable<GameplayCapabilityTrait> traits)
        {
            if (!Enum.IsDefined(typeof(GameplaySemanticCapability), capability))
                throw new ArgumentOutOfRangeException(nameof(capability));
            if (semanticVersion <= 0)
                throw new ArgumentOutOfRangeException(nameof(semanticVersion));
            Capability = capability;
            SemanticVersion = semanticVersion;
            Traits = CopyTraits(traits);
            signature = BuildSignature(capability, semanticVersion, Traits);
        }

        public GameplaySemanticCapability Capability { get; }
        public int SemanticVersion { get; }
        public IReadOnlyList<GameplayCapabilityTrait> Traits { get; }
        public string Signature => signature;

        public string GetTrait(string name)
        {
            foreach (GameplayCapabilityTrait trait in Traits)
                if (string.Equals(trait.Name, name, StringComparison.Ordinal))
                    return trait.Value;
            throw new KeyNotFoundException(
                $"Capability trait '{name}' is not defined by '{signature}'.");
        }

        public bool Equals(GameplayCapabilityProfile other) =>
            other != null
            && string.Equals(signature, other.signature, StringComparison.Ordinal);

        public override bool Equals(object obj) =>
            Equals(obj as GameplayCapabilityProfile);

        public override int GetHashCode() =>
            StringComparer.Ordinal.GetHashCode(signature);

        private static IReadOnlyList<GameplayCapabilityTrait> CopyTraits(
            IEnumerable<GameplayCapabilityTrait> traits)
        {
            if (traits == null) throw new ArgumentNullException(nameof(traits));
            var copy = new List<GameplayCapabilityTrait>(traits);
            copy.Sort((left, right) => StringComparer.Ordinal.Compare(
                left.Name,
                right.Name));
            for (int index = 1; index < copy.Count; index++)
                if (string.Equals(
                    copy[index - 1].Name,
                    copy[index].Name,
                    StringComparison.Ordinal))
                    throw new ArgumentException(
                        $"Capability trait '{copy[index].Name}' is duplicated.",
                        nameof(traits));
            return copy.AsReadOnly();
        }

        private static string BuildSignature(
            GameplaySemanticCapability capability,
            int version,
            IReadOnlyList<GameplayCapabilityTrait> traits)
        {
            var text = new StringBuilder();
            text.Append(capability).Append("@v").Append(version);
            foreach (GameplayCapabilityTrait trait in traits)
            {
                text.Append(';')
                    .Append(trait.Name)
                    .Append('=')
                    .Append(trait.Value);
            }
            return text.ToString();
        }
    }

    public static class GameplayCapabilityProfiles
    {
        private const int Version = 1;

        public static GameplayCapabilityProfile GroundedMove() => Profile(
            GameplaySemanticCapability.Move,
            Trait("path", "grounded"));

        public static GameplayCapabilityProfile TraversalMove() => Profile(
            GameplaySemanticCapability.Move,
            Trait("path", "authored-traversal"));

        public static GameplayCapabilityProfile ChangeStance() => Profile(
            GameplaySemanticCapability.ChangeStance,
            Trait("pose", "standing-crouched"));

        public static GameplayCapabilityProfile Attack(AttackDefinition attack)
        {
            if (attack == null) throw new ArgumentNullException(nameof(attack));
            if (attack.Projectile != null)
            {
                return Profile(
                    GameplaySemanticCapability.LaunchProjectile,
                    Trait("delivery", "turn-flight"),
                    Trait("targeting", "actor-or-world-point"),
                    Trait("resource", "equipped-weapon"),
                    Trait("consequence", attack.Projectile.BlastRadius > 0f
                        ? "blast-actor-and-destructible"
                        : "impact"),
                    Trait("emergency", attack.Projectile
                        .OpensEmergencyReactionWindow ? "opens" : "none"));
            }

            return Profile(
                GameplaySemanticCapability.DirectAttack,
                Trait("delivery", attack.Contact == null
                    ? "immediate-ranged"
                    : "contact"),
                Trait("targeting", attack.Contact == null
                    ? "actor-or-world-point"
                    : "actor"),
                Trait("resource", "equipped-weapon"),
                Trait("consequence", attack.DirectFireDamage == null
                    ? "actor-wound"
                    : "actor-wound-or-destructible-damage"));
        }

        public static GameplayCapabilityProfile ThrowExplosive(
            ThrownExplosiveDefinition definition)
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));
            return Profile(
                GameplaySemanticCapability.ThrowExplosive,
                Trait("delivery", "ballistic-landing-query"),
                Trait("targeting", "world-area"),
                Trait("resource", "inventory-quantity"),
                Trait("consequence", definition.DeploysSmoke
                    ? "smoke-field"
                    : "blast-actor-and-destructible"));
        }

        public static GameplayCapabilityProfile Displace(
            DisplacementActionDefinition definition)
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));
            return Profile(
                GameplaySemanticCapability.Displace,
                Trait("intent", definition.Intent.ToString()),
                Trait("subjects", definition.AcceptedSubjects.ToString()),
                Trait("contest", definition.ContestPolicy.ToString()),
                Trait("results", definition.AllowedResults.ToString()),
                Trait("hands", definition.HandRequirement.ToString()),
                Trait("auto-stow", definition.AutoStowPolicy.ToString()),
                Trait("distance", definition.DistanceDecay == null
                    ? "fixed"
                    : "mass-decay"));
        }

        public static GameplayCapabilityProfile Equip() => Profile(
            GameplaySemanticCapability.Equip,
            Trait("resource", "inventory-equipment"),
            Trait("mode", "equip-or-unequip"));

        public static GameplayCapabilityProfile Interact() => Profile(
            GameplaySemanticCapability.Interact,
            Trait("targeting", "context-objective"),
            Trait("consequence", "objective-completion"));

        public static GameplayCapabilityProfile EndTurn(bool emergency) =>
            Profile(
                GameplaySemanticCapability.EndTurn,
                Trait("turn", emergency ? "emergency" : "normal"));

        private static GameplayCapabilityProfile Profile(
            GameplaySemanticCapability capability,
            params GameplayCapabilityTrait[] traits) =>
            new GameplayCapabilityProfile(capability, Version, traits);

        private static GameplayCapabilityTrait Trait(
            string name,
            string value) => new GameplayCapabilityTrait(name, value);
    }
}
