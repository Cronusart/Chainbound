using UnityEngine;

[CreateAssetMenu(fileName = "MainMenuEventsFeed", menuName = "Signal Jam/UI/Main Menu Events Feed")]
public sealed class MainMenuEventsFeedSO : ScriptableObject
{
    public string title = "EVENTS";

    [TextArea(8, 30)]
    public string body = "Community events will appear here.";
}
