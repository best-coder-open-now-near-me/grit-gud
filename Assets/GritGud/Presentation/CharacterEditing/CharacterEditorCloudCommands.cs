using System;
using GritGud.Domain.Characters;
using GritGud.Presentation.Bootstrap;

namespace GritGud.Presentation.CharacterEditing
{
    internal interface ICharacterEditorCloudGateway
    {
        bool IsAvailable { get; }
        string UnavailableStatus { get; }
        void Save(
            CharacterDocument document,
            string serialized,
            Action succeeded,
            Action<string> failed);
        void Load(string characterId, Action<string> succeeded, Action<string> failed);
    }

    internal interface ICharacterEditorCloudHost
    {
        bool IsReady { get; }
        long Revision { get; }
        CharacterDocument CreateSnapshot();
        string Serialize(CharacterDocument document);
        CharacterDocument DeserializeAndValidate(string text);
        void ReplaceWithLoaded(CharacterDocument document);
        void MarkSaved();
        void SetStatus(string message);
    }

    internal sealed class GameBootstrapCharacterCloudGateway :
        ICharacterEditorCloudGateway
    {
        private static GameBootstrap Bootstrap => GameBootstrap.Instance;

        public bool IsAvailable => Bootstrap?.Supabase?.IsReady == true;
        public string UnavailableStatus =>
            Bootstrap?.Supabase?.Status ?? "Cloud saves are not configured.";

        public void Save(
            CharacterDocument document,
            string serialized,
            Action succeeded,
            Action<string> failed) =>
            Bootstrap.Supabase.SaveCharacter(document, serialized, succeeded, failed);

        public void Load(
            string characterId,
            Action<string> succeeded,
            Action<string> failed) =>
            Bootstrap.Supabase.LoadCharacter(characterId, succeeded, failed);
    }

    internal sealed class CharacterEditorCloudCommands : IDisposable
    {
        private readonly ICharacterEditorCloudGateway gateway;
        private readonly ICharacterEditorCloudHost host;
        private int operationVersion;
        private bool disposed;

        public CharacterEditorCloudCommands(
            ICharacterEditorCloudGateway gateway,
            ICharacterEditorCloudHost host)
        {
            this.gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
            this.host = host ?? throw new ArgumentNullException(nameof(host));
        }

        public bool IsRunning { get; private set; }

        public void Save()
        {
            if (!TryBegin("Saving character to cloud…", out int version)) return;
            long savedRevision = host.Revision;
            CharacterDocument snapshot;
            string serialized;
            try
            {
                snapshot = host.CreateSnapshot();
                serialized = host.Serialize(snapshot);
            }
            catch (Exception exception)
            {
                Fail(version, exception.Message);
                return;
            }

            gateway.Save(
                snapshot,
                serialized,
                () =>
                {
                    if (!CanApply(version)) return;
                    if (host.Revision == savedRevision) host.MarkSaved();
                    host.SetStatus("Saved the character to cloud.");
                    Complete(version);
                },
                error => Fail(version, error));
        }

        public void Load()
        {
            if (!TryBegin("Loading character from cloud…", out int version)) return;
            CharacterDocument snapshot = host.CreateSnapshot();
            long requestedRevision = host.Revision;
            gateway.Load(
                snapshot.characterId,
                text =>
                {
                    if (!CanApply(version)) return;
                    if (host.Revision != requestedRevision)
                    {
                        Fail(version, "Cloud load was not applied because the character changed while loading.");
                        return;
                    }
                    try
                    {
                        CharacterDocument loaded = host.DeserializeAndValidate(text);
                        if (!CanApply(version)) return;
                        host.ReplaceWithLoaded(loaded);
                        host.SetStatus("Loaded the character from cloud.");
                        Complete(version);
                    }
                    catch (Exception exception)
                    {
                        Fail(version, exception.Message);
                    }
                },
                error => Fail(version, error));
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            operationVersion++;
            IsRunning = false;
        }

        private bool TryBegin(string status, out int version)
        {
            version = operationVersion;
            if (disposed || IsRunning) return false;
            if (!gateway.IsAvailable || !host.IsReady)
            {
                host.SetStatus(gateway.UnavailableStatus);
                return false;
            }
            version = ++operationVersion;
            IsRunning = true;
            host.SetStatus(status);
            return true;
        }

        private bool CanApply(int version) =>
            !disposed && host.IsReady && version == operationVersion;

        private void Fail(int version, string message)
        {
            if (!CanApply(version)) return;
            host.SetStatus(message);
            Complete(version);
        }

        private void Complete(int version)
        {
            if (version == operationVersion && !disposed) IsRunning = false;
        }
    }
}
