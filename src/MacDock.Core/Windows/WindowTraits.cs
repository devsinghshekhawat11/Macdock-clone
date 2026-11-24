namespace MacDock.Core.Windows;

/// <summary>The raw facts about a window that decide whether it appears on the dock.</summary>
public readonly record struct WindowTraits(
    bool IsVisible,
    bool IsCloaked,
    bool HasOwner,
    bool IsToolWindow,
    bool IsAppWindow,
    bool IsNoActivate,
    string Title,
    bool IsOwnProcess);
