using System;
using System.Linq;
using System.Reflection;

public static class QueueMilestone1AndroidBuild
{
    public static object Main()
    {
        var buildCommandType = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType("Unity.Pipeline.Editor.Commands.Build.BuildCommand"))
            .First(type => type != null);

        var validateAndQueue = buildCommandType.GetMethod(
            "ValidateAndQueue",
            BindingFlags.Static | BindingFlags.NonPublic);

        if (validateAndQueue == null)
        {
            throw new MissingMethodException(buildCommandType.FullName, "ValidateAndQueue");
        }

        return validateAndQueue.Invoke(
            null,
            new object[]
            {
                "Android",
                "Builds/Android/Chainbound-Milestone1-Dev.apk",
                string.Empty,
                new[] { "Development", "DetailedBuildReport" },
                null,
                true,
                false
            });
    }
}
