// Copyright © 2024 LeagueX Gaming
using UnrealBuildTool;
using System.IO;

public class FlatBufferMetaGenerator : ModuleRules
{
    public FlatBufferMetaGenerator(ReadOnlyTargetRules Target) : base(Target)
    {

        PCHUsage = PCHUsageMode.UseExplicitOrSharedPCHs;

        PrivateDependencyModuleNames.AddRange(
            new[]
            {
                "Core",
                "CoreUObject",
            }
        );

        // Example plugin-wide macro
        PublicDefinitions.Add("FLATBUFFERMETAGENERATOR_PLUGIN=1");

        PublicDependencyModuleNames.Add("zlib");

        string ThirdParytPath = Path.Combine(PluginDirectory, "Source", "ThirdParty");

        string LibraryPath = Path.Combine(ThirdParytPath, "lib");

        PublicSystemIncludePaths.Add(Path.Combine(LibraryPath, "include"));

        //if (Target.IsInPlatformGroup(UnrealPlatformGroup.Unix))
        //{
        //    PublicAdditionalLibraries.Add(Path.Combine(LibraryPath, "Unix", Target.Architecture.LinuxName, "Release", "libprotobuf.a"));
        //}
        //else if (Target.Platform == UnrealTargetPlatform.Mac)
        //{
        //    PublicAdditionalLibraries.Add(Path.Combine(LibraryPath, "Mac", "Release", "libprotobuf.a"));
        //}
        //else
        if (Target.Platform == UnrealTargetPlatform.Win64)
        {
            PublicAdditionalLibraries.Add(Path.Combine(LibraryPath, Target.WindowsPlatform.Architecture.ToString().ToLowerInvariant(), "flatbuffers.lib"));
        }
    }
}
