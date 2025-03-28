using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using EpicGames.UHT.Types;

namespace FlatBuffer.FBS.FlatBufferMetaGenerator
{
    internal sealed record FlatBufferStructInfo(
    string StructName,
    string NamespaceName,
    List<FlatBufferFieldInfo> Fields
);

    internal sealed record FlatBufferFieldInfo(
        string Name,
        string Type,
        string? DefaultValue,
        string? ExtraAttributes
    );

    internal sealed record FlatBufferEnumInfo(
        string EnumName,
        string UnderlyingType,
        List<(string Name, long Value)> Enumerants
    );

    internal static class FlatBufferSchemaGenerator
    {
        public static IEnumerable<FlatBufferStructInfo> FindFlatBufferStructs(IEnumerable<UhtPackage> packages)
        {
            foreach (UhtPackage package in packages)
            {
                foreach (UhtType type in package.Children)
                {
                    foreach (FlatBufferStructInfo structInfo in FindFlatBufferMetadata(type))
                    {
                        yield return structInfo;
                    }
                }
            }
        }

        private static IEnumerable<FlatBufferStructInfo> FindFlatBufferMetadata(UhtType type)
        {
            if (type.MetaData is not null && !type.MetaData.IsEmpty())
            {
                foreach (var (key, value) in type.MetaData.Dictionary!)
                {
                    if (key.Name.Equals("Category", StringComparison.OrdinalIgnoreCase) &&
                        value.Equals("FlatBuffer", StringComparison.OrdinalIgnoreCase))
                    {
                        yield return CreateFlatBufferStructInfo(type);
                    }
                }
            }

            foreach (UhtType child in type.Children)
            {
                foreach (FlatBufferStructInfo childStructInfo in FindFlatBufferMetadata(child))
                {
                    yield return childStructInfo;
                }
            }
        }

        private static FlatBufferStructInfo CreateFlatBufferStructInfo(UhtType type)
        {
            string structName = ExtractStructName(type);
            string packageNamespace = ExtractNamespaceFromPackage(type);
            List<FlatBufferFieldInfo> fields = ExtractStructFields(type);

            return new FlatBufferStructInfo(
                StructName: structName,
                NamespaceName: packageNamespace,
                Fields: fields
            );
        }

        private static string ExtractNamespaceFromPackage(UhtType type)
        {
            // 1) Possibly check if there's a custom metadata like "FlatBufferNamespace"
            //    If so, return that directly:
            if (type.MetaData != null && type.MetaData.TryGetValue("FlatBufferNamespace", out var metaNs))
            {
                return metaNs;
            }

            // 2) Otherwise fallback to the package name:
            //    e.g., "MyProject/Source/Module/Foo" => "MyProject.Source.Module.Foo"
            if (type.Package != null)
            {
                string pkgName = type.Package.ShortName ?? "UnknownPackage";
                // Replace slashes with dots, etc. 
                pkgName = pkgName.Replace('/', '.').Replace('\\', '.');
                return pkgName;
            }

            // fallback
            return "Default.Namespace";
        }

        private static string ExtractStructName(UhtType type)
        {
            if (type is UhtScriptStruct scriptStruct)
            {
                return scriptStruct.SourceName ?? "UnknownStruct";
            }
            return "UnknownStruct";
        }

        private static List<FlatBufferFieldInfo> ExtractStructFields(UhtType type)
        {
            var fields = new List<FlatBufferFieldInfo>();

            if (type is UhtScriptStruct scriptStruct)
            {
                foreach (UhtProperty property in scriptStruct.Properties)
                {
                    bool isEnum;
                    string? enumName;
                    string fieldType = MapToFlatBufferType(property, out isEnum, out enumName);

                    // If this property is an enum, use the enum name
                    if (isEnum && enumName != null)
                    {
                        fieldType = enumName;
                    }

                    // Extract possible default
                    string? defaultValue = TryGetDefaultValue(property, isEnum, fieldType);

                    // Extract attributes, e.g. (deprecated)
                    string? extraAttributes = TryGetExtraAttributes(property);

                    // Convert property name to lowercase (as you do now),
                    // or do snake_case if you prefer
                    string finalFieldName = property.SourceName.ToLowerInvariant();

                    fields.Add(new FlatBufferFieldInfo(
                        Name: finalFieldName,
                        Type: fieldType,
                        DefaultValue: defaultValue,
                        ExtraAttributes: extraAttributes
                    ));
                }
            }

            return fields;
        }

