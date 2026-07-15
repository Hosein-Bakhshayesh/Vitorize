using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Vitorize.Application.Common;
using Vitorize.Application.Interfaces;
using Vitorize.Shared.Exceptions;

namespace Vitorize.Infrastructure.Services
{
    public class AesEncryptionService : IEncryptionService
    {
        private readonly EncryptionSettings _settings;

        public AesEncryptionService(IOptions<EncryptionSettings> settings)
        {
            _settings = settings.Value;
        }

        public string Encrypt(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new BusinessException("مقدار کد معتبر نیست.");

            var keyBytes = GetKeyBytes();

            var plainBytes = Encoding.UTF8.GetBytes(value);
            var nonce = RandomNumberGenerator.GetBytes(12);
            var cipher = new byte[plainBytes.Length];
            var tag = new byte[16];
            using var aes = new AesGcm(keyBytes, tag.Length);
            aes.Encrypt(nonce, plainBytes, cipher, tag);
            return $"v2.{Convert.ToBase64String(nonce)}.{Convert.ToBase64String(tag)}.{Convert.ToBase64String(cipher)}";
        }

        public string Decrypt(string encryptedValue)
        {
            if (string.IsNullOrWhiteSpace(encryptedValue))
                throw new BusinessException("مقدار رمزنگاری شده معتبر نیست.");

            var keyBytes = GetKeyBytes();
            if (encryptedValue.StartsWith("v2.", StringComparison.Ordinal))
            {
                try
                {
                    var parts = encryptedValue.Split('.');
                    if (parts.Length != 4) throw new FormatException();
                    var nonce = Convert.FromBase64String(parts[1]);
                    var tag = Convert.FromBase64String(parts[2]);
                    var cipherText = Convert.FromBase64String(parts[3]);
                    if (nonce.Length != 12 || tag.Length != 16) throw new FormatException();
                    var plainText = new byte[cipherText.Length];
                    using var gcm = new AesGcm(keyBytes, tag.Length);
                    gcm.Decrypt(nonce, cipherText, tag, plainText);
                    return Encoding.UTF8.GetString(plainText);
                }
                catch (Exception ex) when (ex is FormatException or CryptographicException)
                {
                    throw new BusinessException("مقدار رمزنگاری‌شده نامعتبر یا دستکاری شده است.");
                }
            }

            // Backward-compatible reader for pre-hardening AES-CBC rows. New writes are always v2/GCM.
            var fullCipher = Convert.FromBase64String(encryptedValue);

            using var aes = Aes.Create();
            aes.Key = keyBytes;

            var iv = new byte[aes.BlockSize / 8];
            var cipher = new byte[fullCipher.Length - iv.Length];

            Buffer.BlockCopy(fullCipher, 0, iv, 0, iv.Length);
            Buffer.BlockCopy(fullCipher, iv.Length, cipher, 0, cipher.Length);

            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

            var decryptedBytes = decryptor.TransformFinalBlock(
                cipher,
                0,
                cipher.Length);

            return Encoding.UTF8.GetString(decryptedBytes);
        }

        private byte[] GetKeyBytes()
        {
            if (string.IsNullOrWhiteSpace(_settings.Key))
                throw new BusinessException("کلید رمزنگاری تنظیم نشده است.");

            var keyBytes = Encoding.UTF8.GetBytes(_settings.Key);

            if (keyBytes.Length != 32)
                throw new BusinessException("کلید رمزنگاری باید دقیقا 32 کاراکتر باشد.");

            return keyBytes;
        }
    }
}
