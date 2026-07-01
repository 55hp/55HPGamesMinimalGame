using NUnit.Framework;
using System;
using System.Linq;

public class CorePresenceTests
{
    // Fully-qualified names of the one canonical implementation per core role.
    private static readonly string[] ExpectedTypes =
    {
        "hp55games.Ui.UiManager",
        "hp55games.Mobile.Core.Architecture.EventBus",
        "hp55games.Mobile.Core.Architecture.UnityLog",
        "hp55games.Mobile.Core.Architecture.ConfigService",
        "hp55games.Mobile.Core.Architecture.SaveService"
    };

    [Test]
    public void UnityVersion_IsAvailable()
    {
        Assert.IsFalse(string.IsNullOrEmpty(UnityEngine.Application.unityVersion),
            "Unity version should not be empty.");
    }

    [Test]
    public void Core_Types_Are_Present_ByName()
    {
        var asms = AppDomain.CurrentDomain.GetAssemblies();
        bool AnyType(string fqn) =>
            asms.SelectMany(SafeGetTypes).Any(t => t.FullName == fqn);

        var missing = ExpectedTypes.Where(fqn => !AnyType(fqn)).ToArray();

        Assert.IsEmpty(missing,
            "The following core types could not be found by fully-qualified name:\n" +
            string.Join("\n", missing));
    }

    private static Type[] SafeGetTypes(System.Reflection.Assembly a)
    {
        try { return a.GetTypes(); }
        catch { return Array.Empty<Type>(); }
    }
}
