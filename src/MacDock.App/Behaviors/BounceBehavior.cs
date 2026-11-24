using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using MacDock.Core.Dock;

namespace MacDock.App.Behaviors;

/// <summary>
/// Attached behavior: <c>behaviors:BounceBehavior.IsEnabled="True"</c> makes an element hop when its
/// <see cref="DockItemViewModel"/> raises <see cref="DockItemViewModel.BounceRequested"/>.
/// Keeps the animation in the view layer without code-behind per item.
/// </summary>
public static class BounceBehavior
{
    private static readonly Duration Total = TimeSpan.FromMilliseconds(700);

    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(BounceBehavior), new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject d) => (bool)d.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject d, bool value) => d.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element || e.NewValue is not true) return;

        var translate = new TranslateTransform();
        element.RenderTransform = translate;

        DockItemViewModel? current = null;
        void Bounce(object? sender, EventArgs args) => translate.BeginAnimation(TranslateTransform.YProperty, CreateAnimation());
        void Attach(object? dataContext)
        {
            if (current is not null) current.BounceRequested -= Bounce;
            current = dataContext as DockItemViewModel;
            if (current is not null) current.BounceRequested += Bounce;
        }

        Attach(element.DataContext);
        element.DataContextChanged += (_, args) => Attach(args.NewValue);
        element.Loaded += (_, _) => Attach(element.DataContext);
        element.Unloaded += (_, _) => Attach(null);
    }

    private static DoubleAnimationUsingKeyFrames CreateAnimation()
    {
        var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };
        var fall = new QuadraticEase { EasingMode = EasingMode.EaseIn };
        return new DoubleAnimationUsingKeyFrames
        {
            Duration = Total,
            KeyFrames =
            {
                new EasingDoubleKeyFrame(-24, KeyTime.FromPercent(0.25), ease),
                new EasingDoubleKeyFrame(0, KeyTime.FromPercent(0.50), fall),
                new EasingDoubleKeyFrame(-12, KeyTime.FromPercent(0.70), ease),
                new EasingDoubleKeyFrame(0, KeyTime.FromPercent(1.00), fall),
            },
        };
    }
}
