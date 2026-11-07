using System.Collections.Immutable;
using System.Reflection.Metadata;

namespace ExileSight.Architecture.Tests;

internal sealed class MetadataSignatures : ISignatureTypeProvider<string, object?>
{
    internal static string MethodId(MetadataReader reader, TypeDefinition type, MethodDefinition method)
    {
        var signature = method.DecodeSignature(new MetadataSignatures(), null);
        var name = reader.GetString(method.Name);
        if (signature.GenericParameterCount > 0)
            name += "``" + signature.GenericParameterCount;
        var parameters = signature.ParameterTypes.Length == 0 ? "" : "(" + string.Join(",", signature.ParameterTypes) + ")";
        return "M:" + reader.GetString(type.Namespace) + "." + reader.GetString(type.Name) + "." + name + parameters;
    }

    public string GetArrayType(string elementType, ArrayShape shape) => elementType + "[" + string.Join(",", Enumerable.Repeat("0:", shape.Rank)) + "]";
    public string GetByReferenceType(string elementType) => elementType + "@";
    public string GetFunctionPointerType(MethodSignature<string> signature) => ""; // The compiler emits an empty documentation-ID component for function pointers.
    public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments) => genericType.Split('`')[0] + "{" + string.Join(",", typeArguments) + "}";
    public string GetGenericMethodParameter(object? genericContext, int index) => "``" + index;
    public string GetGenericTypeParameter(object? genericContext, int index) => "`" + index;
    public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired) => unmodifiedType;
    public string GetPinnedType(string elementType) => elementType;
    public string GetPointerType(string elementType) => elementType + "*";
    public string GetPrimitiveType(PrimitiveTypeCode typeCode) => "System." + (typeCode switch
    {
        PrimitiveTypeCode.IntPtr => "IntPtr",
        PrimitiveTypeCode.UIntPtr => "UIntPtr",
        _ => typeCode.ToString()
    });
    public string GetSZArrayType(string elementType) => elementType + "[]";
    public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
    {
        var type = reader.GetTypeDefinition(handle);
        return reader.GetString(type.Namespace) + "." + reader.GetString(type.Name);
    }
    public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
    {
        var type = reader.GetTypeReference(handle);
        return reader.GetString(type.Namespace) + "." + reader.GetString(type.Name);
    }
    public string GetTypeFromSpecification(MetadataReader reader, object? genericContext, TypeSpecificationHandle handle, byte rawTypeKind) =>
        reader.GetTypeSpecification(handle).DecodeSignature(this, genericContext);
}
