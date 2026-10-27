using UnityEditor;
using UnityEngine;
using MCPForUnity.Editor.Services;
using MCPForUnity.Editor.Services.Transport;

internal static class RuneRushMcpSession
{
    [InitializeOnLoadMethod]
    private static void OnLoad()
    {
        EditorApplication.delayCall += Connect;
    }

    private static async void Connect()
    {
        if (Application.isBatchMode) return;
        try
        {
            bool ok = await MCPServiceLocator.TransportManager.StartAsync(TransportMode.Http);
            Debug.Log("RUNE_RUSH_MCP_SESSION: " + ok);
        }
        catch (System.Exception e) { Debug.LogWarning("RUNE_RUSH_MCP_SESSION: " + e.Message); }
    }
}
