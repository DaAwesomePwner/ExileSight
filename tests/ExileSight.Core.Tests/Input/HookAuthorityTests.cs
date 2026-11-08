using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using Xunit;

namespace ExileSight.Core.Tests.Input;

public sealed class HookAuthorityTests
{
    [Fact]
    public void OnlyHookHostMayReportPhysicalEventsAndNoPlatformOrAppCodeCanMintDirectly()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "ExileSight.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        foreach (var project in new[] { "ExileSight.Core", "ExileSight.Platform.Windows", "ExileSight.App" })
        {
            var path = Path.Combine(root.FullName, "src", project, "bin", configuration,
                project == "ExileSight.Core" ? "net10.0" : "net10.0-windows10.0.19041.0", project + ".dll");
            using var stream = File.OpenRead(path);
            using var pe = new PEReader(stream);
            var metadata = pe.GetMetadataReader();
            foreach (var handle in metadata.MethodDefinitions)
            {
                var method = metadata.GetMethodDefinition(handle);
                if (method.RelativeVirtualAddress == 0) continue;
                var owner = metadata.GetTypeDefinition(method.GetDeclaringType());
                foreach (var token in Calls(pe.GetMethodBody(method.RelativeVirtualAddress).GetILBytes()!))
                {
                    var target = MetadataTokens.EntityHandle(token);
                    string typeName, name;
                    if (target.Kind == HandleKind.MethodDefinition)
                    {
                        var definition = metadata.GetMethodDefinition((MethodDefinitionHandle)target);
                        typeName = metadata.GetString(metadata.GetTypeDefinition(definition.GetDeclaringType()).Name);
                        name = metadata.GetString(definition.Name);
                    }
                    else if (target.Kind == HandleKind.MemberReference)
                    {
                        var member = metadata.GetMemberReference((MemberReferenceHandle)target);
                        if (member.Parent.Kind != HandleKind.TypeReference) continue;
                        var type = metadata.GetTypeReference((TypeReferenceHandle)member.Parent);
                        typeName = metadata.GetString(type.Name);
                        name = metadata.GetString(member.Name);
                    }
                    else continue;
                    var caller = metadata.GetString(owner.Name);
                    if (typeName == "IntentRegistry" && name is "Register" or ".ctor")
                        Assert.Equal("PhysicalIssuer", caller);
                    if (name == "CreateHookIssuer") Assert.Equal("HookHost", caller);
                    if (typeName == "HotkeyDispatcher" && name is "InstallIssuer" or "InstallFeatureIssuer") Assert.Equal("HookHost", caller);
                    if (typeName == "PhysicalIssuer" && name == "Issue")
                    {
                        Assert.Equal("ExileSight.Platform.Windows", project);
                        Assert.Equal("HookHost", caller);
                        Assert.Contains(metadata.GetString(method.Name), new[] { "Create", "RedeemOverlayClick" });
                    }
                    if (typeName == "HotkeyDispatcher" && name == "Issue") Assert.Equal("PressRouter", caller);
                    if (typeName == "PhysicalPress" && name == ".ctor") Assert.Equal("HotkeyDispatcher", caller);
                    if (typeName == "IntentToken" && name == ".ctor") Assert.Equal("IntentRegistry", caller);
                    if ((typeName == "HotkeyDispatcher" && name == "Observe") ||
                        (typeName == "PressRouter" && name == "Route")) Assert.Equal("HookHost", caller);
                }
            }
        }
    }
    private static readonly Dictionary<ushort, OpCode> Opcodes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.FieldType == typeof(OpCode)).Select(field => (OpCode)field.GetValue(null)!)
        .ToDictionary(code => unchecked((ushort)code.Value));
    private static IEnumerable<int> Calls(byte[] il)
    {
        var offset = 0;
        while (offset < il.Length)
        {
            ushort value = il[offset++];
            if (value == 0xfe) value = (ushort)(0xfe00 | il[offset++]);
            var code = Opcodes[value];
            if (code.OperandType == OperandType.InlineMethod) yield return BitConverter.ToInt32(il, offset);
            offset += code.OperandType switch
            {
                OperandType.InlineNone => 0,
                OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                OperandType.InlineVar => 2,
                OperandType.InlineI8 or OperandType.InlineR => 8,
                OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(il, offset),
                _ => 4
            };
        }
    }
}
