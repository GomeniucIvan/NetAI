using System;

namespace NetAI.Api.Services.Settings;

internal static class OpenHandsConfigurationBridge
{
    private static readonly object SyncRoot = new();

    private static double? _remoteRuntimeResourceFactor;
    private static string _gitUserName;
    private static string _gitUserEmail;

    public static double? RemoteRuntimeResourceFactor
    {
        get
        {
            lock (SyncRoot)
            {
                return _remoteRuntimeResourceFactor;
            }
        }
    }

    public static string GitUserName
    {
        get
        {
            lock (SyncRoot)
            {
                return _gitUserName;
            }
        }
    }

    public static string GitUserEmail
    {
        get
        {
            lock (SyncRoot)
            {
                return _gitUserEmail;
            }
        }
    }

    public static void UpdateRuntimeResourceFactor(double? value)
    {
        lock (SyncRoot)
        {
            _remoteRuntimeResourceFactor = value;
        }
    }

    public static void UpdateGitConfiguration(string userName, string userEmail)
    {
        lock (SyncRoot)
        {
            _gitUserName = userName;
            _gitUserEmail = userEmail;
        }
    }
}
