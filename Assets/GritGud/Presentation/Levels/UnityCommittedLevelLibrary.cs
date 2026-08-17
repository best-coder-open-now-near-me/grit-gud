using System.Linq;
using GritGud.Application.Levels;
using GritGud.Presentation.Gameplay;
using UnityEngine;

namespace GritGud.Presentation.Levels
{
    public static class UnityCommittedLevelLibrary
    {
        public const string PublishedResourceFolder = "Levels/Published";
        public const string DefaultResourceKey =
            PublishedResourceFolder + "/main-level";

        public static CommittedLevelLibrary LoadDefault()
        {
            GameplayContentPackage content = GameplayContentLoader.LoadDefault();
            CommittedLevelSource[] sources = Resources
                .LoadAll<TextAsset>(PublishedResourceFolder)
                .Select(asset => new CommittedLevelSource(
                    PublishedResourceFolder + "/" + asset.name,
                    asset.name,
                    asset.text))
                .ToArray();
            return new CommittedLevelLibrary(
                sources,
                new UnityLevelJsonSerializer(),
                content.ValidationContent);
        }
    }
}
