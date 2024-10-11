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
            LifecyclePhase.Starting => Phrases.Startup_StartingApplication,
            LifecyclePhase.Building => Phrases.Startup_BuildingServiceGraph,
            LifecyclePhase.SettingUp => Phrases.Startup_StartingServices,
            LifecyclePhase.CleaningUp => Phrases.Cleanup_CleaningUpApplication,
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
                ViewModel.TaskName = Phrases.Ellipsis(topActivity.Description);
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

    private class Activity : ILifecycleActivity
    {
        private readonly LifecycleViewController _owner;
        private string _description;

        internal Activity(LifecycleViewController owner, string initialDescription)
        {
            _owner = owner;
            _description = initialDescription;
        }

        public void SetFatalError(Exception error)
        {
            if (FatalError is null)
            {
                Description = Phrases.ErrorWhile(Description);
                FatalError = error;
                _owner?.UpdateTaskName();
            }
        }

        public string Description
        {
            get => _description;
            set
            {
                if (FatalError is null)
                {
                    _description = value;
                    _owner?.UpdateTaskName();
                }
            }
        }

        public Exception? FatalError { get; private set; }

        public void Dispose()
        {
            if (FatalError is null)
            {
                _owner._activities.Remove(this);
            }
            _owner.UpdateTaskName();
        }
    }
}
