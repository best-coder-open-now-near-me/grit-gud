using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using GritGud.Domain.Gameplay;

namespace GritGud.Application.Gameplay
{
    /// <summary>
    /// Rehydrates the exact reducer trajectory already embedded in a strict
    /// battle artifact. It validates canonical transition bytes and resulting
    /// states without regenerating tactical candidates or rerunning policy.
    /// </summary>
    public static class GameplayBattleArtifactReplayLoader
    {
        public static GameplaySemanticReplayTimeline Load(
            GameplayBattleArtifact artifact)
        {
            if (artifact == null) throw new ArgumentNullException(
                nameof(artifact));
            GameplayBattleArtifactContent content = artifact.Content;
            bool migratesLegacyInjuries = content.ExecutionIdentity.Gameplay
                .RulesSchemaVersion < GameplayCombatStateSnapshot
                    .CurrentSchemaVersion;
            GameplayCombatStateSnapshot initial = CanonicalReader.Read<
                GameplayCombatStateSnapshot>(
                    content.InitialStateCanonical,
                    requireCanonicalRoundTrip: !migratesLegacyInjuries);
            if (!migratesLegacyInjuries)
                RequireEqual(
                    "initial state hash",
                    content.InitialStateHash,
                    initial.CanonicalHash);

            var trajectory = new List<GameplayTrajectoryStep>(
                content.Transitions.Count);
            var resultingStates = new List<GameplayCombatStateSnapshot>(
                content.Transitions.Count);
            var domainEvents = new List<
                IReadOnlyList<GameplayDomainEvent>>(
                    content.Transitions.Count);
            foreach (GameplayBattleArtifactTransition recorded in
                content.Transitions)
            {
                GameplaySemanticTransition transition = CanonicalReader.Read<
                    GameplaySemanticTransition>(
                        recorded.TransitionCanonical,
                        requireCanonicalRoundTrip: false);
                GameplayCombatStateSnapshot resulting = CanonicalReader.Read<
                    GameplayCombatStateSnapshot>(
                        recorded.ResultingStateCanonical,
                        requireCanonicalRoundTrip: !migratesLegacyInjuries);
                if (!migratesLegacyInjuries)
                    RequireEqual(
                        "transition[" + resultingStates.Count
                            + "].state hash",
                        recorded.ResultingStateHash,
                        resulting.CanonicalHash);
                trajectory.Add(new GameplayTrajectoryStep(
                    transition,
                    migratesLegacyInjuries
                        ? resulting.CanonicalHash
                        : recorded.ResultingStateHash,
                    recorded.DomainEventTypes,
                    recorded.TransitionPayloadDigest));
                resultingStates.Add(resulting);

                var transitionEvents = new List<GameplayDomainEvent>(
                    recorded.DomainEventTypes.Count);
                for (int eventIndex = 0;
                    eventIndex < recorded.DomainEventTypes.Count;
                    eventIndex++)
                {
                    GameplayDomainEvent domainEvent = CanonicalReader.Read<
                        GameplayDomainEvent>(
                            recorded.DomainEventPayloadsCanonical[eventIndex],
                            requireCanonicalRoundTrip: false);
                    RequireEqual(
                        "transition[" + (resultingStates.Count - 1)
                            + "].event[" + eventIndex + "].type",
                        recorded.DomainEventTypes[eventIndex],
                        domainEvent.EventType);
                    transitionEvents.Add(domainEvent);
                }
                domainEvents.Add(transitionEvents.AsReadOnly());
            }

            GameplaySemanticReplayTimeline replay =
                GameplaySemanticReplayTimeline.FromRecordedArtifact(
                initial,
                trajectory,
                resultingStates,
                domainEvents);
            if (!migratesLegacyInjuries)
                RequireEqual(
                    "terminal state hash",
                    content.Terminal.FinalStateHash,
                    replay.FinalState.CanonicalHash);
            return replay;
        }

