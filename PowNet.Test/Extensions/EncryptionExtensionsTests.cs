using FluentAssertions;
using Xunit;
using PowNet.Extensions;

namespace PowNet.Test.Extensions
{
    public class EncryptionExtensionsTests
    {
        [Fact]
        public void Aes_Gcm_Should_Encrypt_And_Decrypt()
        {
            var key = "secret";
            var ct = "hello".EncryptAesGcm(key);
            var pt = ct.DecryptAesGcm(key);
            pt.Should().Be("hello");
        }

        [Fact]
        public void Legacy_Aes_CBC_Should_Work()
        {
            var key = "secret";
            var ct = "world".Encode(key);
            var pt = ct.Decode(key);
            pt.Should().Be("world");
        }

        [Fact]
        public void RSA_Encrypt_Decrypt_And_Sign_Verify()
        {
            var kp = EncryptionExtensions.GenerateRSAKeyPair(2048);
            var enc = "data".EncryptRSA(kp.PublicKey);
            var dec = enc.DecryptRSA(kp.PrivateKey);
            dec.Should().Be("data");

            var sig = "data".SignRSA(kp.PrivateKey);
            "data".VerifyRSASignature(sig, kp.PublicKey).Should().BeTrue();
        }

        [Fact]
        public void Hashes_And_HMAC_Should_Work()
        {
            "abc".ComputeSHA256().Should().NotBeEmpty();
            "abc".ComputeSHA512().Should().NotBeEmpty();
            var mac = "abc".ComputeHMAC("k");
            "abc".VerifyHMAC(mac, "k").Should().BeTrue();
        }

        [Fact]
        public void Key_Derive_Random_File_Encrypt_Should_Work()
        {
            var key = EncryptionExtensions.DeriveKey("p", 32);
            key.Length.Should().Be(32);
            var s = EncryptionExtensions.GenerateRandomString(10);
            s.Length.Should().Be(10);

            var tmpIn = Path.Combine(Path.GetTempPath(), Guid.NewGuid()+".txt");
            var tmpOut = Path.Combine(Path.GetTempPath(), Guid.NewGuid()+".dat");
            File.WriteAllText(tmpIn, "X");
            EncryptionExtensions.EncryptFile(tmpIn, tmpOut, "k");
            EncryptionExtensions.DecryptFile(tmpOut, tmpIn, "k");
            File.ReadAllText(tmpIn).Should().Be("X");
            File.Delete(tmpIn);
            File.Delete(tmpOut);
        }
    }
}
