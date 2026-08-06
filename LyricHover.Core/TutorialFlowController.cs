namespace LyricHover.Core
{
    public enum TutorialStep
    {
        Inactive,
        AwaitingIslandClick,
        AwaitingFirstSettings,
        RunningBasics,
        AwaitingControlClick,
        AwaitingCustomSettings,
        AwaitingLayoutPage,
        CustomizingModules,
        ShowingLayouts,
        Completed
    }

    public enum TutorialSettingsOpenPurpose
    {
        None,
        FirstSettings,
        CustomModules
    }

    public sealed class TutorialFlowController
    {
        public TutorialStep Step { get; private set; } = TutorialStep.Inactive;

        public bool IsActive => Step != TutorialStep.Inactive && Step != TutorialStep.Completed;

        public void Start()
        {
            Step = TutorialStep.AwaitingIslandClick;
        }

        public bool ContinueFromIslandClick()
        {
            if (Step != TutorialStep.AwaitingIslandClick)
            {
                return false;
            }

            Step = TutorialStep.AwaitingFirstSettings;
            return true;
        }

        public TutorialSettingsOpenPurpose SettingsOpened()
        {
            if (Step == TutorialStep.AwaitingFirstSettings)
            {
                Step = TutorialStep.RunningBasics;
                return TutorialSettingsOpenPurpose.FirstSettings;
            }

            if (Step == TutorialStep.AwaitingCustomSettings)
            {
                Step = TutorialStep.AwaitingLayoutPage;
                return TutorialSettingsOpenPurpose.CustomModules;
            }

            return TutorialSettingsOpenPurpose.None;
        }

        public bool BeginControlClickPractice()
        {
            if (Step != TutorialStep.RunningBasics)
            {
                return false;
            }

            Step = TutorialStep.AwaitingControlClick;
            return true;
        }

        public bool ControlClicked(bool temporaryInteractionHeld)
        {
            if (Step != TutorialStep.AwaitingControlClick || !temporaryInteractionHeld)
            {
                return false;
            }

            Step = TutorialStep.RunningBasics;
            return true;
        }

        public bool RequestCustomSettings()
        {
            if (Step != TutorialStep.RunningBasics)
            {
                return false;
            }

            Step = TutorialStep.AwaitingCustomSettings;
            return true;
        }

        public bool LayoutPageSelected()
        {
            if (Step != TutorialStep.AwaitingLayoutPage)
            {
                return false;
            }

            Step = TutorialStep.CustomizingModules;
            return true;
        }

        public bool CompleteCustomization()
        {
            if (Step != TutorialStep.CustomizingModules)
            {
                return false;
            }

            Step = TutorialStep.ShowingLayouts;
            return true;
        }

        public void Complete()
        {
            Step = TutorialStep.Completed;
        }

        public void Exit()
        {
            Step = TutorialStep.Inactive;
        }
    }
}