        private static void RequireEqual(
            string name,
            string expected,
            string actual)
        {
            if (!string.Equals(expected, actual, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Battle artifact replay does not match its " + name + ".");
        }

        private static class CanonicalReader
        {
            private static readonly Assembly[] AllowedAssemblies =
            {
                typeof(GameplayBattleArtifact).Assembly,
                typeof(ActorStance).Assembly,
            };

            public static T Read<T>(
                string canonical,
                bool requireCanonicalRoundTrip = true)
            {
                GameplayBattleArtifactCodec.JsonNode node =
                    new GameplayBattleArtifactCodec.Parser(canonical).Parse();
                object value = ReadValue(node, typeof(T), typeof(T).Name);
                string roundTrip = GameplayReproBundleFormatter
                    .FormatCanonicalValue(value);
                if (requireCanonicalRoundTrip
                    && !string.Equals(
                        canonical,
                        roundTrip,
                        StringComparison.Ordinal))
                {
                    int difference = FirstDifference(canonical, roundTrip);
                    int laterDifference = FirstDifference(
                        canonical,
                        roundTrip,
                        Math.Min(difference + 64, canonical.Length));
                    throw new GameplayBattleArtifactFormatException(
                        typeof(T).Name
                        + " did not round-trip through canonical replay data at "
                        + difference + ". Expected '"
                        + Snippet(canonical, difference) + "', actual '"
                        + Snippet(roundTrip, difference) + "'. Later expected '"
                        + Snippet(canonical, laterDifference)
                        + "', actual '" + Snippet(roundTrip, laterDifference)
                        + "'.");
                }
                return (T)value;
            }

            private static int FirstDifference(string left, string right)
                => FirstDifference(left, right, 0);

            private static int FirstDifference(
                string left,
                string right,
                int start)
            {
                int length = Math.Min(left.Length, right.Length);
                for (int index = start; index < length; index++)
                    if (left[index] != right[index]) return index;
                return length;
            }

            private static string Snippet(string value, int index)
            {
                int start = Math.Max(0, index - 32);
                int length = Math.Min(96, value.Length - start);
                return value.Substring(start, length);
            }

            private static object ReadValue(
                GameplayBattleArtifactCodec.JsonNode node,
                Type expectedType,
                string path)
            {
                Type nullable = Nullable.GetUnderlyingType(expectedType);
                if (nullable != null)
                {
                    return node.Kind == GameplayBattleArtifactCodec.JsonKind.Null
                        ? null
                        : ReadValue(node, nullable, path);
                }
                if (node.Kind == GameplayBattleArtifactCodec.JsonKind.Null)
                {
                    if (expectedType.IsValueType)
                        throw Wrong(path, expectedType, node.Kind);
                    return null;
                }
                if (expectedType == typeof(string))
                    return node.RequireString(path);
                if (expectedType == typeof(bool))
                    return node.RequireBoolean(path);
                if (expectedType.IsEnum)
                {
                    string text = node.RequireString(path);
                    object parsed = Enum.Parse(expectedType, text, false);
                    if (!expectedType.IsDefined(
                            typeof(FlagsAttribute),
                            inherit: false)
                        && !Enum.IsDefined(expectedType, parsed))
                        throw Wrong(path, expectedType, node.Kind);
                    return parsed;
                }
                if (IsNumber(expectedType))
                    return ReadNumber(node, expectedType, path);
                if (node.Kind == GameplayBattleArtifactCodec.JsonKind.Array)
                    return ReadSequence(node, expectedType, path);
                if (node.Kind != GameplayBattleArtifactCodec.JsonKind.Object)
                    throw Wrong(path, expectedType, node.Kind);
                if (TryGetDictionaryTypes(
                        expectedType,
                        out Type keyType,
                        out Type valueType))
                {
                    return ReadDictionary(
                        node,
                        expectedType,
                        keyType,
                        valueType,
                        path);
                }
                return ReadObject(node, expectedType, path);
            }

            private static object ReadSequence(
                GameplayBattleArtifactCodec.JsonNode node,
                Type expectedType,
                string path)
            {
                Type elementType = GetSequenceElementType(expectedType)
                    ?? throw new GameplayBattleArtifactFormatException(
                        path + " cannot be assigned to " + expectedType.Name + ".");
                IReadOnlyList<GameplayBattleArtifactCodec.JsonNode> nodes =
                    node.RequireArray(path);
                Array values = Array.CreateInstance(elementType, nodes.Count);
                for (int index = 0; index < nodes.Count; index++)
                {
                    values.SetValue(
                        ReadValue(
                            nodes[index],
                            elementType,
                            path + "[" + index + "]"),
                        index);
                }
                if (expectedType.IsArray) return values;
                Type listType = typeof(List<>).MakeGenericType(elementType);
                return Activator.CreateInstance(listType, values);
            }

            private static object ReadDictionary(
                GameplayBattleArtifactCodec.JsonNode node,
                Type expectedType,
                Type keyType,
                Type valueType,
                string path)
            {
                if (keyType != typeof(string))
                    throw new GameplayBattleArtifactFormatException(
                        path + " uses an unsupported dictionary key type.");
                Type dictionaryType = typeof(Dictionary<,>).MakeGenericType(
                    keyType,
                    valueType);
                IDictionary result = (IDictionary)Activator.CreateInstance(
                    dictionaryType);
                foreach (KeyValuePair<
                    string,
                    GameplayBattleArtifactCodec.JsonNode> entry in
                    node.RequireObject(path))
                {
                    result.Add(
                        entry.Key,
                        ReadValue(
                            entry.Value,
                            valueType,
                            path + "." + entry.Key));
                }
                return result;
            }

            private static object ReadObject(
                GameplayBattleArtifactCodec.JsonNode node,
                Type expectedType,
                string path)
            {
                IReadOnlyDictionary<
                    string,
                    GameplayBattleArtifactCodec.JsonNode> properties =
                    node.RequireObject(path);
                if (!properties.TryGetValue(
                        "$type",
                        out GameplayBattleArtifactCodec.JsonNode typeNode))
                {
                    throw new GameplayBattleArtifactFormatException(
                        path + " is missing its canonical type.");
                }
                Type runtimeType = ResolveType(typeNode.RequireString(
                    path + ".$type"));
                if (!expectedType.IsAssignableFrom(runtimeType))
                    throw new GameplayBattleArtifactFormatException(
                        path + " has incompatible type " + runtimeType.Name + ".");

                ConstructorInfo constructor = SelectConstructor(
                    runtimeType,
                    properties);
                ParameterInfo[] parameters = constructor.GetParameters();
                var arguments = new object[parameters.Length];
                for (int index = 0; index < parameters.Length; index++)
                {
                    ParameterInfo parameter = parameters[index];
                    if (TryReadSyntheticParameter(
                            properties,
                            parameter,
                            path,
                            out object synthetic))
                    {
                        arguments[index] = synthetic;
                    }
                    else if (TryGetProperty(
                            properties,
                            parameter.Name,
                            out GameplayBattleArtifactCodec.JsonNode value))
                    {
                        arguments[index] = ReadValue(
                            value,
                            parameter.ParameterType,
                            path + "." + parameter.Name);
                    }
                    else
                    {
                        arguments[index] = parameter.DefaultValue;
                    }
                }
                try
                {
                    AlignSharedActionContext(
                        runtimeType,
                        parameters,
                        arguments);
                    object instance = constructor.Invoke(arguments);
                    HydrateCanonicalAutoProperties(
                        instance,
                        runtimeType,
                        properties,
                        path);
                    AlignSharedActionContext(instance);
                    return instance;
                }
                catch (TargetInvocationException exception)
                {
                    throw new GameplayBattleArtifactFormatException(
                        path + " failed to construct " + runtimeType.Name + ".",
                        exception.InnerException ?? exception);
                }
            }

            private static void AlignSharedActionContext(
                Type runtimeType,
                IReadOnlyList<ParameterInfo> parameters,
                IReadOnlyList<object> arguments)
            {
                if (runtimeType != typeof(GameplayActionRecord)) return;
                IGameplayActionContext context = null;
                IEnumerable outcomes = null;
                for (int index = 0; index < parameters.Count; index++)
                {
                    if (string.Equals(
                            parameters[index].Name,
                            "context",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        context = arguments[index] as IGameplayActionContext;
                    }
                    else if (string.Equals(
                            parameters[index].Name,
                            "outcomes",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        outcomes = arguments[index] as IEnumerable;
                    }
                }
                AlignAttackContexts(outcomes, context);
            }

            private static void AlignSharedActionContext(object instance)
            {
                if (!(instance is GameplayActionRecord action)) return;
                AlignAttackContexts(action.Outcomes, action.Context);
            }

            private static void AlignAttackContexts(
                IEnumerable outcomes,
                IGameplayActionContext context)
            {
                if (outcomes == null) return;
                foreach (object outcome in outcomes)
                {
                    if (!(outcome is AttackResolvedActionOutcome resolved))
                        continue;
                    FieldInfo contextField = FindBackingField(
                        typeof(AttackResolutionRecord),
                        "<Context>k__BackingField");
                    contextField?.SetValue(resolved.Attack, context);
                }
            }

            private static void HydrateCanonicalAutoProperties(
                object instance,
                Type runtimeType,
                IReadOnlyDictionary<
                    string,
                    GameplayBattleArtifactCodec.JsonNode> properties,
                string path)
            {
                foreach (PropertyInfo property in runtimeType.GetProperties(
                    BindingFlags.Public | BindingFlags.Instance))
                {
                    if (string.Equals(
                            property.Name,
                            "CanonicalHash",
                            StringComparison.Ordinal)
                        || !properties.TryGetValue(
                            property.Name,
                            out GameplayBattleArtifactCodec.JsonNode node))
                    {
                        continue;
                    }
                    FieldInfo field = FindBackingField(
                        runtimeType,
                        "<" + property.Name + ">k__BackingField");
                    if (field == null) continue;
                    object value = ReadValue(
                        node,
                        property.PropertyType,
                        path + "." + property.Name);
                    field.SetValue(instance, value);
                }
            }

            private static FieldInfo FindBackingField(Type type, string name)
            {
                for (Type current = type;
                    current != null;
                    current = current.BaseType)
                {
                    FieldInfo field = current.GetField(
                        name,
                        BindingFlags.Instance | BindingFlags.NonPublic);
                    if (field != null) return field;
                }
                return null;
            }

            private static ConstructorInfo SelectConstructor(
                Type type,
                IReadOnlyDictionary<
                    string,
                    GameplayBattleArtifactCodec.JsonNode> properties)
            {
                ConstructorInfo selected = null;
                foreach (ConstructorInfo constructor in type.GetConstructors(
                        BindingFlags.Public
                        | BindingFlags.NonPublic
                        | BindingFlags.Instance)
                    .OrderByDescending(value => value.GetParameters().Length))
                {
                    bool usable = true;
                    foreach (ParameterInfo parameter in
                        constructor.GetParameters())
                    {
                        if (!CanResolveParameter(
                                properties,
                                parameter)
                            && !parameter.HasDefaultValue)
                        {
                            usable = false;
                            break;
                        }
                    }
                    if (!usable) continue;
                    selected = constructor;
                    break;
                }
                return selected ?? throw new GameplayBattleArtifactFormatException(
                    "Canonical replay type " + type.Name
                    + " has no matching constructor.");
            }

            private static bool CanResolveParameter(
                IReadOnlyDictionary<
                    string,
                    GameplayBattleArtifactCodec.JsonNode> properties,
                ParameterInfo parameter) =>
                TryGetProperty(properties, parameter.Name, out _)
                || IsSyntheticParameter(properties, parameter);

            private static bool IsSyntheticParameter(
                IReadOnlyDictionary<
                    string,
                    GameplayBattleArtifactCodec.JsonNode> properties,
                ParameterInfo parameter)
            {
                if (!TryGetProperty(properties, "Profile", out _))
                    return false;
                if (parameter.ParameterType
                    == typeof(GameplaySemanticCapability))
                {
                    return string.Equals(
                        parameter.Name,
                        "capability",
                        StringComparison.OrdinalIgnoreCase);
                }
                if (parameter.ParameterType
                    == typeof(GameplaySemanticSubjectKind))
                {
                    return string.Equals(
                        parameter.Name,
                        "targetKind",
                        StringComparison.OrdinalIgnoreCase);
                }
                if (parameter.ParameterType == typeof(AttackDefinition))
                {
                    GameplayCapabilityProfile profile = ReadProfile(
                        properties,
                        "Profile");
                    string resource = profile.GetTrait("resource");
                    return string.Equals(
                            resource,
                            "controller-drone-weapon",
                            StringComparison.Ordinal)
                        || (string.Equals(
                                resource,
                                "equipped-weapon",
                                StringComparison.Ordinal)
                            && string.Equals(
                                profile.GetTrait("consequence"),
                                "drone-integrity",
                                StringComparison.Ordinal));
                }
                return false;
            }

            private static bool TryReadSyntheticParameter(
                IReadOnlyDictionary<
                    string,
                    GameplayBattleArtifactCodec.JsonNode> properties,
                ParameterInfo parameter,
                string path,
                out object value)
            {
                if (!IsSyntheticParameter(properties, parameter))
                {
                    value = null;
                    return false;
                }
                GameplayCapabilityProfile profile = ReadProfile(
                    properties,
                    path + ".Profile");
                if (parameter.ParameterType
                    == typeof(GameplaySemanticCapability))
                {
                    value = profile.Capability;
                }
                else if (parameter.ParameterType
                    == typeof(GameplaySemanticSubjectKind))
                {
                    value = GameplayCapabilityProfiles.GetSubjectKind(profile);
                }
                else if (parameter.ParameterType == typeof(AttackDefinition))
                {
                    if (string.Equals(
                            profile.GetTrait("consequence"),
                            "drone-integrity",
                            StringComparison.Ordinal))
                    {
                        TryGetProperty(
                            properties,
                            "Action",
                            out GameplayBattleArtifactCodec.JsonNode actionNode);
                        var action = (ActorDroneAttackRecord)ReadValue(
                            actionNode,
                            typeof(ActorDroneAttackRecord),
                            path + ".Action");
                        value = new AttackDefinition(
                            action.AttackId,
                            "Artifact Replay",
                            action.Cost,
                            woundMovementPenalty: 1f,
                            accuracyDecay: AccuracyDecayDefinition.None,
                            directVehicleIntegrityDamage: 1f);
                    }
                    else
                    {
                        value = new AttackDefinition(
                            "artifact-replay",
                            "Artifact Replay",
                            default,
                            woundMovementPenalty: 1f,
                            accuracyDecay: AccuracyDecayDefinition.None);
                    }
                }
                else
                {
                    value = null;
                    return false;
                }
                return true;
            }

            private static GameplayCapabilityProfile ReadProfile(
                IReadOnlyDictionary<
                    string,
                    GameplayBattleArtifactCodec.JsonNode> properties,
                string path)
            {
                TryGetProperty(
                    properties,
                    "Profile",
                    out GameplayBattleArtifactCodec.JsonNode profileNode);
                return (GameplayCapabilityProfile)ReadValue(
                    profileNode,
                    typeof(GameplayCapabilityProfile),
                    path);
            }

            private static bool TryGetProperty(
                IReadOnlyDictionary<
                    string,
                    GameplayBattleArtifactCodec.JsonNode> properties,
                string parameterName,
                out GameplayBattleArtifactCodec.JsonNode node)
            {
                GameplayBattleArtifactCodec.JsonNode suffixMatch = null;
                foreach (KeyValuePair<
                    string,
                    GameplayBattleArtifactCodec.JsonNode> property in
                    properties)
                {
                    if (string.Equals(
                            property.Key,
                            parameterName,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        node = property.Value;
                        return true;
                    }
                    if (parameterName.EndsWith(
                            property.Key,
                            StringComparison.OrdinalIgnoreCase)
                        || property.Key.EndsWith(
                            parameterName,
                            StringComparison.OrdinalIgnoreCase)
                        || string.Equals(
                            RemoveCollectionSuffix(parameterName),
                            RemoveCollectionSuffix(property.Key),
                            StringComparison.OrdinalIgnoreCase))
                    {
                        if (suffixMatch != null)
                        {
                            node = null;
                            return false;
                        }
                        suffixMatch = property.Value;
                    }
                }
                if (suffixMatch != null)
                {
                    node = suffixMatch;
                    return true;
                }
                node = null;
                return false;
            }

            private static string RemoveCollectionSuffix(string value)
            {
                string[] suffixes =
                {
                    "Snapshots",
                    "Definitions",
                    "Records",
                    "Values",
                    "Items",
                };
                foreach (string suffix in suffixes)
                {
                    if (value.EndsWith(
                            suffix,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return value.Substring(0, value.Length - suffix.Length);
                    }
                }
                return value;
            }

            private static Type ResolveType(string fullName)
            {
                if (!fullName.StartsWith(
                        "GritGud.Application.Gameplay.",
                        StringComparison.Ordinal)
                    && !fullName.StartsWith(
                        "GritGud.Domain.Gameplay.",
                        StringComparison.Ordinal)
                    && !fullName.StartsWith(
                        "GritGud.Domain.Turns.",
                        StringComparison.Ordinal))
                {
                    throw new GameplayBattleArtifactFormatException(
                        "Canonical replay type '" + fullName
                        + "' is not allowlisted.");
                }
                foreach (Assembly assembly in AllowedAssemblies.Distinct())
                {
                    Type type = assembly.GetType(fullName, false, false);
                    if (type != null) return type;
                }
                throw new GameplayBattleArtifactFormatException(
                    "Canonical replay type '" + fullName
                    + "' is not allowlisted.");
            }

            private static object ReadNumber(
                GameplayBattleArtifactCodec.JsonNode node,
                Type type,
                string path)
            {
                string text = node.Kind
                    == GameplayBattleArtifactCodec.JsonKind.Number
                    ? node.Text
                    : throw Wrong(path, type, node.Kind);
                try
                {
                    if (type == typeof(float))
                        return float.Parse(text, CultureInfo.InvariantCulture);
                    if (type == typeof(double))
                        return double.Parse(text, CultureInfo.InvariantCulture);
                    if (type == typeof(decimal))
                        return decimal.Parse(text, CultureInfo.InvariantCulture);
                    return Convert.ChangeType(
                        text,
                        type,
                        CultureInfo.InvariantCulture);
                }
                catch (Exception exception)
                {
                    throw new GameplayBattleArtifactFormatException(
                        path + " is not a valid " + type.Name + ".",
                        exception);
                }
            }

            private static Type GetSequenceElementType(Type type)
            {
                if (type.IsArray) return type.GetElementType();
                if (type.IsGenericType
                    && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                    return type.GetGenericArguments()[0];
                Type sequence = type.GetInterfaces().FirstOrDefault(value =>
                    value.IsGenericType
                    && value.GetGenericTypeDefinition() == typeof(IEnumerable<>));
                return sequence?.GetGenericArguments()[0];
            }

            private static bool TryGetDictionaryTypes(
                Type type,
                out Type keyType,
                out Type valueType)
            {
                Type dictionary = type.IsGenericType
                    && (type.GetGenericTypeDefinition() == typeof(IDictionary<,>)
                        || type.GetGenericTypeDefinition()
                            == typeof(IReadOnlyDictionary<,>))
                    ? type
                    : type.GetInterfaces().FirstOrDefault(value =>
                        value.IsGenericType
                        && (value.GetGenericTypeDefinition()
                                == typeof(IDictionary<,>)
                            || value.GetGenericTypeDefinition()
                                == typeof(IReadOnlyDictionary<,>)));
                if (dictionary == null)
                {
                    keyType = null;
                    valueType = null;
                    return false;
                }
                Type[] arguments = dictionary.GetGenericArguments();
                keyType = arguments[0];
                valueType = arguments[1];
                return true;
            }

            private static bool IsNumber(Type type) =>
                type == typeof(byte)
                || type == typeof(sbyte)
                || type == typeof(short)
                || type == typeof(ushort)
                || type == typeof(int)
                || type == typeof(uint)
                || type == typeof(long)
                || type == typeof(ulong)
                || type == typeof(float)
                || type == typeof(double)
                || type == typeof(decimal);

            private static GameplayBattleArtifactFormatException Wrong(
                string path,
                Type expectedType,
                GameplayBattleArtifactCodec.JsonKind actual) =>
                new GameplayBattleArtifactFormatException(
                    path + " expected " + expectedType.Name
                    + " but found " + actual + ".");
        }
    }
}
