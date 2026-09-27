#import <UIKit/UIKit.h>

static UISelectionFeedbackGenerator *gSelectionFeedback;
static UIImpactFeedbackGenerator *gLightImpactFeedback;
static UINotificationFeedbackGenerator *gNotificationFeedback;

static void SJPrepareFeedbackGenerators(void)
{
    if (@available(iOS 10.0, *))
    {
        if (gSelectionFeedback == nil)
            gSelectionFeedback = [UISelectionFeedbackGenerator new];

        if (gLightImpactFeedback == nil)
            gLightImpactFeedback = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleLight];

        if (gNotificationFeedback == nil)
            gNotificationFeedback = [UINotificationFeedbackGenerator new];
    }
}

extern "C"
{
    void SJUIHaptics_TriggerSelection(void)
    {
        if (@available(iOS 10.0, *))
        {
            SJPrepareFeedbackGenerators();
            [gSelectionFeedback prepare];
            [gSelectionFeedback selectionChanged];
        }
    }

    void SJUIHaptics_TriggerImpactLight(void)
    {
        if (@available(iOS 10.0, *))
        {
            SJPrepareFeedbackGenerators();
            [gLightImpactFeedback prepare];
            [gLightImpactFeedback impactOccurred];
        }
    }

    void SJUIHaptics_TriggerNotificationSuccess(void)
    {
        if (@available(iOS 10.0, *))
        {
            SJPrepareFeedbackGenerators();
            [gNotificationFeedback prepare];
            [gNotificationFeedback notificationOccurred:UINotificationFeedbackTypeSuccess];
        }
    }

    void SJUIHaptics_TriggerNotificationWarning(void)
    {
        if (@available(iOS 10.0, *))
        {
            SJPrepareFeedbackGenerators();
            [gNotificationFeedback prepare];
            [gNotificationFeedback notificationOccurred:UINotificationFeedbackTypeWarning];
        }
    }
}
