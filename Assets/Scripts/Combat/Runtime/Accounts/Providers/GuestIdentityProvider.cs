using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Chainbound.Combat
{
    public class GuestIdentityProvider : MonoBehaviour, IIdentityProvider
    {
        private const string GuestSubjectKey = "SignalJam_GuestIdentitySubject";

        public AuthIdentityType AuthIdentityType => AuthIdentityType.Guest;
        public PlatformIdentityType PlatformIdentityType => PlatformIdentityType.Guest;
        public bool IsAvailable => true;

        public Task<IdentitySignInResult> SignInAsync(AccountSignInRequest request)
        {
            string subject = PlayerPrefs.GetString(GuestSubjectKey, "");
            if (string.IsNullOrWhiteSpace(subject))
            {
                subject = Guid.NewGuid().ToString("N");
                PlayerPrefs.SetString(GuestSubjectKey, subject);
                PlayerPrefs.Save();
            }

            return Task.FromResult(new IdentitySignInResult
            {
                success = true,
                authIdentityType = AuthIdentityType.Guest,
                platformIdentityType = PlatformIdentityType.Guest,
                provider = "local_install",
                providerSubject = subject,
            });
        }

        public Task SignOutAsync()
        {
            return Task.CompletedTask;
        }
    }
}
