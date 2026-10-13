namespace HeavenTool.IO.Audio.Singing;

public sealed class NpcVoiceState
{
    public enum Action { Continue, Trigger, Fade }
    private bool enabled;
    private int previousType;
    private int previousPitch = -1;
    private int previousCountdown;

    public Action Update(bool singingEnabled, int voiceType, int pitch, int nextNoteCountdown)
    {
        var action = Action.Continue;
        var storedCountdown = nextNoteCountdown;

        if (!singingEnabled)
        {
            if (enabled) action = Action.Fade;
            enabled = false;
        }
        else
        {
            enabled = true;
            if (voiceType is >= 2 and <= 8 or 10)
            {
                var changed = voiceType != previousType || pitch != previousPitch;
                if (changed || nextNoteCountdown > previousCountdown)
                {
                    action = Action.Trigger;
                    if (changed) storedCountdown = int.MaxValue;
                }
            }
            else action = Action.Fade;
        }

        previousType = voiceType;
        previousPitch = pitch;
        previousCountdown = storedCountdown;

        return action;
    }
}