using System.Reflection;

namespace Woodie.Checks;

// Test-only use of GH8's internal loader: no user settings or installed extension folders are changed.
internal static class IsolatedHost
{
    static Assembly grasshopper;
    static object server;
    static MethodInfo parse;
    static bool woodieLoaded;

    public static void Prepare()
    {
        Environment.SetEnvironmentVariable("GRASSHOPPER_PLUGINS", null);
        var directory = new DirectoryInfo(Path.GetDirectoryName(typeof(Rhino.RhinoDoc).Assembly.Location));
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Plug-ins/Grasshopper/Grasshopper.dll"))) directory = directory.Parent;
        if (directory == null) throw new Exception("Cannot locate installed Grasshopper for the isolated host");
        string gh = Path.Combine(directory.FullName, "Plug-ins/Grasshopper/Grasshopper.dll");
        grasshopper = Assembly.LoadFrom(gh);
        Type serverType = grasshopper.GetType("Grasshopper.Kernel.GH_ComponentServer", true);
        Type instances = grasshopper.GetType("Grasshopper.Instances", true);
        var flags = BindingFlags.NonPublic | BindingFlags.Static;
        var field = instances.GetField("m_comServer", flags) ?? throw new Exception("Cannot isolate this Grasshopper version");
        if (field.GetValue(null) != null) throw new Exception("Grasshopper initialized before extension isolation");
        serverType.GetMethod("SetExternalGHAs", flags)?.Invoke(null, new object[] { Array.Empty<string>() });
        server = Activator.CreateInstance(serverType);
        field.SetValue(null, server);
        parse = serverType.GetMethod("ParseGHA", BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(Assembly), typeof(string) }, null)
            ?? throw new Exception("Isolated GH loader unavailable");
        Console.WriteLine("Isolated Grasshopper server prepared; external extension discovery disabled.");
    }

    public static void LoadBuiltIns() => parse.Invoke(server, new object[] { grasshopper, grasshopper.Location });
    public static void LoadWoodie(string path)
    {
        if (woodieLoaded) return;
        Assembly assembly = Assembly.LoadFrom(path);
        parse.Invoke(server, new object[] { assembly, path });
        woodieLoaded = true;
    }
}
