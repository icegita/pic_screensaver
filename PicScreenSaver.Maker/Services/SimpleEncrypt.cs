using System;
using System.IO;
using System.Text;

namespace PicScreenSaver.Maker.Services
{
    /// <summary>
    /// 简单加密类 - 使用Base64 + 异或混淆防止直接查看配置
    /// </summary>
    public static class SimpleEncrypt
    {
        private const string Key = "PicScreenSaver2024";

        /// <summary>
        /// 加密字符串
        /// </summary>
        public static string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                return plainText;

            try
            {
                var bytes = Encoding.UTF8.GetBytes(plainText);
                var keyBytes = Encoding.UTF8.GetBytes(Key);

                // 异或混淆
                for (int i = 0; i < bytes.Length; i++)
                {
                    bytes[i] ^= keyBytes[i % keyBytes.Length];
                }

                return Convert.ToBase64String(bytes);
            }
            catch
            {
                return plainText;
            }
        }

        /// <summary>
        /// 解密字符串
        /// </summary>
        public static string Decrypt(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText))
                return cipherText;

            try
            {
                var bytes = Convert.FromBase64String(cipherText);
                var keyBytes = Encoding.UTF8.GetBytes(Key);

                // 异或还原
                for (int i = 0; i < bytes.Length; i++)
                {
                    bytes[i] ^= keyBytes[i % keyBytes.Length];
                }

                return Encoding.UTF8.GetString(bytes);
            }
            catch
            {
                // 如果解密失败，可能是旧格式的纯文本JSON
                return cipherText;
            }
        }
    }
}
