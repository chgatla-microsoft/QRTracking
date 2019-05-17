using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

public class HolographicRemotingPluginRegistration : IPreprocessBuildWithReport
{
    public int callbackOrder  { get { return 0; } }

    private readonly string[] nativePluginNames = new string[]
    {
        "HolographicStreamer.dll", "Microsoft.Perception.Simulation.dll", "PerceptionRemotingPlugin.dll"
    };

    public void OnPreprocessBuild(BuildReport report)
    {
        var allPlugins = PluginImporter.GetAllImporters();
        foreach (var plugin in allPlugins)
        {
            if (plugin.isNativePlugin)
            {
                foreach (var pluginName in nativePluginNames)
                {
                    if (plugin.assetPath.Contains(pluginName))
                    {
                        plugin.SetIncludeInBuildDelegate(ShouldIncludeInBuild);
                        break;
                    }
                }
            }
        }
    }

    private bool ShouldIncludeInBuild(string path)
    {
        return PlayerSettings.GetWsaHolographicRemotingEnabled();
    }
}
