﻿using IsmIoTPortal.Models;
using IsmIoTSettings;
using Microsoft.Azure.KeyVault;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Web;

namespace IsmIoTPortal
{
    public class PortalUtils
    {
        public static async Task CreateNewFirmwareUpdateTask(HttpPostedFileBase file, string location)
        {
            IsmIoTPortalContext db = new IsmIoTPortalContext();                
            // Creates all directories in path that do not exist
            Directory.CreateDirectory(location);
            // Full path of file
            string filePath = Path.Combine(location, Path.GetFileName(file.FileName));
            // Save file to disk
            file.SaveAs(filePath);
            // Calculate SHA 256 hash
            var checksum = await Sha256Sum(filePath);
            // Get access to key vault
            var kv = new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(GetToken));
            // Get key
            var key = await kv.GetKeyAsync(ConfigurationManager.AppSettings["kv:fw-signing-key"]);
            // Sign Checksum
            var sig = await kv.SignAsync(
                keyIdentifier: ConfigurationManager.AppSettings["kv:fw-signing-key"],
                algorithm: Microsoft.Azure.KeyVault.WebKey.JsonWebKeySignatureAlgorithm.RS256,
                digest: checksum);
            // Save public key to disk
            var keyPath = Path.Combine(location, "public.pem");
            var publicKey = GetPublicKey(key.Key);
            File.WriteAllText(keyPath, publicKey);
            // Save byte data as sig
            string checksumPath = Path.Combine(location, "sig");
            File.WriteAllBytes(checksumPath, sig.Result);
        }

        public static async Task<byte[]> Sha256Sum(string filePath)
        {
            var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            byte[] sha256sum;
            // Buffered Calculation
            using (var bufferedStream = new BufferedStream(fileStream))
            {
                var sha = new SHA256Managed();
                sha256sum = sha.ComputeHash(bufferedStream);
            }
            return sha256sum;
        }

        private static async Task<string> GetToken(string authority, string resource, string scope)
        {
            var id = ConfigurationManager.AppSettings["hsma-ida:PortalClientId"];
            var secret = ConfigurationManager.AppSettings["hsma-ida:PortalAppKey"];
            return await IsmUtils.GetAccessToken(authority, resource, id, secret);
        }

        public static string GetPublicKey(Microsoft.Azure.KeyVault.WebKey.JsonWebKey key)
        {
            TextWriter outputStream = new StringWriter();
            outputStream.WriteLine("-----BEGIN PUBLIC KEY-----");
            var pkcs8Key = IsmUtils.ConvertJwkToPkcs8(key);
            for (Int32 i = 0; i < pkcs8Key.Length; i += 64)
            {
                outputStream.WriteLine(pkcs8Key.ToCharArray(), i, (Int32)Math.Min(64, pkcs8Key.Length - i));
            }
            outputStream.WriteLine("-----END PUBLIC KEY-----");
            return outputStream.ToString();
        }
    }
}