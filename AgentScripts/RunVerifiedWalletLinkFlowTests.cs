using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

public static class RunVerifiedWalletLinkFlowTests
{
    public static async Task<string> Main()
    {
        Type testType = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType("Chainbound.Combat.Tests.VerifiedWalletLinkFlowTests", false))
            .FirstOrDefault(type => type != null);
        if (testType == null)
            throw new InvalidOperationException("VerifiedWalletLinkFlowTests is not loaded in the Editor domain.");

        MethodInfo tearDown = testType.GetMethod("TearDown", BindingFlags.Instance | BindingFlags.Public);
        MethodInfo[] tests = testType.GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.GetCustomAttributes(false).Any(attribute => attribute.GetType().Name == "TestAttribute"))
            .OrderBy(method => method.Name)
            .ToArray();

        var passed = new List<string>();
        foreach (MethodInfo test in tests)
        {
            object instance = Activator.CreateInstance(testType);
            try
            {
                object result = test.Invoke(instance, null);
                if (result is Task task)
                    await task;
                passed.Add(test.Name);
            }
            catch (TargetInvocationException exception)
            {
                throw exception.InnerException ?? exception;
            }
            finally
            {
                tearDown?.Invoke(instance, null);
            }
        }

        return $"Passed {passed.Count}: {string.Join(", ", passed)}";
    }
}