        private static string MapToFlatBufferType(UhtProperty property, out bool isEnum, out string? enumName)
        {
            isEnum = false;
            enumName = null;

            switch (property)
            {
                case UhtBoolProperty:
                    return "bool";
                case UhtByteProperty:
                    return "ubyte";
                case UhtInt8Property:
                    return "byte";
                case UhtInt16Property:
                    return "short";
                case UhtUInt16Property:
                    return "ushort";
                case UhtIntProperty:
                    return "int";
                case UhtUInt32Property:
                    return "uint";
                case UhtInt64Property:
                    return "long";
                case UhtUInt64Property:
                    return "ulong";
                case UhtFloatProperty:
                    return "float";
                case UhtDoubleProperty:
                    return "double";
                case UhtStrProperty or UhtNameProperty or UhtTextProperty:
                    return "string";

                case UhtArrayProperty arrayProp when arrayProp.ValueProperty != null:
                    {
                        string subtype = MapToFlatBufferType(arrayProp.ValueProperty, out _, out _);
                        return $"[{subtype}]";
                    }
                case UhtArrayProperty:
                    return "[]";

                case UhtSetProperty setProp when setProp.ValueProperty != null:
                    {
                        string subtype = MapToFlatBufferType(setProp.ValueProperty, out _, out _);
                        return $"[{subtype}]";
                    }
                case UhtSetProperty:
                    return "[]";

                case UhtMapProperty:
                    return "table"; // simplistic approach

                case UhtClassProperty or
                     UhtInterfaceProperty or
                     UhtFieldPathProperty or
                     UhtObjectProperty or
                     UhtObjectPropertyBase:
                    return "string";

                case UhtDelegateProperty or
                     UhtMulticastDelegateProperty or
                     UhtVerseValueProperty:
                    return "string";

                case UhtOptionalProperty:
                    return "table";

                case UhtStructProperty structProp:
                    {
                        // e.g., if it's "FVector" => "Vec3"
                        string possibleStructName = structProp.ScriptStruct?.SourceName ?? "UnknownStruct";
                        if (possibleStructName.Equals("FVector", StringComparison.OrdinalIgnoreCase))
                        {
                            return "Vec3";
                        }
                        return "table";
                    }

                case UhtNumericProperty:
                    return "int";

                case UhtEnumProperty enumProp:
                    {
                        isEnum = true;
                        enumName = enumProp.Enum?.SourceName ?? "int";
                        return "int"; // fallback if we can't find an actual name
                    }

                case UhtVoidProperty:
                    return "string";

                default:
                    return "string";
            }
        }

        /// <summary>
        /// Tries to read the default value from UProperty. If it's a string property, wrap in quotes for the final .fbs.
        /// e.g. if the user sets "Color = \"Blue\";", we want "Blue" in quotes in the final schema.
        /// </summary>
        private static string? TryGetDefaultValue(UhtProperty property, bool isEnum, string fieldType)
        {
            // If we specifically store defaults in "FlatBufferDefault" metadata:
            if (property.MetaData.TryGetValue("FlatBufferDefault", out var defaultFromMeta))
            {
                // If it is a string property, you might need quotes. 
                return defaultFromMeta;
            }
            return null;
        }

        private static string? TryGetExtraAttributes(UhtProperty property)
        {
            if (property.MetaData is null || property.MetaData.IsEmpty())
            {
                return null;
            }

            var attributes = new List<string>();

            if (property.MetaData.ContainsKey("Deprecated"))
            {
                attributes.Add("deprecated");
            }

            if (property.MetaData.TryGetValue("Priority", out var priorityValue))
            {
                attributes.Add($"priority: {priorityValue}");
            }

            if (attributes.Count == 0)
            {
                return null;
            }

            return $"({string.Join(", ", attributes)})";
        }

        /// <summary>
        /// Finds any UHT enums you'd like to generate as FlatBuffer enums.
        /// </summary>
        private static void GatherEnumsRecursive(UhtType type, List<FlatBufferEnumInfo> outEnums)
        {
            if (type is UhtEnum uhtEnum)
            {
                // 1) Get a raw name
                string enumName = uhtEnum.SourceName ?? "UnknownEnum";

                // 2) Convert underlying type to a FlatBuffers-friendly name
                //    e.g. "UInt8" => "byte", "Int32" => "int", etc.
                string underlyingType = MapUnderlyingType(uhtEnum.UnderlyingType.ToString() ?? "byte");

                // 3) Gather enumerators, but remove C++ scope qualifiers if present
                var enumerants = new List<(string Name, long Value)>();
                foreach (var enumerator in uhtEnum.EnumValues)
                {
                    // E.g. enumerator.Name might be "ECharacterType::AI"
                    string cleanedName = StripScopePrefix(enumerator.Name);
                    enumerants.Add((cleanedName, enumerator.Value));
                }

                Console.WriteLine($"Found enum: {enumName} with underlying type: {underlyingType}");

                outEnums.Add(new FlatBufferEnumInfo(enumName, underlyingType, enumerants));
            }

            // Recurse to find nested enums
            foreach (UhtType child in type.Children)
            {
                GatherEnumsRecursive(child, outEnums);
            }
        }

