using System;
using System.Collections.Generic;
using System.Text;

namespace FlatBuffer.FBS.FlatBufferMetaGenerator
{
    /// <summary>
    /// Represents a complete .fbs schema with a single namespace, some file-level attributes,
    /// plus multiple enums, structs (or tables), unions, etc.
    /// </summary>
    internal sealed class FlatBufferSchema
    {
        public string NamespaceName { get; set; } = "";
        public List<string> FileLevelAttributes { get; } = new();
        public List<FbsEnum> Enums { get; } = new();
        public List<FbsStructOrTable> StructsOrTables { get; } = new();
        public List<FbsUnion> Unions { get; } = new();
        public string? RootType { get; set; }

        public string ToFbsString()
        {
            var sb = new StringBuilder();

            // 1) Namespace
            if (!string.IsNullOrWhiteSpace(NamespaceName))
            {
                sb.AppendLine($"namespace {NamespaceName};");
                sb.AppendLine();
            }

            // 2) File-level attributes
            foreach (var attr in FileLevelAttributes)
            {
                sb.AppendLine($"attribute \"{attr}\";");
            }
            if (FileLevelAttributes.Count > 0)
            {
                sb.AppendLine();
            }

            // 3) Enums
            foreach (FbsEnum en in Enums)
            {
                sb.AppendLine(en.ToFbsString());
            }

            // 4) Structs/Tables
            foreach (FbsStructOrTable st in StructsOrTables)
            {
                sb.AppendLine(st.ToFbsString());
            }

            // 5) Unions
            foreach (FbsUnion union in Unions)
            {
                sb.AppendLine(union.ToFbsString());
            }

            // 6) root_type
            if (!string.IsNullOrEmpty(RootType))
            {
                sb.AppendLine($"root_type {RootType};");
            }

            return sb.ToString().TrimEnd();
        }
    }

    internal sealed class FbsEnum
    {
        public string Name { get; }
        public string UnderlyingType { get; }
        public List<FbsEnumValue> Values { get; } = new();

        public FbsEnum(string name, string underlyingType)
        {
            Name = name;
            UnderlyingType = underlyingType;
        }

        public string ToFbsString()
        {
            // Collect enumerators in a single line, e.g.:
            // "UseDefault = 0, Player = 1, AI = 2, Hidden = 3"
            var items = new List<string>();
            foreach (var val in Values)
            {
                if (val.ExplicitValue.HasValue)
                {
                    items.Add($"{val.Name} = {val.ExplicitValue.Value}");
                }
                else
                {
                    items.Add(val.Name);
                }
            }

            // Build a single-line enum
            // => enum ECharacterType : byte { UseDefault = 0, Player = 1, AI = 2, Hidden = 3 }
            return $"enum {Name} : {UnderlyingType} {{ {string.Join(", ", items)} }}";
        }
    }

    internal readonly struct FbsEnumValue
    {
        public string Name { get; }
        public long? ExplicitValue { get; }

        public FbsEnumValue(string name, long? explicitValue = null)
        {
            Name = name;
            ExplicitValue = explicitValue;
        }
    }

    /// <summary>
    /// Represent either a 'struct' or 'table' in the .fbs schema.
    /// </summary>
    internal sealed class FbsStructOrTable
    {
        public string Name { get; }
        public bool IsTable { get; }
        public List<FbsField> Fields { get; } = new();

        public FbsStructOrTable(string name, bool isTable)
        {
            Name = name;
            IsTable = isTable;
        }

        public string ToFbsString()
        {
            string kind = IsTable ? "table" : "struct";
            var sb = new StringBuilder();
            sb.AppendLine($"{kind} {Name} {{");
            foreach (var field in Fields)
            {
                sb.AppendLine("  " + field.ToFbsString());
            }
            sb.Append("}");
            return sb.ToString();
        }
    }

    internal sealed class FbsField
    {
        public string Name { get; }
        // e.g. "int", "string", "[int]", "Vec3", "TextureFormat"
        public string Type { get; }
        // e.g. "100", "true", "Blue"
        public string? DefaultValue { get; }
        // e.g. "(deprecated, priority:1)", or "(ui: \"min:1, max:16384\")", etc.
        public string? ExtraAttributes { get; }

        public FbsField(string name, string type, string? defaultValue, string? extraAttributes)
        {
            Name = name;
            Type = type;
            DefaultValue = defaultValue;
            ExtraAttributes = extraAttributes;
        }

        public string ToFbsString()
        {
            var sb = new StringBuilder();
            sb.Append(Name).Append(": ").Append(Type);

            if (!string.IsNullOrEmpty(DefaultValue))
            {
                sb.Append(" = ").Append(DefaultValue);
            }

            if (!string.IsNullOrEmpty(ExtraAttributes))
            {
                sb.Append(" ").Append(ExtraAttributes);
            }

            sb.Append(";");
            return sb.ToString();
        }
    }

    internal sealed class FbsUnion
    {
        public string Name { get; }
        public List<string> Types { get; } = new();

        public FbsUnion(string name)
        {
            Name = name;
        }

        public string ToFbsString()
        {
            // e.g. "union Any { FTestingFlatBuffer, Weapon, Pickup }"
            return $"union {Name} {{ {string.Join(", ", Types)} }}";
        }
    }
}