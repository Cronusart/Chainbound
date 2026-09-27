using UnityEngine;

namespace SignalJam.UI.MenuNavigation
{
    [CreateAssetMenu(fileName = "ProfileShareCopy", menuName = "Chainbound/UI/Menu/Profile Share Copy")]
    public sealed class ProfileShareCopySO : ScriptableObject
    {
        [TextArea(1, 3)]
        [SerializeField] private string template = ProfileCardFormatter.DefaultShareTemplate;

        public string Template => string.IsNullOrWhiteSpace(template)
            ? ProfileCardFormatter.DefaultShareTemplate
            : template;
    }
}
