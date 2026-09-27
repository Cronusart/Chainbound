using System.Threading.Tasks;
using UnityEngine;

namespace Chainbound.Combat
{
    public class EditorMockIdentityProvider : MonoBehaviour, IRecoverableSignInProvider, IEditorOnlyAccountProvider
    {
        [SerializeField] private PlatformIdentityType platformIdentityType = PlatformIdentityType.GooglePlay;
        [SerializeField] private AuthIdentityType authIdentityType = AuthIdentityType.GooglePlay;
        [SerializeField] private string provider = "editor_mock";
        [SerializeField] private string providerSubject = "editor_user";
        [SerializeField] private string authUserId = "editor_auth_user";
        [SerializeField] private bool available = true;

        public AuthIdentityType AuthIdentityType => authIdentityType;
        public PlatformIdentityType PlatformIdentityType => platformIdentityType;
        public bool IsAvailable => available && Application.isEditor;
        public bool CanRestore => IsAvailable;

        public Task<IdentitySignInResult> SignInAsync(AccountSignInRequest request)
        {
            return Task.FromResult(CreateResult());
        }

        public Task<IdentitySignInResult> RestoreAsync(AccountSignInRequest request)
        {
            return Task.FromResult(CreateResult());
        }

        public Task SignOutAsync()
        {
            return Task.CompletedTask;
        }

        private IdentitySignInResult CreateResult()
        {
            return new IdentitySignInResult
            {
                success = true,
                authIdentityType = authIdentityType,
                platformIdentityType = platformIdentityType,
                provider = provider,
                providerSubject = providerSubject,
                authUserId = authUserId,
            };
        }
    }
}
