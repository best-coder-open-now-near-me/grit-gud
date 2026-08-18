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
        VehicleMove,
        ChangeTurnMode,
        ChangeEncounter,
        ObserveEncounter,
        Patrol,
    }

    public enum GameplaySemanticSubjectKind
    {
        Actor,
        DestructibleProp,
        Vehicle,
        Objective,
        WorldPosition,
        InventoryItem,
        Projectile,
        System,
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
            Subject(GameplaySemanticSubjectKind.Actor),
            Trait("path", "grounded"));

        public static GameplayCapabilityProfile TraversalMove() => Profile(
            GameplaySemanticCapability.Move,
            Subject(GameplaySemanticSubjectKind.Actor),
            Trait("path", "authored-traversal"));

        public static GameplayCapabilityProfile ChangeStance() => Profile(
            GameplaySemanticCapability.ChangeStance,
            Subject(GameplaySemanticSubjectKind.Actor),
            Trait("pose", "standing-crouched"));

        public static GameplayCapabilityProfile Attack(
            AttackDefinition attack) => Attack(
                attack,
                GameplaySemanticSubjectKind.Actor);

        public static GameplayCapabilityProfile Attack(
            AttackDefinition attack,
            GameplaySemanticSubjectKind subjectKind)
        {
            if (attack == null) throw new ArgumentNullException(nameof(attack));
            if (attack.Projectile != null)
            {
                RequireSubject(
                    subjectKind,
                    GameplaySemanticSubjectKind.Actor,
                    GameplaySemanticSubjectKind.DestructibleProp,
                    GameplaySemanticSubjectKind.Vehicle,
                    GameplaySemanticSubjectKind.WorldPosition);
                return Profile(
                    GameplaySemanticCapability.LaunchProjectile,
                    Subject(subjectKind),
                    Trait("delivery", "turn-flight"),
                    Trait("targeting", "semantic-subject"),
                    Trait("resource", "equipped-weapon"),
                    Trait("consequence", attack.Projectile.BlastRadius > 0f
                        ? "blast-actor-and-destructible"
                        : "impact"),
                    Trait("emergency", attack.Projectile
                        .OpensEmergencyReactionWindow ? "opens" : "none"));
            }

            if (attack.Contact != null
                && subjectKind != GameplaySemanticSubjectKind.Actor)
                throw new ArgumentException(
                    "Contact attacks require actor subjects.",
                    nameof(subjectKind));
            RequireSubject(
                subjectKind,
                GameplaySemanticSubjectKind.Actor,
                GameplaySemanticSubjectKind.DestructibleProp,
                GameplaySemanticSubjectKind.Vehicle,
                GameplaySemanticSubjectKind.WorldPosition);
            if (subjectKind == GameplaySemanticSubjectKind.DestructibleProp
                && attack.DirectFireDamage == null)
                throw new ArgumentException(
                    "Destructible subjects require direct-fire damage semantics.",
                    nameof(subjectKind));

            return Profile(
                GameplaySemanticCapability.DirectAttack,
                Subject(subjectKind),
                Trait("delivery", attack.Contact == null
                    ? "immediate-ranged"
                    : "contact"),
                Trait("targeting", "semantic-subject"),
                Trait("resource", "equipped-weapon"),
                Trait("consequence", DirectAttackConsequence(subjectKind)));
        }

        public static GameplayCapabilityProfile ThrowExplosive(
            ThrownExplosiveDefinition definition)
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));
            return Profile(
                GameplaySemanticCapability.ThrowExplosive,
                Subject(GameplaySemanticSubjectKind.WorldPosition),
                Trait("delivery", "ballistic-landing-query"),
                Trait("targeting", "world-area"),
                Trait("resource", "inventory-quantity"),
                Trait("consequence", definition.DeploysSmoke
                    ? "smoke-field"
                    : "blast-actor-and-destructible"));
        }

        public static GameplayCapabilityProfile Displace(
            DisplacementActionDefinition definition) => Displace(
                definition,
                (definition ?? throw new ArgumentNullException(nameof(definition)))
                    .AcceptedSubjects == DisplacementSubjectKinds.Prop
                        ? GameplaySemanticSubjectKind.DestructibleProp
                        : GameplaySemanticSubjectKind.Actor);

        public static GameplayCapabilityProfile Displace(
            DisplacementActionDefinition definition,
            GameplaySemanticSubjectKind subjectKind)
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));
            if (subjectKind == GameplaySemanticSubjectKind.Actor)
            {
                if (!definition.Accepts(DisplacementSubjectKind.Combatant))
                    throw new ArgumentException(
                        "The displacement action does not accept actor subjects.",
                        nameof(subjectKind));
            }
            else if (subjectKind == GameplaySemanticSubjectKind.DestructibleProp)
            {
                if (!definition.Accepts(DisplacementSubjectKind.Prop))
                    throw new ArgumentException(
                        "The displacement action does not accept prop subjects.",
                        nameof(subjectKind));
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(subjectKind));
            }
            return Profile(
                GameplaySemanticCapability.Displace,
                Subject(subjectKind),
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
            Subject(GameplaySemanticSubjectKind.InventoryItem),
            Trait("resource", "inventory-equipment"),
            Trait("mode", "equip-or-unequip"));

        public static GameplayCapabilityProfile Interact() => Profile(
            GameplaySemanticCapability.Interact,
            Subject(GameplaySemanticSubjectKind.Objective),
            Trait("targeting", "context-objective"),
            Trait("consequence", "objective-completion"));

        public static GameplayCapabilityProfile EndTurn(bool emergency) =>
            Profile(
                GameplaySemanticCapability.EndTurn,
                Subject(GameplaySemanticSubjectKind.Actor),
                Trait("turn", emergency ? "emergency" : "normal"));

        public static GameplayCapabilityProfile AdvanceProjectile() => Profile(
            GameplaySemanticCapability.AdvanceProjectile,
            Subject(GameplaySemanticSubjectKind.Projectile),
            Trait("evidence", "segment-query"),
            Trait("consequence", "impact-or-blast"));

        public static GameplayCapabilityProfile VehicleMove() => Profile(
            GameplaySemanticCapability.VehicleMove,
            Subject(GameplaySemanticSubjectKind.Vehicle),
            Trait("path", "momentum-envelope"),
            Trait("constraint", "speed-and-curvature"));

        public static GameplayCapabilityProfile AdvanceWorld(string mode) =>
            Profile(
                GameplaySemanticCapability.AdvanceWorld,
                Subject(GameplaySemanticSubjectKind.System),
                Trait("mode", mode));

        public static GameplayCapabilityProfile EmergencyReaction(
            string phase) => Profile(
            GameplaySemanticCapability.EmergencyReaction,
            Subject(GameplaySemanticSubjectKind.Actor),
                Trait("phase", phase));

        public static GameplayCapabilityProfile ChangeTurnMode(string mode) =>
            Profile(
            GameplaySemanticCapability.ChangeTurnMode,
            Subject(GameplaySemanticSubjectKind.System),
                Trait("mode", mode));

        public static GameplayCapabilityProfile ChangeEncounter(string mode) =>
            Profile(
            GameplaySemanticCapability.ChangeEncounter,
            Subject(GameplaySemanticSubjectKind.System),
                Trait("mode", mode));

        public static GameplayCapabilityProfile ObserveEncounter() =>
            Profile(
                GameplaySemanticCapability.ObserveEncounter,
                Subject(GameplaySemanticSubjectKind.Actor),
                Trait("evidence", "frozen-sight-and-sound"),
                Trait("consequence", "awareness-transition"));

        public static GameplayCapabilityProfile Patrol() => Profile(
            GameplaySemanticCapability.Patrol,
            Subject(GameplaySemanticSubjectKind.Actor),
            Trait("path", "authored-patrol-route"),
            Trait("consequence", "world-pose-advance"));

        private static GameplayCapabilityProfile Profile(
            GameplaySemanticCapability capability,
            params GameplayCapabilityTrait[] traits) =>
            new GameplayCapabilityProfile(capability, Version, traits);

        private static GameplayCapabilityTrait Trait(
            string name,
            string value) => new GameplayCapabilityTrait(name, value);

        public static GameplaySemanticSubjectKind GetSubjectKind(
            GameplayCapabilityProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (!Enum.TryParse(
                    profile.GetTrait("subject"),
                    ignoreCase: false,
                    out GameplaySemanticSubjectKind result))
                throw new ArgumentException(
                    $"Capability '{profile.Signature}' has an invalid subject kind.",
                    nameof(profile));
            return result;
        }

        private static GameplayCapabilityTrait Subject(
            GameplaySemanticSubjectKind kind) => Trait(
                "subject",
                kind.ToString());

        private static string DirectAttackConsequence(
            GameplaySemanticSubjectKind subjectKind)
        {
            switch (subjectKind)
            {
                case GameplaySemanticSubjectKind.Actor:
                    return "actor-wound";
                case GameplaySemanticSubjectKind.DestructibleProp:
                    return "destructible-damage";
                case GameplaySemanticSubjectKind.Vehicle:
                case GameplaySemanticSubjectKind.WorldPosition:
                    return "discharge-only";
                default:
                    throw new ArgumentOutOfRangeException(nameof(subjectKind));
            }
        }

        private static void RequireSubject(
            GameplaySemanticSubjectKind actual,
            params GameplaySemanticSubjectKind[] supported)
        {
            if (!Enum.IsDefined(typeof(GameplaySemanticSubjectKind), actual))
                throw new ArgumentOutOfRangeException(nameof(actual));
            foreach (GameplaySemanticSubjectKind candidate in supported)
                if (actual == candidate) return;
            throw new ArgumentException(
                $"Subject kind '{actual}' is not supported by this capability.",
                nameof(actual));
        }
    }
}
