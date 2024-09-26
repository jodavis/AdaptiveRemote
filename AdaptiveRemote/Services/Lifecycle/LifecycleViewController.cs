using System.Security.RightsManagement;
using AdaptiveRemote.Models;

namespace AdaptiveRemote.Services.Lifecycle;

internal class LifecycleViewController : ILifecycleViewController
{
    private readonly List<Activity> _activities;

    public LifecycleViewController(LifecycleView viewModel)
    {
        _activities =
        [
            new(this, string.Empty)
        ];

        ViewModel = viewModel;
    }

    public LifecycleView ViewModel { get; }

    public ILifecycleActivity StartTask(string description)
    {
        Activity activity = new(this, description);
        lock (_lock)
        {
            _activities.Insert(1, activity);
        }
        UpdateTaskName();
        return activity;
    }

    public void SetPhase(LifecyclePhase phase)
    {
        ViewModel.CurrentPhase = phase;
        _activities[0].Description = DescriptionFor(phase);
        UpdateTaskName();
    }

    public void SetFatalError(Exception error)
    {
        _activities[0].SetFatalError(error);
    }

    private static string DescriptionFor(LifecyclePhase phase)
        => phase switch
        {
            LifecyclePhase.Starting => "Starting application",
            LifecyclePhase.Building => "Building service graph",
            LifecyclePhase.SettingUp => "Starting services",
            LifecyclePhase.CleaningUp => "Cleaning up",
            _ => string.Empty,
        };

    private object _lock = new();
    private void UpdateTaskName()
    {
        lock (_lock)
        {
            Activity topActivity = _activities.Where(x => x.FatalError is not null).LastOrDefault()
                ?? _activities[_activities.Count - 1];

            if (topActivity.FatalError is null &&
                !string.IsNullOrWhiteSpace(topActivity.Description))
            {
                ViewModel.TaskName = $"{topActivity.Description}...";
            }
            else
            {
                ViewModel.TaskName = topActivity.Description;
                ViewModel.FatalError = topActivity.FatalError;
            }

            if (ViewModel.FatalError is not null)
            {
                ViewModel.CurrentPhase = LifecyclePhase.FatalError;
            }
        }
    }

    private class Activity : LifecycleActivity
    {
        private readonly LifecycleViewController _owner;

        internal Activity(LifecycleViewController owner, string initialDescription)
            : base(initialDescription)
        {
            _owner = owner;
        }

        public override void SetFatalError(Exception error)
        {
            if (FatalError is null)
            {
                Description = $"Error while {Description}";
                FatalError = error;
                _owner?.UpdateTaskName();
            }
        }

        public override string Description
        {
            get => base.Description;
            set
            {
                if (FatalError is null)
                {
                    base.Description = value;
                    _owner?.UpdateTaskName();
                }
            }
        }

        public Exception? FatalError { get; private set; }

        public override void Dispose()
        {
            if (FatalError is null)
            {
                _owner._activities.Remove(this);
            }
            _owner.UpdateTaskName();
            base.Dispose();
        }
    }
}
