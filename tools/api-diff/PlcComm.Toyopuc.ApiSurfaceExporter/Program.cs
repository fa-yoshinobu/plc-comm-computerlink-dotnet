using System.Globalization;
using System.Reflection;
using System.Text.Json;

if (args.Length != 2)
{
    Console.Error.WriteLine("Usage: PlcComm.Toyopuc.ApiSurfaceExporter <assembly> <output-json>");
    return 2;
}

var assemblyPath = Path.GetFullPath(args[0]);
var outputPath = Path.GetFullPath(args[1]);
if (!File.Exists(assemblyPath))
{
    Console.Error.WriteLine($"Assembly does not exist: {assemblyPath}");
    return 2;
}

var assembly = Assembly.LoadFrom(assemblyPath);
const BindingFlags Flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
var surface = new List<SurfaceItem>();

foreach (var type in assembly.GetExportedTypes().OrderBy(item => item.FullName, StringComparer.Ordinal))
{
    var owner = StableTypeName(type);
    var kind = type.IsEnum ? "enum"
        : type.IsInterface ? "interface"
        : type.IsValueType ? "struct"
        : type.BaseType is not null && typeof(MulticastDelegate).IsAssignableFrom(type.BaseType) ? "delegate"
        : "class";
    var baseType = type.BaseType is null ? "none" : StableTypeName(type.BaseType);
    var interfaces = string.Join(",", type.GetInterfaces().Select(StableTypeName).Order(StringComparer.Ordinal));
    var constraints = GenericConstraintText(type.GetGenericArguments().Where(item => item.IsGenericParameter));
    var visibility = type.IsNested ? "nested-public" : "public";
    Add($"type:{owner}", $"type|{kind}|{owner}|visibility={visibility}|abstract={type.IsAbstract}|sealed={type.IsSealed}|base={baseType}|interfaces={interfaces}|constraints={constraints}");

    foreach (var constructor in type.GetConstructors(Flags).OrderBy(item => item.ToString(), StringComparer.Ordinal))
    {
        var parameters = string.Join(",", constructor.GetParameters().Select(ParameterText));
        Add($"constructor:{owner}::.ctor", $"member|{owner}|constructor|.ctor|public|instance|params={parameters}");
    }

    foreach (var method in type.GetMethods(Flags).Where(item => !item.IsSpecialName).OrderBy(item => item.Name, StringComparer.Ordinal).ThenBy(item => item.ToString(), StringComparer.Ordinal))
    {
        var parameters = string.Join(",", method.GetParameters().Select(ParameterText));
        var methodConstraints = GenericConstraintText(method.GetGenericArguments().Where(item => item.IsGenericParameter));
        var staticState = method.IsStatic ? "static" : "instance";
        Add($"method:{owner}::{method.Name}", $"member|{owner}|method|{method.Name}|public|{staticState}|abstract={method.IsAbstract}|virtual={method.IsVirtual}|final={method.IsFinal}|returns={StableTypeName(method.ReturnType)}|params={parameters}|constraints={methodConstraints}");
    }

    foreach (var property in type.GetProperties(Flags).OrderBy(item => item.Name, StringComparer.Ordinal).ThenBy(item => item.ToString(), StringComparer.Ordinal))
    {
        var getter = property.GetGetMethod();
        var setter = property.GetSetMethod();
        var staticState = (getter?.IsStatic is true || setter?.IsStatic is true) ? "static" : "instance";
        var index = string.Join(",", property.GetIndexParameters().Select(ParameterText));
        Add($"property:{owner}::{property.Name}", $"member|{owner}|property|{property.Name}|{staticState}|type={StableTypeName(property.PropertyType)}|public-get={getter is not null};public-set={setter is not null}|index={index}");
    }

    foreach (var eventInfo in type.GetEvents(Flags).OrderBy(item => item.Name, StringComparer.Ordinal))
    {
        var staticState = eventInfo.GetAddMethod()?.IsStatic is true ? "static" : "instance";
        Add($"event:{owner}::{eventInfo.Name}", $"member|{owner}|event|{eventInfo.Name}|public|{staticState}|type={StableTypeName(eventInfo.EventHandlerType!)}");
    }

    foreach (var field in type.GetFields(Flags).OrderBy(item => item.Name, StringComparer.Ordinal))
    {
        var staticState = field.IsStatic ? "static" : "instance";
        var value = field.IsLiteral ? Convert.ToString(field.GetRawConstantValue(), CultureInfo.InvariantCulture) ?? string.Empty : "none";
        Add($"field:{owner}::{field.Name}", $"member|{owner}|field|{field.Name}|public|{staticState}|type={StableTypeName(field.FieldType)}|literal={field.IsLiteral}|readonly={field.IsInitOnly}|value={value}");
    }
}

surface.Sort((left, right) =>
{
    var symbolOrder = StringComparer.Ordinal.Compare(left.symbol, right.symbol);
    return symbolOrder != 0 ? symbolOrder : StringComparer.Ordinal.Compare(left.signature, right.signature);
});
Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
File.WriteAllText(outputPath, JsonSerializer.Serialize(surface, new JsonSerializerOptions { WriteIndented = true }));
return 0;

void Add(string symbol, string signature) => surface.Add(new SurfaceItem(symbol, signature));

static string StableTypeName(Type type)
{
    if (type.IsByRef) return $"{StableTypeName(type.GetElementType()!)}&";
    if (type.IsPointer) return $"{StableTypeName(type.GetElementType()!)}*";
    if (type.IsArray) return $"{StableTypeName(type.GetElementType()!)}[{new string(',', type.GetArrayRank() - 1)}]";
    if (type.IsGenericParameter) return $"!{type.GenericParameterPosition}:{type.Name}";
    if (!type.IsGenericType) return (type.FullName ?? type.Name).Replace('+', '.');

    var definition = (type.GetGenericTypeDefinition().FullName ?? type.Name).Replace('+', '.');
    var tick = definition.LastIndexOf('`');
    if (tick >= 0) definition = definition[..tick];
    return $"{definition}<{string.Join(',', type.GetGenericArguments().Select(StableTypeName))}>";
}

static string ParameterText(ParameterInfo parameter)
{
    var direction = parameter.IsOut ? "out"
        : parameter.ParameterType.IsByRef && parameter.IsIn ? "in"
        : parameter.ParameterType.IsByRef ? "ref"
        : "value";
    var optional = parameter.IsOptional ? "optional" : "required";
    var defaultValue = "none";
    if (parameter.HasDefaultValue)
    {
        defaultValue = parameter.DefaultValue is null
            ? "null"
            : (Convert.ToString(parameter.DefaultValue, CultureInfo.InvariantCulture) ?? string.Empty).Replace("|", "\\|");
    }
    return $"{parameter.Name}:{StableTypeName(parameter.ParameterType)}:{direction}:{optional}:default={defaultValue}";
}

static string GenericConstraintText(IEnumerable<Type> arguments) => string.Join(';', arguments.Select(argument =>
{
    var constraints = string.Join(',', argument.GetGenericParameterConstraints().Select(StableTypeName).Order(StringComparer.Ordinal));
    return $"{argument.Name}:attributes={argument.GenericParameterAttributes}:types={constraints}";
}));

internal sealed record SurfaceItem(string symbol, string signature);
