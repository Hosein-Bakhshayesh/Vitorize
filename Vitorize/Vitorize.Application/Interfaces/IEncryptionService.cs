namespace Vitorize.Application.Interfaces
{
    public interface IEncryptionService
    {
        string Encrypt(string value);

        string Decrypt(string encryptedValue);
    }
}