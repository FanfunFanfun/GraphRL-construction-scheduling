// Author: wyf

using System;

/// <summary>
/// The open-source release contains the 86-task scheduling problem only.
/// </summary>
public enum SchedulingProblemSize
{
    Auto = 0,
    Tasks86 = 86,
}

/// <summary>
/// Centralizes the correspondence between a Unity scene, its task CSV, graph shape,
/// and discrete action count.
/// </summary>
public static class SchedulingProblem
{
    public const int FeatureDimension = 22;

    public static int ResolveTaskCount(SchedulingProblemSize configuredSize, string sceneName)
    {
        if (configuredSize != SchedulingProblemSize.Auto)
        {
            return (int)configuredSize;
        }

        if (int.TryParse(sceneName, out var taskCount) && IsSupported(taskCount))
        {
            return taskCount;
        }

        throw new InvalidOperationException(
            $"Cannot infer the scheduling problem size from scene '{sceneName}'. " +
            "Name the scene 86, or set Problem Size to Tasks86 on SchedulingArea."
        );
    }

    public static string GetCsvFileName(int taskCount)
    {
        if (!IsSupported(taskCount))
        {
            throw new ArgumentOutOfRangeException(nameof(taskCount), taskCount, "Unsupported task count.");
        }

        return $"{taskCount}_tasks.CSV";
    }

    public static bool IsSupported(int taskCount)
    {
        return taskCount == 86;
    }
}
