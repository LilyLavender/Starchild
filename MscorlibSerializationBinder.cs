using System;
using System.Reflection;
using OdinSerializer;

// needed bc "System.Private.CoreLib" as an assemblyName breaks Peglin
public class MscorlibSerializationBinder : TwoWaySerializationBinder
{
    public override bool ContainsType(string typeName)
    {
        return Type.GetType(typeName) != null;
    }

    public override Type BindToType(string typeName, DebugContext debugContext)
    {
        string[] parts = typeName.Split(',');
        string cleanTypeName = parts[0].Trim();
        string assemblyName = parts.Length > 1 ? parts[1].Trim() : null;

        if (assemblyName == "mscorlib")
        {
            assemblyName = typeof(object).Assembly.GetName().Name;
        }

        return Type.GetType($"{cleanTypeName}, {assemblyName}");
    }

    public override string BindToName(Type type, DebugContext debugContext)
    {
        string typeName = type.FullName;
        string cleanTypeName = typeName.Replace(", Version=0.0.0.0, Culture=neutral, PublicKeyToken=null", "");
        string assemblyName = type.Assembly.GetName().Name;

        if (assemblyName == "System.Private.CoreLib")
        {
            assemblyName = "mscorlib";
        }

        return $"{cleanTypeName}, {assemblyName}";
    }
}