        private static string StripScopePrefix(string enumeratorName)
        {
            // If enumeratorName = "ECharacterType::AI", split by "::" and take the last
            // If enumeratorName = "AI" already, it remains "AI"
            var parts = enumeratorName.Split(new[] { "::" }, StringSplitOptions.None);
            return parts[parts.Length - 1];
        }


        private static string MapUnderlyingType(string ueUnderlyingType)
        {
            // Example typical mappings – adapt as needed for your project
            // e.g. "UInt8" => "byte", "Int8" => "byte", "Int16" => "short", etc.
            switch (ueUnderlyingType.ToLowerInvariant())
            {
                case "uint8":
                case "uint16":
                case "int8":
                    return "byte";
                case "int16":
                    return "short";
                case "uint32":
                case "int32":
                    return "int";
                case "int64":
                case "uint64":
                    return "long";
                // Fallback
                default:
                    return "int";
            }
        }


        public static List<FlatBufferEnumInfo> FindFlatBufferEnums(IEnumerable<UhtPackage> packages)
        {
            var enumInfos = new List<FlatBufferEnumInfo>();
            foreach (UhtPackage package in packages)
            {
                // For each top-level child, do a recursive walk
                foreach (UhtType topLevelType in package.Children)
                {
                    GatherEnumsRecursive(topLevelType, enumInfos);
                }
            }
            return enumInfos;
        }


        /// <summary>
        /// Builds the final .fbs file with a structured approach, allowing 
        /// the namespace and root type to be configurable.
        /// </summary>
        //public static string BuildFbsString(
        //    IEnumerable<FlatBufferStructInfo> structInfos,
        //    IEnumerable<FlatBufferEnumInfo> allEnumInfos
        //)

        public static string BuildSingleStructFbs(
    FlatBufferStructInfo structInfo,
    IEnumerable<FlatBufferEnumInfo> allEnumInfos
)
        {
            // 1) Create a new FlatBufferSchema for just this struct
            var schema = new FlatBufferSchema
            {
                NamespaceName = structInfo.NamespaceName + "." + structInfo.StructName
            };
            schema.FileLevelAttributes.Add("priority");

            // 2) Identify which enums this struct uses
            var neededEnumNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Check if "Vec3" needed (if the struct references "Vec3" or "FVector" mapped to "Vec3")
            bool requiresVec3 = false;

            // Build a table for this struct
            var fbsTable = new FbsStructOrTable(structInfo.StructName, isTable: true);

            foreach (var field in structInfo.Fields)
            {
                // Check if field references a known enum
                if (allEnumInfos.Any(e => e.EnumName.Equals(field.Type, StringComparison.OrdinalIgnoreCase)))
                {
                    neededEnumNames.Add(field.Type);
                }

                // Check if field type is "Vec3"
                if (field.Type.Equals("Vec3", StringComparison.OrdinalIgnoreCase))
                {
                    requiresVec3 = true;
                }

                // Add field
                fbsTable.Fields.Add(new FbsField(
                    name: field.Name,
                    type: field.Type,
                    defaultValue: field.DefaultValue,
                    extraAttributes: field.ExtraAttributes
                ));
            }

            // 3) Add needed enums
            foreach (string enumName in neededEnumNames)
            {
                var match = allEnumInfos.FirstOrDefault(e => e.EnumName.Equals(enumName, StringComparison.OrdinalIgnoreCase));
                if (match == null)
                    continue; // or fallback to "int"?

                var fbsEnum = new FbsEnum(match.EnumName, match.UnderlyingType);
                foreach (var (enName, enVal) in match.Enumerants)
                {
                    fbsEnum.Values.Add(new FbsEnumValue(enName, enVal));
                }
                schema.Enums.Add(fbsEnum);
            }

            // 4) Insert Vec3 if required
            if (requiresVec3)
            {
                var vec3Struct = new FbsStructOrTable("Vec3", isTable: false);
                vec3Struct.Fields.Add(new FbsField("x", "float", null, null));
                vec3Struct.Fields.Add(new FbsField("y", "float", null, null));
                vec3Struct.Fields.Add(new FbsField("z", "float", null, null));
                schema.StructsOrTables.Add(vec3Struct);
            }

            // 5) Add this struct’s table
            schema.StructsOrTables.Add(fbsTable);

            // 6) (Optional) set this table as root_type
            schema.RootType = structInfo.StructName;

            // 7) Return final .fbs text
            return schema.ToFbsString();
        }
    }
}
