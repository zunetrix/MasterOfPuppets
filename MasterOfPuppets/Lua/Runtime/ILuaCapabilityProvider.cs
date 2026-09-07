namespace MasterOfPuppets.LuaScripting.Runtime;

public interface ILuaCapabilityProvider {
    LuaCapabilityDescriptor Descriptor { get; }
    void Register(LuaApiRegistrationContext registration);
}
