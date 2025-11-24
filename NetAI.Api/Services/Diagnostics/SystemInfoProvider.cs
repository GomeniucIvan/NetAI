using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using NetAI.Api.Models.Diagnostics;

namespace NetAI.Api.Services.Diagnostics;

public sealed class SystemInfoProvider : ISystemInfoProvider
{
    private static readonly DateTimeOffset StartTime = DateTimeOffset.UtcNow;
    private static readonly object SyncRoot = new();

    private DateTimeOffset _lastExecutionTime = StartTime;
    private DateTimeOffset _lastCpuSampleTime = DateTimeOffset.UtcNow;
    private TimeSpan _lastTotalProcessorTime = Process.GetCurrentProcess().TotalProcessorTime;

    public SystemInfoDto GetSystemInfo()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        double uptime = (now - StartTime).TotalSeconds;
        double idleTime = (now - _lastExecutionTime).TotalSeconds;

        SystemResourceStatsDto resources = GetSystemStats(now);

        return new SystemInfoDto(uptime, idleTime, resources);
    }

    public void UpdateLastExecutionTime()
    {
        _lastExecutionTime = DateTimeOffset.UtcNow;
    }

    private SystemResourceStatsDto GetSystemStats(DateTimeOffset now)
    {
        Process process = Process.GetCurrentProcess();
        double cpuPercent = CalculateCpuPercent(process, now);

        MemoryStatsDto memory = GetMemoryStats(process);
        DiskStatsDto disk = GetDiskStats();
        IoStatsDto io = GetIoStats(process);

        return new SystemResourceStatsDto(cpuPercent, memory, disk, io);
    }

    private double CalculateCpuPercent(Process process, DateTimeOffset now)
    {
        lock (SyncRoot)
        {
            TimeSpan totalProcessorTime = process.TotalProcessorTime;
            double cpuPercent = 0d;

            double elapsedMilliseconds = (now - _lastCpuSampleTime).TotalMilliseconds;
            if (elapsedMilliseconds > double.Epsilon)
            {
                TimeSpan cpuTimeDelta = totalProcessorTime - _lastTotalProcessorTime;
                cpuPercent = cpuTimeDelta.TotalMilliseconds / (Environment.ProcessorCount * elapsedMilliseconds) * 100d;
                if (cpuPercent < 0 || double.IsNaN(cpuPercent) || double.IsInfinity(cpuPercent))
                {
                    cpuPercent = 0d;
                }
            }

            _lastCpuSampleTime = now;
            _lastTotalProcessorTime = totalProcessorTime;
            return cpuPercent;
        }
    }

    private static MemoryStatsDto GetMemoryStats(Process process)
    {
        long rss = process.WorkingSet64;
        long vms = process.VirtualMemorySize64;
        double? percent = null;

        long totalMemory = GetTotalSystemMemory();
        if (totalMemory > 0)
        {
            percent = rss / (double)totalMemory * 100d;
        }

        return new MemoryStatsDto(rss, vms, percent);
    }

    private static long GetTotalSystemMemory()
    {
        try
        {
            long total = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
            if (total > 0)
            {
                return total;
            }
        }
        catch
        {
            // ignored
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            try
            {
                foreach (string line in File.ReadLines("/proc/meminfo"))
                {
                    if (line.StartsWith("MemTotal:", StringComparison.Ordinal))
                    {
                        string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 2 && long.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out long valueKb))
                        {
                            return valueKb * 1024;
                        }
                    }
                }
            }
            catch
            {
                // ignored
            }
        }

        return 0;
    }

    private static DiskStatsDto GetDiskStats()
    {
        try
        {
            string root = GetPrimaryDriveRoot();
            var drive = new DriveInfo(root);
            if (drive.IsReady)
            {
                long total = drive.TotalSize;
                long free = drive.TotalFreeSpace;
                long used = total - free;
                double percent = total > 0 ? used / (double)total * 100d : 0d;
                return new DiskStatsDto(total, used, free, percent);
            }
        }
        catch
        {
            // ignored
        }

        return new DiskStatsDto(0, 0, 0, 0);
    }

    private static string GetPrimaryDriveRoot()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            string systemDir = Environment.GetFolderPath(Environment.SpecialFolder.System);
            return Path.GetPathRoot(systemDir) ?? Path.DirectorySeparatorChar.ToString();
        }

        return Path.DirectorySeparatorChar.ToString();
    }

    private static IoStatsDto GetIoStats(Process process)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            string path = Path.Combine("/proc", process.Id.ToString(CultureInfo.InvariantCulture), "io");
            try
            {
                long readBytes = 0;
                long writeBytes = 0;

                foreach (string line in File.ReadLines(path))
                {
                    if (line.StartsWith("read_bytes:", StringComparison.Ordinal))
                    {
                        readBytes = ParseIoValue(line);
                    }
                    else if (line.StartsWith("write_bytes:", StringComparison.Ordinal))
                    {
                        writeBytes = ParseIoValue(line);
                    }
                }

                return new IoStatsDto(readBytes, writeBytes);
            }
            catch
            {
                // ignored
            }
        }

        return new IoStatsDto(0, 0);
    }

    private static long ParseIoValue(string line)
    {
        string[] parts = line.Split(':', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2 && long.TryParse(parts[1].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out long value))
        {
            return value;
        }

        return 0;
    }
}
