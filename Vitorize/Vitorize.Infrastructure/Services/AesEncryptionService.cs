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

            using var aes = Aes.Create();
            aes.Key = keyBytes;
            aes.GenerateIV();

            using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

            var plainBytes = Encoding.UTF8.GetBytes(value);
            var encryptedBytes = encryptor.TransformFinalBlock(
                plainBytes,
                0,
                plainBytes.Length);

            var resultBytes = new byte[aes.IV.Length + encryptedBytes.Length];

            Buffer.BlockCopy(aes.IV, 0, resultBytes, 0, aes.IV.Length);
            Buffer.BlockCopy(encryptedBytes, 0, resultBytes, aes.IV.Length, encryptedBytes.Length);

            return Convert.ToBase64String(resultBytes);
        }

        public string Decrypt(string encryptedValue)
        {
            if (string.IsNullOrWhiteSpace(encryptedValue))
                throw new BusinessException("مقدار رمزنگاری شده معتبر نیست.");

            var keyBytes = GetKeyBytes();
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