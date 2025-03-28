// Fill out your copyright notice in the Description page of Project Settings.

using UnrealBuildTool;

public class TestingFlatBuffers : ModuleRules
{
	public TestingFlatBuffers(ReadOnlyTargetRules Target) : base(Target)
	{
		PCHUsage = PCHUsageMode.UseExplicitOrSharedPCHs;

        // Force the aggregator as private PCH for *this* module too:
        PrivatePCHHeaderFile = "FlatBufferAutoIncludes.h";

        PublicDependencyModuleNames.AddRange(new string[] { "Core", "CoreUObject", "Engine", "InputCore","FlatBufferMetaGenerator" });

		PrivateDependencyModuleNames.AddRange(new string[] {  });
	}
}
