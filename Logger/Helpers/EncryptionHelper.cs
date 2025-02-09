using Newtonsoft.Json;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Logger
{
    public class EncryptionHelper
    {
        private readonly string _encryptionKey;

        public EncryptionHelper(string encryptionKey)
        {
            _encryptionKey = encryptionKey;
        }

        public void LoadCredentials(out string accessKeyId, out string accessKeySecret)
        {
            string encryptedKey = "ztT9kXbV5AXOI5xmrvRFF68Wf0tnlIYJsW1surVOuSSuNudRJtgmJWxDu7twPO/GkDJj6mwwDD31WK56HljFLg==";
            if (File.Exists("appsettings.json"))
            {
                var config = JsonConvert.DeserializeObject<Config>(File.ReadAllText("appsettings.json"));
                encryptedKey = config?.ApiSettings?.Key ?? encryptedKey;
            }

            string decryptedData = DecryptString(encryptedKey);
            string[] keys = decryptedData.Split('|');
            accessKeyId = keys.Length == 2 ? keys[0] : "";
            accessKeySecret = keys.Length == 2 ? keys[1] : "";
        }

        public string EncryptString(string text)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = Encoding.UTF8.GetBytes(_encryptionKey);
                aes.IV = new byte[16]; // Default to zero IV
                using (var encryptor = aes.CreateEncryptor(aes.Key, aes.IV))
                using (var ms = new MemoryStream())
                using (var cryptoStream = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                using (var writer = new StreamWriter(cryptoStream))
                {
                    writer.Write(text);
                    writer.Flush();
                    cryptoStream.FlushFinalBlock();
                    return Convert.ToBase64String(ms.ToArray());
                }
            }
        }

        public string DecryptString(string encryptedText)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = Encoding.UTF8.GetBytes(_encryptionKey);
                aes.IV = new byte[16]; // Default to zero IV
                using (var decryptor = aes.CreateDecryptor(aes.Key, aes.IV))
                using (var ms = new MemoryStream(Convert.FromBase64String(encryptedText)))
                using (var cryptoStream = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                using (var reader = new StreamReader(cryptoStream))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        public class Config
        {
            public ApiSettings ApiSettings { get; set; }
        }

        public class ApiSettings
        {
            public string Key { get; set; }
        }
    }
}
