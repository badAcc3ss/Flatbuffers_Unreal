using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using EpicGames.UHT.Tables;
using EpicGames.UHT.Utils;

namespace FlatBuffer.FBS.FlatBufferMetaGenerator
{
    [UnrealHeaderTool]
    public static class FlatBufferSchemaExporter
    {
        private const string PluginName = "FlatBufferMetaGenerator";
        private const string ExporterName = "FlatBufferExporter";
        private const string ModuleName = "FlatBufferMetaGenerator";

        // The subdirectory under the project folder where .fbs files and generated code go.
        // e.g. "<ProjectRoot>/Intermediate/FlatBufferMetaGenerator"
        private static readonly string IntermediateRelativeDir = Path.Combine("Intermediate", PluginName);

        [UhtExporter(Name = ExporterName, ModuleName = ModuleName, Options = UhtExporterOptions.Default)]
        public static void GenerateReferenceList(IUhtExportFactory factory)
        {
            // 1) Check if plugin is enabled
            factory.Session.LogInfo("GenerateReferenceList invoked... Checking plugin status.");
            if (!factory.Session.IsPluginEnabled(PluginName, includeTargetCheck: true))
            {
                factory.Session.LogInfo($"Plugin '{PluginName}' is not enabled. Exiting.");
                return;
            }
            //TODO need to modify how the files are being created and stored in a more optimal way
            try
            {
                factory.Session.LogInfo("Gathering FlatBuffer USTRUCTs and UENUMs...");
                var structInfos = FlatBufferSchemaGenerator.FindFlatBufferStructs(factory.Session.Packages).ToList();
                var enumInfos = FlatBufferSchemaGenerator.FindFlatBufferEnums(factory.Session.Packages).ToList();

                if (structInfos.Count == 0)
                {
                    factory.Session.LogInfo("No USTRUCT found with 'Category=FlatBuffer'. Nothing to do.");
                    return;
                }

                string projectRoot = factory.Session.ProjectDirectory ?? Directory.GetCurrentDirectory();
                string intermediateDir = Path.Combine(projectRoot, IntermediateRelativeDir);
                Directory.CreateDirectory(intermediateDir); // ensure it exists

                // The plugin source folder, e.g. "<ProjectRoot>/Plugins/FlatBufferMetaGenerator/Source"
                string pluginSourceDir = Path.Combine(projectRoot, "Plugins", PluginName, "Source");
                string flatcExePath = Path.Combine(pluginSourceDir, "ThirdParty", "flatc.exe");

                if (!File.Exists(flatcExePath))
                {
                    throw new FileNotFoundException($"Could not find flatc.exe at: {flatcExePath}");
                }

                factory.Session.LogInfo($"Using flatc at: {flatcExePath}");
                factory.Session.LogInfo($"Intermediate output dir: {intermediateDir}");

                var generatedHeaders = new List<string>();

                foreach (var structInfo in structInfos)
                {
                    // Build a minimal .fbs for this single struct (with any needed enums).
                    string fbsContent = FlatBufferSchemaGenerator.BuildSingleStructFbs(structInfo, enumInfos);

                    // e.g. "FMyStruct.fbs"
                    string fbsFileName = structInfo.StructName + ".fbs";
                    string fbsFullPath = Path.Combine(intermediateDir, fbsFileName);

                    // Write the .fbs
                    File.WriteAllText(fbsFullPath, fbsContent);
                    factory.Session.LogInfo($"Wrote: {fbsFileName} in {intermediateDir}");

                    // Run flatc to produce .h/.cpp in the same intermediate folder
                    RunFlatcToGenerateCode(flatcExePath, fbsFullPath, intermediateDir, factory);

                    // Typically, flatc produces "FMyStruct_generated.h"
                    // We'll store just "FMyStruct_generated.h", expecting the build system to have
                    // added 'Intermediate/FlatBufferMetaGenerator' to its include paths.
                    string generatedHeader = structInfo.StructName + "_generated.h";
                    generatedHeaders.Add(generatedHeader);
                }

                //TODO needs changes
                string aggregatorPath = Path.Combine(projectRoot, "Source", "TestingFlatBuffers","FlatBufferAutoIncludes.h");

                CreateAggregatorHeader(aggregatorPath, generatedHeaders, factory);

                factory.Session.LogInfo("FlatBuffer schema generation & compilation completed successfully.");
            }
            catch (Exception ex)
            {
                factory.Session.LogError($"Error generating FlatBuffer schema or invoking flatc: {ex}");
                throw;
            }
        }

        /// <summary>
        /// Invokes 'flatc' on a single .fbs to produce generated C++ code.
        /// </summary>
        private static void RunFlatcToGenerateCode(
            string flatcExePath,
            string fbsPath,
            string outputDir,
            IUhtExportFactory factory
        )
        {
            factory.Session.LogInfo($"Running flatc on '{fbsPath}'...");

            var psi = new ProcessStartInfo
            {
                FileName = flatcExePath,
                Arguments = $"--cpp --gen-mutable -o \"{outputDir}\" \"{fbsPath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = outputDir,
            };

            using var p = Process.Start(psi) ?? throw new Exception("Failed to start flatc process");
            string stdOut = p.StandardOutput.ReadToEnd();
            string stdErr = p.StandardError.ReadToEnd();
            p.WaitForExit();

            // Log outputs
            if (!string.IsNullOrWhiteSpace(stdOut))
            {
                factory.Session.LogInfo("flatc stdout:\n" + stdOut);
            }
            if (!string.IsNullOrWhiteSpace(stdErr))
            {
                factory.Session.LogWarning("flatc stderr:\n" + stdErr);
            }
            if (p.ExitCode != 0)
            {
                throw new Exception($"flatc.exe failed with exit code {p.ExitCode}");
            }

            factory.Session.LogInfo($"flatc generation finished for: {Path.GetFileName(fbsPath)}");
        }

        /// <summary>
        /// Creates a single "FlatBufferAutoIncludes.h" that #includes each generated .h
        /// so the build system can bring them in automatically.
        /// </summary>
        private static void CreateAggregatorHeader(
            string aggregatorPath,
            List<string> generatedHeaderNames,
            IUhtExportFactory factory
        )
        {
            factory.Session.LogInfo($"Creating aggregator header: {aggregatorPath}");

            // We optionally do a preprocessor guard so that if the file doesn't exist yet,
            // we don't get a fatal error. e.g.:
            //
            // #if __has_include("FMyStruct_generated.h")
            // #include "FMyStruct_generated.h"
            // #endif
            //
            // This approach allows the plugin to compile even if the file isn't there yet.

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("#pragma once");
            sb.AppendLine("// Auto-generated aggregator of all FlatBuffer code");
            sb.AppendLine("// DO NOT manually edit this file; it is re-generated each build.");
            sb.AppendLine();

            foreach (string headerFile in generatedHeaderNames)
            {
                sb.AppendLine($"#if __has_include(\"{headerFile}\")");
                sb.AppendLine($"#include \"{headerFile}\"");
                sb.AppendLine("#endif");
                sb.AppendLine();
            }

            File.WriteAllText(aggregatorPath, sb.ToString());
            factory.Session.LogInfo($"Wrote aggregator with {generatedHeaderNames.Count} includes.");
        }
    }
}
