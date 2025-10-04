using FluentAssertions;
using PowNet.Extensions;
using Xunit;

namespace PowNet.Test.Extensions
{
    public class EncryptionExtensionsErrorTests
    {
        [Fact]
        public void DecryptAesGcm_Should_Fail_On_Corrupted_Data()
        {
            var key = "secret-key";
            var cipher = "hello".EncryptAesGcm(key);
            // corrupt base64 (truncate)
            var bad = cipher.Substring(0, cipher.Length/2);
            Action act = () => bad.DecryptAesGcm(key);
            act.Should().Throw<PowNet.Common.PowNetSecurityException>();
        }

        [Fact]
        public void DecryptRSA_Should_Fail_With_Wrong_Key()
        {
            var kp1 = EncryptionExtensions.GenerateRSAKeyPair();
            var kp2 = EncryptionExtensions.GenerateRSAKeyPair();
            var enc = "data".EncryptRSA(kp1.PublicKey);
            Action act = () => enc.DecryptRSA(kp2.PrivateKey);
            act.Should().Throw<PowNet.Common.PowNetSecurityException>();
        }

        [Fact]
        public void VerifyRSASignature_Should_Return_False_For_Tampered()
        {
            var kp = EncryptionExtensions.GenerateRSAKeyPair();
            var sig = "payload".SignRSA(kp.PrivateKey);
            var ok = "payload".VerifyRSASignature(sig, kp.PublicKey);
            ok.Should().BeTrue();
            var tampered = "payload2".VerifyRSASignature(sig, kp.PublicKey);
            tampered.Should().BeFalse();
        }
    }
}
