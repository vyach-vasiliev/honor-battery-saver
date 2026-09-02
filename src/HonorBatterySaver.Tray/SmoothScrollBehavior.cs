using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace HonorBatterySaver.Tray;

public static class SmoothScrollBehavior
{
    private const double WheelStep = 84;
    private static readonly ConditionalWeakTable<ScrollViewer, ScrollState> States = new();

    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled",
        typeof(bool),
        typeof(SmoothScrollBehavior),
        new PropertyMetadata(false, OnIsEnabledChanged));

    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);

    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

    private static void OnIsEnabledChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is not ScrollViewer viewer)
        {
            return;
        }

        if ((bool)args.NewValue)
        {
            viewer.PreviewMouseWheel += OnPreviewMouseWheel;
        }
        else
        {
            viewer.PreviewMouseWheel -= OnPreviewMouseWheel;
            if (States.TryGetValue(viewer, out var state))
            {
                state.Timer.Stop();
            }
        }
    }

    private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs args)
    {
        if (sender is not ScrollViewer viewer || args.Handled || viewer.ScrollableHeight <= 0)
        {
            return;
        }

        var nearest = FindNearestScrollViewer(args.OriginalSource as DependencyObject);
        if (nearest is not null && !ReferenceEquals(nearest, viewer) && CanScroll(nearest, args.Delta))
        {
            return;
        }

        if (!CanScroll(viewer, args.Delta))
        {
            return;
        }

        var state = States.GetValue(viewer, CreateState);
        var origin = state.Timer.IsEnabled ? state.TargetOffset : viewer.VerticalOffset;
        state.TargetOffset = Math.Clamp(origin - Math.Sign(args.Delta) * WheelStep, 0, viewer.ScrollableHeight);
        args.Handled = true;

        if (!SystemParameters.ClientAreaAnimation)
        {
            viewer.ScrollToVerticalOffset(state.TargetOffset);
            return;
        }

        if (!state.Timer.IsEnabled)
        {
            state.Timer.Start();
        }
    }

    private static ScrollState CreateState(ScrollViewer viewer)
    {
        var timer = new DispatcherTimer(DispatcherPriority.Render, viewer.Dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        var state = new ScrollState(timer, viewer.VerticalOffset);
        timer.Tick += (_, _) =>
        {
            var difference = state.TargetOffset - viewer.VerticalOffset;
            if (Math.Abs(difference) < 0.5)
            {
                viewer.ScrollToVerticalOffset(state.TargetOffset);
                timer.Stop();
                return;
            }

            viewer.ScrollToVerticalOffset(viewer.VerticalOffset + difference * 0.24);
        };
        return state;
    }

    private static bool CanScroll(ScrollViewer viewer, int wheelDelta) => wheelDelta switch
    {
        > 0 => viewer.VerticalOffset > 0,
        < 0 => viewer.VerticalOffset < viewer.ScrollableHeight,
        _ => false
    };

    private static ScrollViewer? FindNearestScrollViewer(DependencyObject? element)
    {
        while (element is not null)
        {
            if (element is ScrollViewer viewer)
            {
                return viewer;
            }

            element = VisualTreeHelper.GetParent(element);
        }

        return null;
    }

    private sealed record ScrollState(DispatcherTimer Timer, double InitialOffset)
    {
        public double TargetOffset { get; set; } = InitialOffset;
    }
}
