using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace MasterOfPuppets.LuaScripting.Runtime;

public sealed class LuaCapabilityRegistry {
    private readonly IReadOnlyList<ILuaCapabilityProvider> _providers;
    private readonly IReadOnlyList<LuaCapabilityDescriptor> _descriptors;

    public LuaCapabilityRegistry(IEnumerable<ILuaCapabilityProvider> providers) {
        ArgumentNullException.ThrowIfNull(providers);
        _providers = providers.OrderBy(provider => provider.Descriptor.Name, StringComparer.Ordinal).ToArray();
        _descriptors = _providers.Select(provider => provider.Descriptor.Validate()).ToArray();
        var duplicate = _descriptors
            .GroupBy(descriptor => descriptor.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate != null)
            throw new InvalidOperationException($"Duplicate Lua capability provider name: {duplicate.Key}");
    }

    public IReadOnlyList<LuaCapabilityDescriptor> Descriptors => _descriptors;

    public LuaCapabilityRegistry SelectForRun(IReadOnlyList<string>? declaredCapabilities) {
        if (declaredCapabilities == null || declaredCapabilities.Count == 0)
            return this;

        var requested = declaredCapabilities
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        requested.Add("mop.runtime");
        var unknown = requested
            .Where(name => !_descriptors.Any(descriptor =>
                descriptor.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        if (unknown.Length > 0)
            throw new InvalidOperationException(
                $"Lua bundle declares unavailable capabilities: {string.Join(", ", unknown)}. " +
                $"Available: {string.Join(", ", _descriptors.Select(descriptor => descriptor.Name))}.");

        return new LuaCapabilityRegistry(_providers.Where(provider => requested.Contains(provider.Descriptor.Name)));
    }

    public static LuaCapabilityRegistry Discover(Assembly? assembly = null) {
        assembly ??= typeof(LuaCapabilityRegistry).Assembly;
        var providers = assembly.GetTypes()
            .Where(type => typeof(ILuaCapabilityProvider).IsAssignableFrom(type)
                && type is { IsAbstract: false, IsInterface: false }
                && type.GetConstructor(Type.EmptyTypes) != null)
            .Select(type => (ILuaCapabilityProvider)Activator.CreateInstance(type)!)
            .ToArray();
        return new LuaCapabilityRegistry(providers);
    }

    public void RegisterAll(LuaApiRegistrationContext registration) {
        foreach (var provider in _providers)
            provider.Register(registration);
    }
}
