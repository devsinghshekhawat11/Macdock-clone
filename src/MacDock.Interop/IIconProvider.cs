using System.Windows.Media;

namespace MacDock.Interop;

public interface IIconProvider
{
    /// <summary>Returns a frozen icon for the pin target, or a generic placeholder. Never throws.</summary>
    ImageSource Get(string target, int sizePx);
}
