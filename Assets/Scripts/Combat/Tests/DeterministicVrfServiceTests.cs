using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Chainbound.Combat.Tests
{
    public class DeterministicVrfServiceTests
    {
        [Test]
        public async Task SameSeedAndContextProduceSameBytes()
        {
            byte[] context = Encoding.UTF8.GetBytes("zone-1:encounters-v1");
            VrfRequestResult first = await new DeterministicVrfService(42).RequestRandomnessAsync(context);
            VrfRequestResult replay = await new DeterministicVrfService(42).RequestRandomnessAsync(context);
            VrfRequestResult other = await new DeterministicVrfService(42)
                .RequestRandomnessAsync(Encoding.UTF8.GetBytes("zone-2:encounters-v1"));

            Assert.That(first.status, Is.EqualTo(VrfRequestStatus.Fulfilled));
            Assert.That(replay.randomness, Is.EqualTo(first.randomness));
            Assert.That(other.randomness, Is.Not.EqualTo(first.randomness));
        }

        [Test]
        public async Task FulfilledRequestCanBeConsumedExactlyOnce()
        {
            var service = new DeterministicVrfService(7);
            VrfRequestResult requested = await service.RequestRandomnessAsync(new byte[] { 1, 2, 3 });

            VrfRequestResult consumed = await service.ConsumeRandomnessAsync(requested.requestId);
            VrfRequestResult secondConsume = await service.ConsumeRandomnessAsync(requested.requestId);

            Assert.That(consumed.status, Is.EqualTo(VrfRequestStatus.Consumed));
            Assert.That(secondConsume.status, Is.EqualTo(VrfRequestStatus.Failed));
            Assert.That(secondConsume.errorMessage, Does.Contain("already consumed"));
        }

        [Test]
        public void PhaseOneEconomyDefaultsRemainPinned()
        {
            Assert.That(CombatConstants.RAM_COST_GET_KNOWN, Is.EqualTo(5));
            Assert.That(CombatConstants.RAM_COST_STAMP_KNOWN, Is.EqualTo(75));
        }
    }
}
